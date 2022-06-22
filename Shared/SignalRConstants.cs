using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazorChat.Shared
{
    /// <summary>
    /// Constants for use of SignalR (Event/Method names)
    /// </summary>
    public static class SignalRConstants
    {
        /// <summary>
        /// The client receives a message sent to a group they are a part of
        /// </summary>
        /// <remarks>parameters: (ChatMessage message)</remarks>
        public const string MESSAGE_INCOMING = "Chat.Message.Incoming";
        /// <summary>
        /// The clients are notified that a message was deleted in a channel they are present in
        /// </summary>
        /// <remarks>parameters: (ItemId channelId, ItemId messageId)</remarks>
        public const string MESSAGE_DELETED = "Chat.Message.Deleted";
        /// <summary>
        /// The clients are notified that a message was modified in a channel they are present in
        /// </summary>
        /// <remarks>parameters: (ChatMessage message)</remarks>
        public const string MESSAGE_UPDATED = "Chat.Message.Updated";
        /// <summary>
        /// The client is notified that the list of channels they have access to has changed
        /// </summary>
        /// <remarks>parameters: ()</remarks>
        public const string CHANNEL_LISTCHANGED = "Chat.Channel.ListChanged";
        /// <summary>
        /// The client is notified that one of the channels they are in has changed (A user added or removed or Name changed)
        /// </summary>
        /// <remarks>parameters: (ItemId channelId)</remarks>
        public const string CHANNEL_UPDATED = "Chat.Channel.ItemChanged";
        /// <summary>
        /// The client is notified that a users name was changed
        /// </summary>
        /// <remarks>parameters: (ItemId userId)</remarks>
        public const string USER_UPDATED = "Chat.User.ItemChanged";
        /// <summary>
        /// The client is notified that a user has changed their presence state
        /// </summary>
        /// <remarks>parameters: (ItemId userId, bool online)</remarks>
        public const string USER_PRESENCE = "Chat.User.Presence";
        /// <summary>
        /// The client is notified that a user has updated their read horizon
        /// </summary>
        /// <remarks>parameters: (ChannelId channelId, UserId userId, long timestamp)</remarks>
        public const string CHANNEL_MESSAGESREAD = "Chat.Channel.MessagesRead";
        /// <summary>
        /// The client is notified that the list of pending calls has changed
        /// </summary>
        /// <remarks>parameters: ()</remarks>
        public const string CALLS_PENDINGCALLSLISTCHANGED = "Call.Pending.ListChanged";
        /// <summary>
        /// The client is notified that another client has initiated a call
        /// </summary>
        /// <remarks>parameters: (ItemId callId, ItemId senderId, NegotiationMessage msg)</remarks>
        public const string CALL_NEGOTIATION = "Call.Negotiation";
        /// <summary>
        /// The hub side method used to forward negotiation messages to another client
        /// </summary>
        /// <remarks>parameters: (ItemId callId, ItemId recipientId, NegotiationMessage message), returns: boolean (success)</remarks>
        public const string CALL_FORWARD_NEGOTIATION = "Call.ForwardNegotiation";
        /// <summary>
        /// The client is notified that a call has been terminated
        /// </summary>
        /// <remarks>parameters: (ItemId callId)</remarks>
        public const string CALL_TERMINATED = "Call.Terminated";
        public const string CLIENTKEEPALIVE = "Client.KeepAlive";
        public const string HUBKEEPALIVE = "Hub.KeepAlive";
    }
}
