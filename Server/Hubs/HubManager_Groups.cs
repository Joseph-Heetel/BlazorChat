using BlazorChat.Server.Services;
using BlazorChat.Shared;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace BlazorChat.Server.Hubs
{
    public partial class HubManager : IHubManager
    {
        private class GroupAssociation
        {
            public GroupAssociation() { }

            public GroupAssociation(string connectionId, string group)
            {
                ConnectionId = connectionId;
                Group = group;
            }

            public string ConnectionId { get; set; } = "";
            public string Group { get; set; } = "";
        }

        private readonly SemaphoreSlim _groupAssociationsSemaphore = new SemaphoreSlim(1);
        /// <summary>
        /// Maps ConnectionIds to Groups
        /// </summary>
        /// <remarks>ConnectionId &lt-&gt Group is a N..N association. This list is dominantly accessed with the intent to read/write to it.
        /// Double Dictionary not used because both dictionaries would be Dictionary&ltstring, List&ltstring&gt&gt and as such quite complicated to use</remarks>
        private readonly List<GroupAssociation> _groupAssociations = new List<GroupAssociation>();

        private readonly SemaphoreSlim _userAssociationsSemaphore = new SemaphoreSlim(1);
        /// <summary>
        /// Maps ConnectionIds to UserIds
        /// </summary>
        private readonly Dictionary<string, ItemId> _userAssociations = new Dictionary<string, ItemId>();
        
        private readonly SemaphoreSlim _onlineUsersSemaphore = new SemaphoreSlim(1);
        /// <summary>
        /// Contains any online user
        /// </summary>
        private readonly HashSet<ItemId> _onlineUsers = new HashSet<ItemId>();

        private readonly IHubContext<ChatHub> _hub;
        private IServiceProvider _serviceProvider;
        private IChannelDataService? __channelService;
        /// <summary>
        /// Lazily accesses channel data service. Circumvents circular service dependency.
        /// </summary>
        /// <remarks>No satisfying way here to resolve the circular dependency:
        /// This service requires ChannelDataService to determine which groups a new connection needs to be added to.
        /// ChannelDataService requires this service to manage groups whenever a user is added/removed from a channel.</remarks>
        private IChannelDataService _channelService
        {
            get
            {
                if (__channelService == null)
                {
                    __channelService = _serviceProvider.GetRequiredService<IChannelDataService>();
                }
                return __channelService;
            }
        }
        private readonly ILogger<HubManager> _logger;

        public HubManager(IHubContext<ChatHub> hub, IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
        {
            _hub = hub;
            _serviceProvider = serviceProvider;
            _logger = loggerFactory.CreateLogger<HubManager>();
            _ = Task.Run(watchConnections);
        }

        public async Task AddUserToGroups(ItemId user, IEnumerable<string> groups)
        {
            List<Task> addTasks = new List<Task>();
            string[] connections = await GetConnectionsForUser(user);
            using (var lockDisposable = await _groupAssociationsSemaphore.WaitAsyncDisposable())
            {
                foreach (var group in groups)
                {
                    foreach (var connection in connections)
                    {
                        _groupAssociations.Add(new GroupAssociation(connection, group));
                        addTasks.Add(_hub.Groups.AddToGroupAsync(connection, group));
                    }
                }
            }
        }

        public async Task ClearGroup(string group)
        {
            List<Task> removeTasks = new List<Task>();
            using (var lockDisposable = await _groupAssociationsSemaphore.WaitAsyncDisposable())
            {
                GroupAssociation[] groupAssociations = _groupAssociations.Where(association => association.Group == group).ToArray();
                foreach (var association in groupAssociations)
                {
                    _groupAssociations.Remove(association);
                    removeTasks.Add(_hub.Groups.RemoveFromGroupAsync(association.ConnectionId, association.Group));
                }
            }
            await Task.WhenAll(removeTasks);
        }

        public async Task<string[]> GetConnectionsForChannel(ItemId channelId)
        {
            string filter = IHubManager.ChannelGroupName(channelId);
            List<string> result = new List<string>();
            using (var lockDisposable = await _groupAssociationsSemaphore.WaitAsyncDisposable())
            {
                result.AddRange(_groupAssociations.Where(association => association.Group == filter).Select(association => association.ConnectionId));
            }
            return result.ToArray();
        }

        public async Task<string[]> GetConnectionsForUser(ItemId userId)
        {
            string filter = IHubManager.UserGroupName(userId);
            List<string> result = new List<string>();
            using (var lockDisposable = await _groupAssociationsSemaphore.WaitAsyncDisposable())
            {
                result.AddRange(_groupAssociations.Where(association => association.Group == filter).Select(association => association.ConnectionId));
            }
            return result.ToArray();
        }

        public async Task RemoveUserFromGroups(ItemId user, IEnumerable<string> groups)
        {
            List<Task> removeTasks = new List<Task>();
            HashSet<string> groupLookup = new HashSet<string>(groups);
            HashSet<string> connectionLookup = new HashSet<string>(await GetConnectionsForUser(user));
            using (var lockDisposable = await _groupAssociationsSemaphore.WaitAsyncDisposable())
            {
                GroupAssociation[] associations = _groupAssociations.Where(association => groupLookup.Contains(association.Group) && connectionLookup.Contains(association.ConnectionId)).ToArray();
                foreach (var association in associations)
                {
                    _groupAssociations.Remove(association);
                    removeTasks.Add(_hub.Groups.RemoveFromGroupAsync(association.ConnectionId, association.Group));
                }
            }
        }

        public async Task<bool> IsOnline(ItemId userId)
        {
            using (var lockDisposable = await _onlineUsersSemaphore.WaitAsyncDisposable())
            {
                return _onlineUsers.Contains(userId);
            }
        }

        public async Task CreateGroup(string group, IEnumerable<ItemId> members)
        {
            List<string> connections = new List<string>();
            HashSet<ItemId> memberLookup = new HashSet<ItemId>(members);
            using (var lockDisposable = await _userAssociationsSemaphore.WaitAsyncDisposable())
            {
                foreach (var pair in _userAssociations)
                {
                    if (memberLookup.Contains(pair.Value))
                    {
                        connections.Add(pair.Key);
                    }
                }
            }
            List<Task> addTasks = new List<Task>();
            using (var lockDisposable = await _groupAssociationsSemaphore.WaitAsyncDisposable())
            {
                foreach (var connection in connections)
                {
                    _groupAssociations.Add(new GroupAssociation(connection, group));
                    addTasks.Add(_hub.Groups.AddToGroupAsync(connection, group));
                }
            }
            await Task.WhenAll(addTasks);
        }
    }
}
