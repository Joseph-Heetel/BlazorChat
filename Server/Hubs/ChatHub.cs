using BlazorChat.Server.Services;
using BlazorChat.Shared;
using Microsoft.AspNetCore.SignalR;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BlazorChat.Server.Hubs
{

    public class ChatHub : Hub
    {
        private readonly IChannelDataService _channelService;
        private readonly ICallSupportService _callService;

        public ChatHub(IChannelDataService channelData, ICallSupportService callSupport)
        {
            _channelService = channelData;
            _callService = callSupport;
        }

        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"User connected to hub: {Context.UserIdentifier}");
            if (ItemId.TryParse(Context.UserIdentifier, out ItemId userId))
            {
                bool wasOnlineBefore = await ConnectionMap.Users.IsConnected(userId);

                List<Task> tasks = new List<Task>();
                ItemId[] channelIds = await _channelService.GetChannels(userId);
                string[] channelIdstrs = channelIds.Select(x => x.ToString()).ToArray();

                // Add connection to all channel groups
                foreach (ItemId channelId in channelIds)
                {
                    tasks.Add(Groups.AddToGroupAsync(Context.ConnectionId, channelId.ToString()));
                    tasks.Add(ConnectionMap.Channels.RegisterConnection(channelId, Context.ConnectionId));
                }

                tasks.Add(ConnectionMap.Users.RegisterConnection(userId, Context.ConnectionId));
                await Task.WhenAll(tasks);
                if (!wasOnlineBefore)
                {
                    await Clients.Groups(channelIdstrs).SendAsync(SignalRConstants.USER_PRESENCE, userId, true);
                }
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            List<Task> tasks = new List<Task>();
            if (ItemId.TryParse(Context.UserIdentifier, out ItemId userId))
            {
                tasks.Add(ConnectionMap.Users.UnregisterConnection(userId, Context.ConnectionId));

                ItemId[] channelIds = await _channelService.GetChannels(userId);
                string[] channelIdstrs = channelIds.Select(x => x.ToString()).ToArray();

                // Add connection to all channel groups
                foreach (ItemId channelId in channelIds)
                {
                    tasks.Add(ConnectionMap.Channels.UnregisterConnection(channelId, Context.ConnectionId));
                }
                await Task.WhenAll(tasks);
                if (!await ConnectionMap.Users.IsConnected(userId))
                {
                    await Clients.Groups(channelIdstrs).SendAsync(SignalRConstants.USER_PRESENCE, userId, false);
                }
            }
            await base.OnDisconnectedAsync(exception);
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

            using (var connections = await ConnectionMap.Users.GetConnections(recipientId))
            {
                if (connections != null)
                {
                    await Clients.Clients(connections).SendAsync(SignalRConstants.CALL_NEGOTIATION, callId, userId, message);
                    return true;
                }
            }
            return false;
        }
    }
}
