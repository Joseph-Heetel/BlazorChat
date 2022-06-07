using BlazorChat.Server.Services;
using BlazorChat.Shared;
using Microsoft.AspNetCore.Connections.Features;
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
        private readonly ILogger _logger;

        public ChatHub(IChannelDataService channelData, ICallSupportService callSupport, ILoggerFactory loggerFactory)
        {
            _channelService = channelData;
            _callService = callSupport;
            _logger = loggerFactory.CreateLogger(nameof(ChatHub));
        }

        public static string MakeChannelGroup(ItemId channelId) { return $"Channel-{channelId}"; }
        public static string MakeUserGroup(ItemId userId) { return $"Channel-{userId}"; }

        public override async Task OnConnectedAsync()
        {
            try
            {
                if (ItemId.TryParse(Context.UserIdentifier, out ItemId userId))
                {
                    bool wasOnlineBefore = await ConnectionMap.Users.IsConnected(userId);

                    List<Task> tasks = new List<Task>();
                    ItemId[] channelIds = await _channelService.GetChannels(userId);
                    string[] hubgroups = channelIds.Select(x => MakeChannelGroup(x)).ToArray();

                    // Add connection to all channel groups
                    foreach (string hubgroup in hubgroups)
                    {
                        tasks.Add(Groups.AddToGroupAsync(Context.ConnectionId, hubgroup));
                    }

                    tasks.Add(Groups.AddToGroupAsync(Context.ConnectionId, MakeUserGroup(userId)));

                    tasks.Add(ConnectionMap.Users.RegisterConnection(userId, Context.ConnectionId));
                    await Task.WhenAll(tasks);
                    if (!wasOnlineBefore)
                    {
                        await Clients.Groups(hubgroups).SendAsync(SignalRConstants.USER_PRESENCE, userId, true);
                    }

                    _logger.LogWarning($"[+] User \"{Context.UserIdentifier}/{Context.ConnectionId}\" connected");
                }
                else
                {
                    _logger.LogWarning($"Connect: User \"{Context.UserIdentifier}/{Context.ConnectionId}\" not parsed to user Id!");
                }
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
                if (ItemId.TryParse(Context.UserIdentifier, out ItemId userId))
                {
                    await ConnectionMap.Users.UnregisterConnection(userId, Context.ConnectionId);


                    ItemId[] channelIds = await _channelService.GetChannels(userId);
                    string[] hubgroups = channelIds.Select(x => MakeChannelGroup(x)).ToArray();


                    // Remove connection from all channel groups
                    if (!await ConnectionMap.Users.IsConnected(userId))
                    {
                        await Clients.Groups(hubgroups).SendAsync(SignalRConstants.USER_PRESENCE, userId, false);
                    }

                    _logger.LogWarning($"[-] User \"{userId}/{Context.ConnectionId}\" disconnected: {exception?.Message}");
                }
                else
                {
                    _logger.LogWarning($"Disconnect: User \"{Context.UserIdentifier}/{Context.ConnectionId}\" not parsed to user Id!");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception disconnecting \"{Context.UserIdentifier}/{Context.ConnectionId}\" : {ex}");
                throw;
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
