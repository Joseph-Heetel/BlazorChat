using BlazorChat.Server.Hubs;
using BlazorChat.Server.Models;
using BlazorChat.Server.Services.DatabaseWrapper;
using BlazorChat.Shared;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using System.Net;

namespace BlazorChat.Server.Services
{
    public class UserDataService : IUserDataService
    {
        private readonly ITableService<UserModel> _usersTable;
        private readonly IIdGeneratorService _idGenService;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IChannelDataService _channelService;

        public UserDataService(IDatabaseConnection db, IIdGeneratorService idgen, IHubContext<ChatHub> hub, IChannelDataService channels)
        {
            _usersTable = db.GetTable<UserModel>(DatabaseConstants.USERSTABLE);
            _idGenService = idgen;
            _hubContext = hub;
            _channelService = channels;
        }
        /// <inheritdoc/>
        public async Task<Shared.User?> GetUser(ItemId id)
        {
            var response = await _usersTable.GetItemAsync(id.ToString());
            if (!response.IsSuccess)
            {
                return null;
            }
            UserModel userModel = response.ResultAsserted;
            User user = userModel.ToApiType();
            user.Online = await ConnectionMap.Users.IsConnected(id);
            return user;
        }

        /// <inheritdoc/>
        public async Task<Shared.User?> CreateUser(string name)
        {
            UserModel user = new UserModel
            {
                Id = _idGenService.Generate(),
                Created = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Name = name
            };
            var result = await _usersTable.CreateItemAsync(user);
            return result.IsSuccess ? user.ToApiType() : null;
        }


        /// <inheritdoc/>
        public async Task<User[]> GetUsers()
        {
            var queryResult = await _usersTable.QueryItemsAsync();
            if (!queryResult.IsSuccess)
            {
                return Array.Empty<User>();
            }
            var userModels = queryResult.ResultAsserted;
            if (userModels.Count > 0)
            {
                List<User> result = new List<User>(userModels.Count);
                foreach (var userModel in userModels)
                {
                    if (userModel.CheckWellFormed())
                    {
                        User user = userModel.ToApiType();
                        user.Online = await ConnectionMap.Users.IsConnected(userModel.Id);
                        result.Add(user);
                    }
                }
                return result.ToArray();
            }
            return Array.Empty<User>();
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateUserName(ItemId userId, string newUserName)
        {
            string userIdstr = userId.ToString();

            // Get user data
            TableActionResult<UserModel> result = await _usersTable.GetItemAsync(userIdstr);
            if (!result.IsSuccess)
            {
                return false;
            }
            UserModel? user = result.ResultAsserted;
            if (user == null || !user.CheckWellFormed())
            {
                return false;
            }

            // Replace with updated data
            user.Name = newUserName;
            if (!user.CheckWellFormed())
            {
                return false;
            }
            var replaceResult = await _usersTable.ReplaceItemAsync(user);
            if (replaceResult.IsSuccess)
            {
                // Notify all connections that share a channel with the user
                ItemId[] channelIds = await _channelService.GetChannels(userId);

                List<string> hubgroups = new List<string>();
                foreach (ItemId channelId in channelIds)
                {
                    hubgroups.Add(ChatHub.MakeChannelGroup(channelId));
                }
                await _hubContext.Clients.Groups(hubgroups).SendAsync(SignalRConstants.USER_UPDATED, userId);
            }
            return replaceResult.IsSuccess;
        }

        public async Task<bool> UpdateAvatar(ItemId userId, FileAttachment fileAttachment)
        {
            string userIdstr = userId.ToString();

            MediaModel mediaModel = MediaModel.FromApiType(fileAttachment);

            if (!mediaModel.CheckWellFormed())
            {
                return false;
            }

            // Get user data
            var userResponse = await _usersTable.GetItemAsync(userIdstr);
            if (!userResponse.IsSuccess)
            {
                return false;
            }
            var user = userResponse.ResultAsserted;
            if (!user.CheckWellFormed())
            {
                return false;
            }

            // Replace with updated data
            user.Avatar = mediaModel;

            if (!user.CheckWellFormed())
            {
                return false;
            }
            var result = await _usersTable.ReplaceItemAsync(user);
            if (result.IsSuccess)
            {
                // Notify all connections that share a channel with the user
                ItemId[] channelIds = await _channelService.GetChannels(userId);

                List<string> hubgroups = new List<string>();
                foreach (ItemId channelId in channelIds)
                {
                    hubgroups.Add(ChatHub.MakeChannelGroup(channelId));
                }
                await _hubContext.Clients.Groups(hubgroups).SendAsync(SignalRConstants.USER_UPDATED, userId);
            }
            return result.IsSuccess;
        }
    }
}
