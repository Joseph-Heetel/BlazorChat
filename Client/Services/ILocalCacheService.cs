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
        public ValueTask<IReadOnlyCollection<Channel>> CachedChannels { get; }
        public ValueTask<IReadOnlyCollection<Message>> CachedMessages(ItemId channelId);
        public ValueTask<IReadOnlyCollection<User>> CachedUsers { get; }
        public ValueTask<Result<Channel>> CachedChannel(ItemId channelId);
        public ValueTask<Result<Message>> CachedMessage(ItemId messageId);
        public ValueTask<Result<User>> CachedUser(ItemId userId);
        public ValueTask UpdateItem(Channel channel);
        public ValueTask UpdateItem(Message message);
        public ValueTask UpdateItem(User user);
        public Task Maintain(Channel[] channels);
        public Task ClearCache();
    }
}
