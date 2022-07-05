using BlazorChat.Server.Hubs;
using BlazorChat.Server.Models;
using BlazorChat.Server.Services.DatabaseWrapper;
using BlazorChat.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.Cosmos;
using System.Diagnostics;
using System.Text.Json;

namespace BlazorChat.Server.Services
{
    public class MessageDataService : IMessageDataService
    {
        private readonly ITableService<MessageModel> _messagesTable;
        private readonly IIdGeneratorService _idGenService;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IChannelDataService _channelService;

        public MessageDataService(IDatabaseConnection db, IIdGeneratorService idgen, IHubContext<ChatHub> hub, IChannelDataService channels)
        {
            _messagesTable = db.GetTable<MessageModel>(DatabaseConstants.MESSAGESTABLE);
            _idGenService = idgen;
            _hubContext = hub;
            _channelService = channels;
        }


        public async Task<Message?> CreateMessage(ItemId channelId, ItemId authorId, string body, FileAttachment? attachment)
        {
            MessageModel message = new MessageModel()
            {
                Id = _idGenService.Generate(),
                ChannelId = channelId,
                AuthorId = authorId,
                Created = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Body = body,
            };
            if (attachment != null)
            {
                message.Media = MediaModel.FromApiType(attachment);

            }
            if (!message.CheckWellFormed())
            {
                return default;
            }
            var response = await _messagesTable.CreateItemAsync(message);
            if (response.IsSuccess)
            {
                await _channelService.UpdateReadHorizon(channelId, authorId, message.Created);
                await _channelService.PatchLastMessageTimestamp(channelId, message.Created);
                await _hubContext.Clients.Group(IHubManager.ChannelGroupName(channelId)).SendAsync(SignalRConstants.MESSAGE_INCOMING, message.ToApiType());
                return message.ToApiType();
            }
            return null;
        }

        /// <summary>
        /// <see cref="GetMessages(ItemId, DateTimeOffset, bool, int)"/>
        /// </summary>
        private static string MakeQuery_GetMessages(long reference, bool older, int limit)
        {
            return $"SELECT * FROM i " +
                $"WHERE i.created {(older ? "<=" : ">=")} {reference} " +
                $"ORDER BY i.created {(older ? "DESC" : "ASC")} " +
                $"OFFSET 0 LIMIT {limit}";
        }

        /// <inheritdoc/>
        public async Task<Message[]> GetMessages(ItemId channelId, DateTimeOffset reference, bool older, int limit)
        {

            var queryResult = await _messagesTable.QueryItemsAsync(MakeQuery_GetMessages(reference.ToUnixTimeMilliseconds(), older, limit), channelId.ToString());
            if (!queryResult.IsSuccess)
            {
                return Array.Empty<Message>();
            }
            List<MessageModel>? messageModels = queryResult.ResultAsserted;
            if (messageModels != null && messageModels.Count > 0)
            {
                List<Message> result = new List<Message>(messageModels.Count);
                foreach (var message in messageModels)
                {
                    if (message.CheckWellFormed())
                    {
                        var apiType = message.ToApiType();
                        result.Add(apiType);
                    }
                }
                return result.ToArray();
            }
            return Array.Empty<Message>();
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteMessage(ItemId messageId, ItemId channelId)
        {
            // Remove message
            string channelIdstr = channelId.ToString();
            var response = await _messagesTable.DeleteItemAsync(messageId.ToString(), channelIdstr);

            if (response.IsSuccess)
            {
                // Notify hub group that message was deleted
                await _hubContext.Clients.Group(IHubManager.ChannelGroupName(channelId)).SendAsync(SignalRConstants.MESSAGE_DELETED, channelId, messageId);
            }
            return response.IsSuccess;
        }

        private static string MakeQuery_FindMessages(
            ItemId authorId = default,
            long before = default,
            long after = default,
            string searchstr = "")
        {
            List<string> conditions = new List<string>();
            if (!authorId.IsZero)
            {
                conditions.Add($"i.authorId = \"{authorId}\"");
            }
            if (before != default)
            {
                conditions.Add($"i.created < {before}");
            }
            if (after != default)
            {
                conditions.Add($"i.created > {after}");
            }
            if (!string.IsNullOrEmpty(searchstr))
            {
                conditions.Add($"CONTAINS(i.body, \"{searchstr}\", false)");
            }

            Trace.Assert(conditions.Count > 0);

            return $"SELECT * FROM i WHERE {string.Join(" AND ", conditions)}";
        }

        public async Task<Message[]> FindMessages(
            string searchstr = "",
            ItemId channelId = default,
            ItemId authorId = default,
            long before = default,
            long after = default)
        {
            var queryResult = await _messagesTable.QueryItemsAsync(
                MakeQuery_FindMessages(authorId, before, after, searchstr),
                channelId.IsZero ? null : channelId.ToString());
            if (!queryResult.IsSuccess)
            {
                return Array.Empty<Message>();
            }
            List<MessageModel>? messageModels = queryResult.ResultAsserted;
            if (messageModels != null && messageModels.Count > 0)
            {
                List<Message> result = new List<Message>(messageModels.Count);
                foreach (var message in messageModels)
                {
                    if (message.CheckWellFormed())
                    {
                        var apiType = message.ToApiType();
                        result.Add(apiType);
                    }
                }
                return result.ToArray();
            }
            return Array.Empty<Message>();
        }

        public async Task<Message?> GetMessage(ItemId channelId, ItemId messageId)
        {
            var response = await _messagesTable.GetItemAsync(messageId.ToString(), channelId.ToString());
            if (!response.IsSuccess)
            {
                return null;
            }
            var apiType = response.ResultAsserted.ToApiType();
            return apiType;
        }

        public async Task<bool> AttachFormRequest(ItemId channelId, ItemId messageId, ItemId formId)
        {
            var getResponse = await _messagesTable.GetItemAsync(messageId.ToString(), channelId.ToString());
            if (!getResponse.IsSuccess)
            {
                return false;
            }
            var model = getResponse.ResultAsserted;
            model.FormRequestId = formId;
            var replaceResponse = await _messagesTable.ReplaceItemAsync(model);
            if (replaceResponse.IsSuccess)
            {
                await _hubContext.Clients.Group(IHubManager.ChannelGroupName(channelId)).SendAsync(SignalRConstants.MESSAGE_UPDATED, model.ToApiType());
            }
            return replaceResponse.IsSuccess;
        }
    }
}
