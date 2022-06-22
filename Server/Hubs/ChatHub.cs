using BlazorChat.Server.Services;
using BlazorChat.Shared;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.SignalR;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BlazorChat.Server.Hubs
{
    public interface IHubManager
    {
        /// <summary>
        /// Adds a user to multiple groups
        /// </summary>
        public Task AddUserToGroups(ItemId user, IEnumerable<string> groups);
        /// <summary>
        /// Removes a user from multiple groups
        /// </summary>
        public Task RemoveUserFromGroups(ItemId user, IEnumerable<string> groups);
        /// <summary>
        /// Clears a group (deleting it)
        /// </summary>
        /// <param name="group"></param>
        /// <returns></returns>
        public Task ClearGroup(string group);
        /// <summary>
        /// Creates a new hub group, adding all connections of members to it
        /// </summary>
        public Task CreateGroup(string group, IEnumerable<ItemId> members);
        /// <summary>
        /// Returns true, if user is online
        /// </summary>
        public Task<bool> IsOnline(ItemId userId);
        public static string ChannelGroupName(ItemId channelId) { return $"Channel:{channelId}"; }
        public static string UserGroupName(ItemId userId) { return $"User:{userId}"; }

        /// <summary>
        /// Invoke this from the hub whenever a connection interacts with the hub
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns></returns>
        Task OnPingReceived(string connectionId);
        /// <summary>
        /// Invoke this from the hub whenever a connection connects with the hub
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="userIdRaw"></param>
        /// <returns></returns>
        Task OnConnected(string connectionId, string? userIdRaw);
        /// <summary>
        /// Invoke this from the hub whenever a connection disconnects from the hub
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="userIdRaw"></param>
        /// <returns></returns>
        Task OnDisconnected(string connectionId, string? userIdRaw);
    }

    public class ChatHub : Hub
    {
        private readonly IChannelDataService _channelService;
        private readonly ICallSupportService _callService;
        private readonly ILogger _logger;
        private readonly IHubManager _manager;

        public ChatHub(IChannelDataService channelData, ICallSupportService callSupport, ILoggerFactory loggerFactory, IHubManager hubManager)
        {
            _channelService = channelData;
            _callService = callSupport;
            _manager = hubManager;
            _logger = loggerFactory.CreateLogger(nameof(ChatHub));
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                await _manager.OnConnected(Context.ConnectionId, Context.UserIdentifier);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception connecting {Context.UserIdentifier}/{Context.ConnectionId} : {ex}");
                throw;
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                await _manager.OnDisconnected(Context.ConnectionId, Context.UserIdentifier);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception disconnecting \"{Context.UserIdentifier}/{Context.ConnectionId}\" : {ex}");
                throw;
            }

            await base.OnDisconnectedAsync(exception);
        }

        [HubMethodName(SignalRConstants.HUBKEEPALIVE)]
        public async Task KeepAlive()
        {
            await _manager.OnPingReceived(Context.ConnectionId);
        }

        [HubMethodName(SignalRConstants.CALL_FORWARD_NEGOTIATION)]
        public async Task<bool> ForwardNegotiation(ItemId callId, ItemId recipientId, NegotiationMessage message)
        {
            if (!ItemId.TryParse(Context.UserIdentifier, out ItemId userId))
            {
                return false;
            }
            List<Task<bool>> tasks = new List<Task<bool>>();

            tasks.Add(_callService.IsInCall(callId, userId, true, true));
            tasks.Add(_callService.IsInCall(callId, recipientId, true, true));

            var checks = await Task<bool>.WhenAll(tasks);
            if (checks.Any(v => !v))
            {
                return false;
            }

            await Clients.Group(IHubManager.UserGroupName(recipientId)).SendAsync(SignalRConstants.CALL_NEGOTIATION, callId, userId, message);
            await _manager.OnPingReceived(Context.ConnectionId);
            return true;
        }
    }
}
