using BlazorChat.Shared;
using Microsoft.AspNetCore.SignalR;

namespace BlazorChat.Server.Hubs
{
    public partial class HubManager
    {
        public async Task OnConnected(string connectionId, string? userIdRaw)
        {
            if (ItemId.TryParse(userIdRaw, out ItemId userId))
            {
                await initUserConnection(connectionId, userId);

                _logger.LogWarning($"[+] User \"{userIdRaw}/{connectionId}\" connected");
            }
            else
            {
                _logger.LogWarning($"[!] User \"{userIdRaw}/{connectionId}\" not parsed to user Id!");
            }
        }

        private async Task initUserConnection(string connectionId, ItemId userId)
        {
            bool wasOnlineBefore = false;
            using (var lockDisposable = await _onlineUsersSemaphore.WaitAsyncDisposable())
            {
                wasOnlineBefore = !_onlineUsers.Add(userId);
            }

            List<Task> tasks = new List<Task>();
            ItemId[] channelIds = await _channelService.GetChannels(userId);
            IEnumerable<string> groups = channelIds.Select(x => IHubManager.ChannelGroupName(x)).Append(IHubManager.UserGroupName(userId));

            // Add connection to all channel groups
            using (var lockDisposable = await _groupAssociationsSemaphore.WaitAsyncDisposable())
            {
                foreach (string group in groups)
                {
                    _groupAssociations.Add(new GroupAssociation(connectionId, group));
                    tasks.Add(_hub.Groups.AddToGroupAsync(connectionId, group));
                }
            }

            // Associate the connection with userId

            await Task.WhenAll(tasks);
            if (!wasOnlineBefore)
            {
                await _hub.Clients.Groups(groups).SendAsync(SignalRConstants.USER_PRESENCE, userId, true);
            }

            // Watch the connection

            _ = Task.Run(() => OnPingReceived(connectionId));
        }

        public async Task OnDisconnected(string connectionId, string? userIdRaw)
        {
            if (ItemId.TryParse(userIdRaw, out ItemId userId))
            {
                await deinitUserConnection(connectionId, userId);

                _logger.LogWarning($"[-] User \"{userIdRaw}/{connectionId}\" disconnected");
            }
            else
            {
                _logger.LogWarning($"Disconnect: User \"{userIdRaw}/{connectionId}\" not parsed to user Id!");
            }
        }

        private async Task deinitUserConnection(string connectionId, ItemId userId)
        {
            // Remove connection from all channel groups
            using (var lockDisposable = await _groupAssociationsSemaphore.WaitAsyncDisposable())
            {
                _groupAssociations.RemoveAll(association => association.ConnectionId == connectionId);
            }
            bool wentOffline = false;
            using (var lockDisposable = await _userAssociationsSemaphore.WaitAsyncDisposable())
            {
                _userAssociations.Remove(connectionId);
                wentOffline = !_userAssociations.Values.Any(id => id == userId);
            }

            if (wentOffline && !userId.IsZero)
            {
                ItemId[] channelIds = await _channelService.GetChannels(userId);
                IEnumerable<string> groups = channelIds.Select(x => IHubManager.ChannelGroupName(x)).Append(IHubManager.UserGroupName(userId));

                using (var lockDisposable = await _onlineUsersSemaphore.WaitAsyncDisposable())
                {
                    _onlineUsers.Remove(userId);
                }
                await _hub.Clients.Groups(groups).SendAsync(SignalRConstants.USER_PRESENCE, userId, false);
            }
        }
    }
}
