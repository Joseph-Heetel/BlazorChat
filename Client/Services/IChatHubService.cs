using BlazorChat.Shared;

namespace BlazorChat.Client.Services
{
    public interface IChatHubService
    {
        IReadOnlyObservable<bool> Connected { get; }

        /// <summary>
        /// <see cref="SignalRConstants.MESSAGE_INCOMING"/>
        /// </summary>
        public event Func<Message, Task>? OnMessageReceived;
        /// <summary>
        /// <see cref="SignalRConstants.MESSAGE_DELETED"/>
        /// </summary>
        public event Func<ItemId, ItemId, Task>? OnMessageDeleted;
        /// <summary>
        /// <see cref="SignalRConstants.CHANNEL_MESSAGESREAD"/>
        /// </summary>
        public event Func<ItemId, ItemId, long, Task>? OnMessageReadUpdate;
        /// <summary>
        /// <see cref="SignalRConstants.CHANNEL_LISTCHANGED"/>
        /// </summary>
        public event Func<Task>? OnChannellistChanged;
        /// <summary>
        /// <see cref="SignalRConstants.CHANNEL_UPDATED"/>
        /// </summary>
        public event Func<ItemId, Task>? OnChannelUpdated;
        /// <summary>
        /// <see cref="SignalRConstants.USER_UPDATED"/>
        /// </summary>
        public event Func<ItemId, Task>? OnUserUpdated;
        /// <summary>
        /// <see cref="SignalRConstants.CALLS_PENDINGCALLSLISTCHANGED"/>
        /// </summary>
        public event Func<Task>? OnPendingCallsListChanged;
        /// <summary>
        /// <see cref="SignalRConstants.CALL_NEGOTIATION"/>
        /// </summary>
        public event Func<ItemId, ItemId, NegotiationMessage, Task>? OnCallNegotiation;
        /// <summary>
        /// <see cref="SignalRConstants.CALL_TERMINATED"/>
        /// </summary>
        public event Func<ItemId, Task>? OnCallTerminated;
        /// <summary>
        /// <see cref="SignalRConstants.USER_PRESENCE"/>
        /// </summary>
        public event Func<ItemId, bool, Task>? OnUserPresence;
        /// <summary>
        /// <see cref="SignalRConstants.MESSAGE_UPDATED"/>
        /// </summary>
        public event Func<Message, Task>? OnMessageUpdated;

        /// <summary>
        /// Sends a P2P negotiation <paramref name="message"/> via the hub to <paramref name="recipientId"/>
        /// </summary>
        /// <returns>True when send succeeds</returns>
        public Task<bool> SendNegotiation(ItemId callId, ItemId recipientId, NegotiationMessage message);
    }
}
