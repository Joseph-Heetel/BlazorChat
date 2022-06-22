using BlazorChat.Shared;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace BlazorChat.Server.Hubs
{
    public partial class HubManager
    {
        private enum EConnectionState
        {
            /// <summary>
            /// The connection is active
            /// </summary>
            Alive,
            /// <summary>
            /// The connection has been inactive and the opposing party prompted to respond
            /// </summary>
            Waiting,
            /// <summary>
            /// The connection has been inactive for too long and subsequently terminated
            /// </summary>
            Terminated
        }
        private struct ConnectionWatch
        {
            public ConnectionWatch() { }
            public ConnectionWatch(string connectionId, DateTimeOffset lastAction, EConnectionState state)
            {
                ConnectionId = connectionId;
                LastAction = lastAction;
                State = state;
            }

            public string ConnectionId { get; set; } = "";
            public DateTimeOffset LastAction { get; set; } = default;
            public EConnectionState State { get; set; } = default;
        }

        private readonly ConcurrentDictionary<string, ConnectionWatch> _watches = new();
        public static readonly TimeSpan ActivityThreshhold = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan ExpireThreshhold = ActivityThreshhold * 2;

        private async Task onConnectionTimeout(ConnectionWatch connectionWatch)
        {
            ItemId userId = default;
            using (SemaphoreAccessDisposable lockDisposable = await _userAssociationsSemaphore.WaitAsyncDisposable())
            {
                _userAssociations.TryGetValue(connectionWatch.ConnectionId, out userId);
            }
            await deinitUserConnection(connectionWatch.ConnectionId, userId);
        }

        public Task OnPingReceived(string connectionId)
        {
            _watches[connectionId] = new ConnectionWatch(connectionId, DateTimeOffset.UtcNow, EConnectionState.Alive);
            return Task.CompletedTask;
        }

        private Task pingConnection(ConnectionWatch connectionWatch)
        {
            return _hub.Clients.Client(connectionWatch.ConnectionId).SendAsync(SignalRConstants.CLIENTKEEPALIVE);
        }

        private async Task watchConnections()
        {
            while (true)
            {
                List<ConnectionWatch> ping = new List<ConnectionWatch>();
                List<ConnectionWatch> expire = new List<ConnectionWatch>();
                DateTimeOffset activityBarrier = DateTimeOffset.UtcNow - ActivityThreshhold;
                DateTimeOffset expireBarrier = DateTimeOffset.UtcNow - ExpireThreshhold;

                // Gather all connections which need to be refreshed

                foreach (var connection in _watches.Values)
                {
                    if (connection.LastAction < expireBarrier)
                    {
                        expire.Add(connection);
                    }
                    else if (connection.LastAction < activityBarrier && connection.State == EConnectionState.Alive)
                    {
                        ping.Add(connection);
                    }
                }

                // Schedule refresh pings on connections requiring it off thread

                foreach (var connection in ping)
                {
                    var newValue = new ConnectionWatch(connection.ConnectionId, connection.LastAction, EConnectionState.Waiting);
                    _watches[connection.ConnectionId] = newValue;
                    _ = Task.Run(() => pingConnection(newValue));
                }

                // Remove connections which have expired, and process the timeout actions offthread

                foreach (var connection in expire)
                {
                    _watches.Remove(connection.ConnectionId, out _);
                    var newValue = new ConnectionWatch(connection.ConnectionId, connection.LastAction, EConnectionState.Terminated);
                    _ = Task.Run(() => onConnectionTimeout(connection));

                }
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }
}
