using BlazorChat.Shared;


namespace BlazorChat.Client.Services
{
    public struct Result<T> where T : class
    {
        public bool Success;
        public T? Value;
        public bool TryGet(out T value)
        {
            value = Value!;
            return Success;
        }
    }

    /// <summary>
    /// Service wrapping a local browser storage cache of channels, messages and users
    /// </summary>
    public interface ILocalCacheService
    {
        /// <summary>
        /// Get a list of all cached channels
        /// </summary>
        public ValueTask<IReadOnlyCollection<Channel>> CachedChannels { get; }
        /// <summary>
        /// Get a list of all cached messages of a channel
        /// </summary>
        public ValueTask<IReadOnlyCollection<Message>> CachedMessages(ItemId channelId);
        /// <summary>
        /// Get a list of all cached users
        /// </summary>
        public ValueTask<IReadOnlyCollection<User>> CachedUsers { get; }
        ///// <summary>
        ///// Get a specific cached channel
        ///// </summary>
        //public ValueTask<Result<Channel>> CachedChannel(ItemId channelId);
        ///// <summary>
        ///// Get a specific cached message
        ///// </summary>
        //public ValueTask<Result<Message>> CachedMessage(ItemId messageId);
        ///// <summary>
        ///// Get a specific cached user
        ///// </summary>
        //public ValueTask<Result<User>> CachedUser(ItemId userId);
        /// <summary>
        /// Update a channel with new information
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public ValueTask UpdateItem(Channel channel);
        /// <summary>
        /// Update a message with new information
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public ValueTask UpdateItem(Message message);
        /// <summary>
        /// Update a user with new information
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public ValueTask UpdateItem(User user);
        /// <summary>
        /// Syncs the cached list of channels with the array provided as parameter <paramref name="channels"/>
        /// </summary>
        public Task Maintain(Channel[] channels);
        /// <summary>
        /// Purges the entire cache (for example on user logout)
        /// </summary>
        public Task ClearCache();
        /// <summary>
        /// Retrieve a placeholder session for offline use
        /// </summary>
        public Task<Session?> GetOfflineSession();
        /// <summary>
        /// Save session to be retrievable as offline session
        /// </summary>
        public Task SetOfflineSession(Session session);
        /// <summary>
        /// Set pending messages to be stored for later upload
        /// </summary>
        public Task SetQueuedMessages(IReadOnlyCollection<MessageDispatchProcess> processes);
        /// <summary>
        /// Retrieve pending messages
        /// </summary>
        /// <returns></returns>
        public Task<MessageDispatchProcess[]> GetQueuedMessages();
    }
}
