using BlazorChat.Shared;

namespace BlazorChat.Client.Services
{
    /// <summary>
    /// Service exposing chat state access. Manages state of Channel/User collections, current channel and Message Collections and Hub connection
    /// </summary>
    public interface IChatStateService
    {
        /// <summary>
        /// Collection of channels visible to the user
        /// </summary>
        public IReadOnlyObservable<IDictionary<ItemId, Channel>> ChannelCache { get; }
        /// <summary>
        /// Messages of currently viewed channel
        /// </summary>
        public IReadOnlyObservable<IReadOnlyCollection<Message>> LoadedMessages { get; }
        /// <summary>
        /// Cache mapping UserIds to respective User information objects
        /// </summary>
        public IReadOnlyObservable<IDictionary<ItemId, User>> UserCache { get; }
        /// <summary>
        /// Id of the highlighted message.
        /// </summary>
        public IReadOnlyObservable<ItemId> HighlightedMessageId { get; }

        /// <summary>
        /// State of currently viewed channel
        /// </summary>
        public IReadOnlyObservable<Channel?> CurrentChannel { get; }
        /// <summary>
        /// List of pending calls, if any
        /// </summary>
        public IReadOnlyObservable<IReadOnlyCollection<PendingCall>> PendingCalls { get; }

        /// <summary>
        /// Updates <see cref="CurrentChannel"/> and initiates loading of messages
        /// </summary>
        /// <param name="channel">Channel or null</param>
        /// <param name="reference">Optional reference timestamp for message loading. <see cref="DateTimeOffset.Now"/> if omitted.</param>
        public Task SetCurrentChannel(Channel? channel, DateTimeOffset? reference = null);
        /// <summary>
        /// Loads newer messages
        /// </summary>
        /// <param name="reference">Optional reference timestamp for message loading. <see cref="DateTimeOffset.Now"/> or time of newest message if omitted.</param>
        /// <returns>Number of new messages loaded</returns>
        public Task<int> LoadNewerMessages(DateTimeOffset? reference = null);
        /// <summary>
        /// Loads older messages
        /// </summary>
        /// <param name="reference">Optional reference timestamp for message loading. <see cref="DateTimeOffset.Now"/> or time of oldest message if omitted.</param>
        /// <returns>Number of new messages loaded</returns>
        public Task<int> LoadOlderMessages(DateTimeOffset? reference = null);
        /// <summary>
        /// Loads messages surrounding <paramref name="reference"/> timestamp
        /// </summary>
        public Task CheckoutTimestamp(DateTimeOffset reference);
        /// <summary>
        /// Checks if message with id <paramref name="id"/> is loaded
        /// </summary>
        /// <returns>True if is loaded, false otherwise.</returns>
        bool HasMessageLoaded(ItemId id);
        /// <summary>
        /// Sets the highlighted message. Will cause the <see cref="CurrentChannel"/> to be set to <paramref name="channelId"/> and all messages surrounding <paramref name="messageId"/> to be loaded
        /// </summary>
        Task SetHighlightedMessage(ItemId channelId, ItemId messageId);
        /// <summary>
        /// Clears the highlighted message
        /// </summary>
        /// <returns></returns>
        Task ClearHighlightedMessage();
        Task TranslateMessage(ItemId channelId, ItemId messageId);
    }
}
