using CustomBlazorApp.Shared;
using System.Collections.Concurrent;
using System.Diagnostics;
using static CustomBlazorApp.CosmosDBExtensions;
using static CustomBlazorApp.OtherExtensions;
using static CustomBlazorApp.Shared.Extensions;

namespace CustomBlazorApp.Server.Hubs
{
    public class ConnectionBag
    {
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private HashSet<string> _connections = new HashSet<string>();

        public ConnectionBag() { }
        public ConnectionBag(params string[] connections)
        {
            foreach (var connection in connections) _connections.Add(connection);
        }
        public Task<LockedEnumerable<string>> GetLockedEnumerable() => _connections.AsLockedEnumerable(_semaphore);
        public async Task<bool> Add(string connection)
        {
            using (await _semaphore.WaitAsyncDisposable())
            {
                return _connections.Add(connection);
            }
        }
        public async Task<int> Remove(string connection)
        {
            using (await _semaphore.WaitAsyncDisposable())
            {
                _connections.Remove(connection);
                return _connections.Count;
            }
        }
        public async Task<bool> Contains(string connection)
        {
            using (await _semaphore.WaitAsyncDisposable())
            {
                return _connections.Contains(connection);
            }
        }
    }

    /// <summary>
    /// Static cache for associating connections with Id decorated objects
    /// </summary>
    public class ConnectionMap
    {
        /// <summary>
        /// Maps connections to user Ids
        /// </summary>
        public static readonly ConnectionMap Users = new ConnectionMap();
        /// <summary>
        /// Maps connections to channel Ids
        /// </summary>
        public static readonly ConnectionMap Channels = new ConnectionMap();

        private ConcurrentDictionary<ItemId, ConnectionBag> _connections = new ConcurrentDictionary<ItemId, ConnectionBag>();

        public Task RegisterConnection(ItemId id, string connection)
        {
            _connections.AddOrUpdate(id, new ConnectionBag(connection), (id, bag) =>
            {
                bag.Add(connection).Wait();
                return bag;
            });
            return Task.CompletedTask;
        }

        public async Task UnregisterConnection(ItemId id, string connection)
        {
            if (_connections.TryGetValue(id, out ConnectionBag? userConnections))
            {
                if (await userConnections.Remove(connection) == 0)
                {
                    Debug.Assert(_connections.TryRemove(id, out _));
                }
            }
        }

        public async Task<LockedEnumerable<string>?> GetConnections(ItemId id)
        {
            if (_connections.TryGetValue(id, out ConnectionBag? userConnections))
            {
                return await userConnections.GetLockedEnumerable();
            }
            return null;
        }

        public Task<bool> IsConnected(ItemId id)
        {
            return Task.FromResult(_connections.ContainsKey(id));
        }
    }
}
