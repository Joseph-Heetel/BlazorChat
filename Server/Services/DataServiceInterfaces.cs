using BlazorChat.Shared;

namespace BlazorChat.Server.Services
{
    /// <summary>
    /// Service providing access to the data backend
    /// </summary>
    public interface ILoginDataService
    {
        /// <summary>
        /// Returns true, if <paramref name="login"/> exists
        /// </summary>
        public Task<bool> TestLogin(string login);
        /// <summary>
        /// Returns the user Id, if <paramref name="login"/> and <paramref name="passwordHash"/> perfectly matches an existing pair. Returns null otherwise.
        /// </summary>
        public Task<ItemId?> TestLoginPassword(string login, ByteArray passwordHash);
        /// <summary>
        /// Creates a login - password pair, returns the associated user Id if successful. Returns null otherwise.
        /// </summary>
        public Task<ItemId?> CreateLoginPassword(string login, ByteArray passwordHash);
        /// <summary>
        /// Creates a login - password pair, returns the associated <see cref="User"/> if successful. Returns null otherwise.
        /// </summary>
        public Task<User?> CreateUserAndLogin(string name, string login, ByteArray passwordHash);
    }

    public interface IUserDataService
    {
        /// <summary>
        /// Gets an existing <see cref="User"/> by matching <paramref name="userId"/>. Returns null if not found.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public Task<User?> GetUser(ItemId userId);
        /// <summary>
        /// Get all users present in the chat service
        /// </summary>
        public Task<User[]> GetUsers();
        /// <summary>
        /// Creates a new user with <paramref name="name"/>. Returns <see cref="User"/> if successful, null otherwise.
        /// </summary>
        public Task<User?> CreateUser(string name);
        public Task<bool> UpdateUserName(ItemId userId, string newUserName);
        public Task<bool> UpdateAvatar(ItemId userId, FileAttachment fileAttachment);
    }

    public interface IChannelDataService
    {
        /// <summary>
        /// Gets ids of all channels which the user identified with <paramref name="userId"/> is a member of
        /// </summary>
        public Task<ItemId[]> GetChannels(ItemId userId);
        /// <summary>
        /// Gets data of all channels present in the chat service.
        /// </summary>
        public Task<Channel[]> GetChannels();
        public Task<bool> ChannelExists(ItemId channelId);
        /// <summary>
        /// Gets the channel identified by <paramref name="channelId"/>. Returns null if not found.
        /// </summary>
        public Task<Channel?> GetChannel(ItemId channelId, ItemId requestingUserId);
        /// <summary>
        /// Deletes the channel identified by <paramref name="channelId"/>
        /// </summary>
        public Task<bool> DeleteChannel(ItemId channelId);
        /// <summary>
        /// Returns true, if user <paramref name="userId"/> is member in channel <paramref name="channelId"/>
        /// </summary>
        public Task<Participation?> GetParticipation(ItemId channelId, ItemId userId);
        /// <summary>
        /// Returns true, if user <paramref name="userId"/> is member in channel <paramref name="channelId"/>
        /// </summary>
        public Task<bool> IsMember(ItemId channelId, ItemId userId);
        /// <summary>
        /// Sets user <paramref name="userId"/> as member of channel <paramref name="channelId"/>. Returns success.
        /// </summary>
        public Task<bool> AddMember(ItemId channelId, ItemId userId);
        /// <summary>
        /// Removes user <paramref name="userId"/> as member of channel <paramref name="channelId"/>. Returns success.
        /// </summary>
        public Task<bool> RemoveMember(ItemId channelId, ItemId userId);
        /// <summary>
        /// Creates a new channel with <paramref name="name"/> and <paramref name="userIds"/>. Returns null if failed.
        /// </summary>
        public Task<Channel?> CreateChannel(string name, HashSet<ItemId> userIds);
        public Task<bool> UpdateChannelName(ItemId channelId, string newName);
        public Task<bool> UpdateReadHorizon(ItemId channelId, ItemId userId, long timeOfReadMessage);
        public Task<bool> PatchLastMessageTimestamp(ItemId channelId, long messageSendTime);
    }

    public interface IMessageDataService
    {
        /// <summary>
        /// Creates a new message sent to <paramref name="channelId"/> authored by <paramref name="userId"/> with message body <paramref name="body"/>. Returns null if failed.
        /// </summary>
        public Task<Message?> CreateMessage(ItemId channelId, ItemId userId, string body, FileAttachment? attachment);
        /// <summary>
        /// Gets a list of messages in channel <paramref name="channelId"/>. If <paramref name="older"/> is set, messages older or equal to <paramref name="reference"/> are returned. 
        /// Otherwise, newer or equal. At most <paramref name="limit"/> message are returned. An empty array indicates no messages matching the parameters
        /// </summary>
        public Task<Message[]> GetMessages(ItemId channelId, DateTimeOffset reference, bool older, int limit);
        /// <summary>
        /// Deletes the message in channel <paramref name="channelId"/> identified by <paramref name="messageId"/>. Returns success.
        /// </summary>
        public Task<bool> DeleteMessage(ItemId messageId, ItemId channelId);
        Task<Message[]> FindMessages(string searchstr = "", ItemId channelId = default, ItemId authorId = default, long before = default, long after = default);
        Task<Message?> GetMessage(ItemId channelId, ItemId messageId);
        Task<bool> AttachFormRequest(ItemId channelId, ItemId messageId, ItemId formId);
    }
}
