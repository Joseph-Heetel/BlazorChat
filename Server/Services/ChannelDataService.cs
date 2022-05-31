using CustomBlazorApp.Server.Hubs;
using CustomBlazorApp.Server.Models;
using CustomBlazorApp.Server.Services.DatabaseWrapper;
using CustomBlazorApp.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.Cosmos;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace CustomBlazorApp.Server.Services
{
    public class ChannelDataService : IChannelDataService
    {

        private readonly ITableService<ChannelModel> _channelsTable;
        private readonly ITableService<MembershipModel> _membersTable;
        private readonly ITableService<MessageModel> _messagesTable;
        private readonly IIdGeneratorService _idGenService;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IStorageService _storageService;

        public ChannelDataService(IDatabaseConnection db, IIdGeneratorService idgen, IHubContext<ChatHub> hub, IStorageService storage)
        {
            _channelsTable = db.GetTable<ChannelModel>(DatabaseConstants.CHANNELSTABLE);
            _membersTable = db.GetTable<MembershipModel>(DatabaseConstants.MEMBERSHIPSTABLE);
            _messagesTable = db.GetTable<MessageModel>(DatabaseConstants.MESSAGESTABLE);
            _idGenService = idgen;
            _hubContext = hub;
            _storageService = storage;
        }

        /// <summary>
        /// Find all users who are member of a channel
        /// </summary>
        private static string MakeQuery_Participants(ItemId channelId)
        {
            return $"SELECT * FROM i WHERE i.channelId = \"{channelId}\"";
        }

        /// <summary>
        /// Find all channels that a user is member of
        /// </summary>
        private static string MakeQuery_Participations(ItemId userId)
        {
            return $"SELECT * FROM i WHERE i.userId = \"{userId}\"";
        }

        /// <inheritdoc/>
        public async Task<ItemId[]> GetChannels(ItemId userId)
        {
            var response = await _membersTable.QueryItemsAsync(MakeQuery_Participations(userId), "0");
            if (!response.IsSuccess)
            {
                return Array.Empty<ItemId>();
            }
            var participations = response.ResultAsserted;
            return participations.Select(participation => participation.ChannelId).ToArray();
        }

        /// <inheritdoc/>
        public async Task<Channel[]> GetChannels()
        {
            var response = await _channelsTable.QueryItemsAsync();
            if (!response.IsSuccess)
            {
                return Array.Empty<Channel>();
            }
            List<ChannelModel> queryResult = response.ResultAsserted;
            if (queryResult.Count > 0)
            {
                List<Channel> result = new List<Channel>(queryResult.Count);
                foreach (var channel in queryResult)
                {
                    if (channel.CheckWellFormed())
                    {
                        result.Add(channel.ToApiType());
                    }
                }
                return result.ToArray();
            }
            return Array.Empty<Channel>();
        }

        /// <inheritdoc/>
        public async Task<Channel?> GetChannel(ItemId channelId, ItemId requestingUserId)
        {
            // get base data and all user ids
            var getBaseDataTask = _channelsTable.GetItemAsync(channelId.ToString());
            var getMembershipsTask = _membersTable.QueryItemsAsync(MakeQuery_Participants(channelId), "0");

            await Task.WhenAll(getBaseDataTask, getMembershipsTask);

            // Evaluate and transform results
            var baseData = getBaseDataTask.Result;
            var membershipsRequest = getMembershipsTask.Result;
            if (!baseData.IsSuccess || !membershipsRequest.IsSuccess)
            {
                return null;
            }

            Channel result = baseData.ResultAsserted.ToApiType();
            var memberships = membershipsRequest.ResultAsserted;

            var participants = new HashSet<Participation>(memberships.Count);
            foreach (var membership in memberships)
            {
                Participation participation = membership.ToApiType();
                if (participation.Id == requestingUserId)
                {
                    result.HasUnread = participation.LastReadTS < result.LastMessageTS;
                }
                participants.Add(participation);
            }
            result.Participants = participants;

            return result;
        }

        /// <inheritdoc/>
        public async Task<bool> ChannelExists(ItemId channelId)
        {
            var channel = await _channelsTable.GetItemAsync(channelId.ToString());
            return channel.IsSuccess;
        }

        /// <inheritdoc/>
        public async Task<Participation?> GetParticipation(ItemId channelId, ItemId userId)
        {
            var memberShipResponse = await _membersTable.GetItemAsync(MembershipModel.MakeId(channelId, userId), "0");
            if (memberShipResponse.IsSuccess)
            {
                return memberShipResponse.ResultAsserted.ToApiType();
            }
            return null;
        }

        /// <inheritdoc/>
        public async Task<bool> IsMember(ItemId channelId, ItemId userId)
        {
            var memberShipResponse = await _membersTable.GetItemAsync(MembershipModel.MakeId(channelId, userId), "0");
            return memberShipResponse.IsSuccess;
        }

        /// <inheritdoc/>
        public async Task<Channel?> CreateChannel(string name, HashSet<ItemId> userIds)
        {
            // Generate Id, fill information
            ItemId channelId = _idGenService.Generate();
            ChannelModel channelDataModel = new ChannelModel()
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Id = channelId,
                Name = name,
                LastMessage = 0
            };

            // Upload channel data
            var result = await _channelsTable.CreateItemAsync(channelDataModel);
            if (!result.IsSuccess)
            {
                return null;
            }

            // transform into fully qualified channel object
            Channel channel = channelDataModel.ToApiType();
            foreach (var userId in userIds)
            {
                channel.Participants.Add(new Participation(userId, channel.CreatedTS, channel.LastMessageTS));
            }

            string channelIdStr = channelId.ToString();

            // Create all memberships and add all user connections to new hub group
            List<Task> memberShipTasks = new List<Task>();
            foreach (var memberId in userIds)
            {
                MembershipModel membership = new MembershipModel()
                {
                    ChannelId = channelId,
                    UserId = memberId,
                    Joined = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    LastRead = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                };
                memberShipTasks.Add(_membersTable.CreateItemAsync(membership));
                using (var connections = await ConnectionMap.Users.GetConnections(memberId))
                {
                    if (connections != null)
                    {
                        foreach (var connection in connections)
                        {
                            memberShipTasks.Add(_hubContext.Groups.AddToGroupAsync(connection, channelIdStr));
                        }
                    }
                }
            }
            await Task.WhenAll(memberShipTasks);

            // Notify hub group that they have new channel available
            await _hubContext.Clients.Group(channelIdStr).SendAsync(SignalRConstants.CHANNEL_LISTCHANGED);
            return channel;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteChannel(ItemId channelId)
        {
            string channelIdstr = channelId.ToString();

            // Remove channel data and get all memberships
            var removeChannelTask = _channelsTable.DeleteItemAsync(channelIdstr);
            var getMembershipsTask = _membersTable.QueryItemsAsync(MakeQuery_Participants(channelId), "0");
            await Task.WhenAll(removeChannelTask, getMembershipsTask);

            if (removeChannelTask.Result.IsSuccess)
            {
                // Run Bulk delete on memberships and messages
                var deleteMessagesTask = _messagesTable.BulkDeleteItemsAsync($"SELECT i.id FROM i", channelIdstr);
                var deleteMembershipsTask = _membersTable.BulkDeleteItemsAsync(MakeQuery_Participants(channelId), "0");

                // Delete the blob container storing media
                var deleteBlobTasks = _storageService.DeleteContainer(channelId);

                // Notify channel members that the channels available to them have changed
                var notifyChannelMembersTask = _hubContext.Clients.Group(channelId.ToString()).SendAsync(SignalRConstants.CHANNEL_LISTCHANGED);

                // Clear the hub group. For some (stupid or otherwise) reason SignalR hubs don't allow killing a whole group. So we do it manually.
                // Leaving the group to persist would be workable if the server is expected to restart regularly,
                // or if Users aren't expected to persist for long durations
                var clearHubGroupTask = Task.Run(async () =>
                {
                    List<Task> tasks = new List<Task>();
                    using (var connections = await ConnectionMap.Channels.GetConnections(channelId))
                    {
                        if (connections != null)
                        {
                            foreach (var connection in connections)
                            {
                                tasks.Add(_hubContext.Groups.RemoveFromGroupAsync(connection, channelIdstr));
                            }
                        }
                    }
                    await Task.WhenAll(tasks);
                });

                await Task.WhenAll(
                    deleteMessagesTask,
                    deleteMembershipsTask,
                    deleteBlobTasks,
                    clearHubGroupTask);
                return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public async Task<bool> AddMember(ItemId channelId, ItemId userId)
        {
            MembershipModel membership = new MembershipModel()
            {
                ChannelId = channelId,
                UserId = userId,
                Joined = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                LastRead = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
            };
            var response = await _membersTable.CreateItemAsync(membership);
            if (response.IsSuccess)
            {
                string channelIdStr = channelId.ToString();
                List<Task> tasks = new List<Task>();

                // Notify members that the channel has been updated
                tasks.Add(_hubContext.Clients.Group(channelIdStr).SendAsync(SignalRConstants.CHANNEL_UPDATED, channelId));

                // Add all user connections to the channel group,
                // and notify each connection that new channel is available
                using (var connections = await ConnectionMap.Users.GetConnections(userId))
                {
                    if (connections != null)
                    {
                        foreach (var connection in connections)
                        {
                            tasks.Add(_hubContext.Groups.AddToGroupAsync(connection, channelIdStr));
                            tasks.Add(_hubContext.Clients.Client(connection).SendAsync(SignalRConstants.CHANNEL_LISTCHANGED));
                        }
                    }
                }
                await Task.WhenAll(tasks);
            }
            return response.IsSuccess;
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveMember(ItemId channelId, ItemId userId)
        {
            string channelIdStr = channelId.ToString();
            var response = await _membersTable.DeleteItemAsync(MembershipModel.MakeId(channelId, userId), "0");
            if (response.IsSuccess)
            {
                List<Task> tasks = new List<Task>();

                // Notify members that the channel has been updated
                tasks.Add(_hubContext.Clients.Group(channelIdStr).SendAsync(SignalRConstants.CHANNEL_UPDATED, channelId));

                // Remove all user connections to the channel group,
                // and notify each connection that a channel is no longer available
                using (var connections = await ConnectionMap.Users.GetConnections(userId))
                {
                    if (connections != null)
                    {
                        foreach (var connection in connections)
                        {
                            tasks.Add(_hubContext.Groups.RemoveFromGroupAsync(connection, channelIdStr));
                            tasks.Add(_hubContext.Clients.Client(connection).SendAsync(SignalRConstants.CHANNEL_LISTCHANGED));
                        }
                    }
                }
                await Task.WhenAll(tasks);
            }
            return response.IsSuccess;
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateChannelName(ItemId channelId, string newName)
        {
            string channelIdstr = channelId.ToString();

            // Fetch channel data
            var channelResponse = await _channelsTable.GetItemAsync(channelIdstr);
            if (!channelResponse.IsSuccess)
            {
                return false;
            }

            var channel = channelResponse.ResultAsserted;

            // Replace with updated data
            channel.Name = newName;
            if (!channel.CheckWellFormed())
            {
                return false;
            }
            var result = await _channelsTable.ReplaceItemAsync(channel);

            if (result.IsSuccess)
            {
                // Notify members that the channel was updated
                await _hubContext.Clients.Group(channelIdstr).SendAsync(SignalRConstants.CHANNEL_UPDATED, channelId);
            }
            return result.IsSuccess;
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateReadHorizon(ItemId channelId, ItemId userId, long timeOfReadMessage)
        {
            // Patch user read timestamp in memberships table
            var getResponse = await _membersTable.GetItemAsync(MembershipModel.MakeId(channelId, userId), "0");
            if (getResponse.IsSuccess)
            {
                var model = getResponse.ResultAsserted;

                model.LastRead = timeOfReadMessage;

                var replaceResponse = await _membersTable.ReplaceItemAsync(model);
                return replaceResponse.IsSuccess;
            }

            return false;
        }

        public async Task<bool> PatchLastMessageTimestamp(ItemId channelId, long messageSendTime)
        {
            // Patch channel read timestamp in channel table
            var getResponse = await _channelsTable.GetItemAsync(channelId.ToString());
            if (getResponse.IsSuccess)
            {
                var model = getResponse.ResultAsserted;

                model.LastMessage = messageSendTime;

                var replaceResponse = await _channelsTable.ReplaceItemAsync(model);
                return replaceResponse.IsSuccess;
            }

            return false;
        }
    }
}
