using BlazorChat.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;
using System.Text.Json;

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

    public class ChatHubService : IChatHubService, IAsyncDisposable
    {
        private readonly NavigationManager _navManager;
        private readonly IChatApiService _apiService;
        private HubConnection? _hubConnection;

        private readonly Observable<bool> _connected = new Observable<bool>(false);
        public IReadOnlyObservable<bool> Connected => _connected;
        public event Func<Message, Task>? OnMessageReceived;
        public event Func<Message, Task>? OnMessageUpdated;
        public event Func<ItemId, ItemId, Task>? OnMessageDeleted;
        public event Func<ItemId, ItemId, long, Task>? OnMessageReadUpdate;
        public event Func<Task>? OnChannellistChanged;
        public event Func<ItemId, Task>? OnChannelUpdated;
        public event Func<ItemId, Task>? OnUserUpdated;
        public event Func<ItemId, bool, Task>? OnUserPresence;
        public event Func<Task>? OnPendingCallsListChanged;
        public event Func<ItemId, ItemId, NegotiationMessage, Task>? OnCallNegotiation;
        public event Func<ItemId, Task>? OnCallTerminated;


        public ChatHubService(NavigationManager nav, IChatApiService api)
        {
            this._navManager = nav;
            this._apiService = api;
            api.LoginState.StateChanged += LoginState_StateChanged;
            LoginState_StateChanged(api.LoginState.State);
        }

        private void LoginState_StateChanged(LoginState value)
        {
            if (value == LoginState.Connected)
            {
                _ = Task.Run(initializeHub);
            }
            else if (_hubConnection != null)
            {
                HubConnection connection = _hubConnection;
                _hubConnection = null;
                _ = Task.Run(async () =>
                {
                    await connection.StopAsync();
                    await connection.DisposeAsync();
                });
            }
        }

        /// <summary>
        /// Steadily increasing retry delay, maxing out at 1 min
        /// </summary>
        private class RetryPolicy : IRetryPolicy
        {
            private static readonly TimeSpan[] retryDelays = new TimeSpan[]
            {
                TimeSpan.FromSeconds(0),
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(60)
            };

            public TimeSpan? NextRetryDelay(RetryContext retryContext)
            {
                long index = Math.Min(retryContext.PreviousRetryCount, (long)retryDelays.Length - 1);
                TimeSpan delay = retryDelays[index];
#if HUBDEBUGLOGGING
                Console.WriteLine($"Hub Connection Retry attempt #{retryContext.PreviousRetryCount} in {delay.TotalSeconds} seconds)");
#endif
                return delay;
            }
        }

        private async Task initializeHub()
        {
            if (_hubConnection != null)
            {
                return;
            }
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_navManager.BaseUri + "hubs/chat")
                .WithAutomaticReconnect(new RetryPolicy())
                .Build();

            _hubConnection.On<Message>(SignalRConstants.MESSAGE_INCOMING, (msg) => Hub_MessageIncoming(msg));
            _hubConnection.On<Message>(SignalRConstants.MESSAGE_UPDATED, (msg) => Hub_MessageUpdated(msg));
            _hubConnection.On<ItemId, ItemId>(SignalRConstants.MESSAGE_DELETED, (cid, mid) => Hub_MessageDeleted(cid, mid));
            _hubConnection.On(SignalRConstants.CHANNEL_LISTCHANGED, () => Hub_ChannelListChanged());
            _hubConnection.On<ItemId>(SignalRConstants.CHANNEL_UPDATED, (id) => Hub_ChannelUpdated(id));
            _hubConnection.On<ItemId>(SignalRConstants.USER_UPDATED, (id) => Hub_UserUpdated(id));
            _hubConnection.On<ItemId, bool>(SignalRConstants.USER_PRESENCE, (id, o) => Hub_UserPresence(id, o));
            _hubConnection.On<ItemId, ItemId, long>(SignalRConstants.CHANNEL_MESSAGESREAD, (cid, uid, ts) => Hub_MessagesRead(cid, uid, ts));
            _hubConnection.On(SignalRConstants.CALLS_PENDINGCALLSLISTCHANGED, () => Hub_PendingCallsListChanged());
            _hubConnection.On<ItemId, ItemId, NegotiationMessage>(SignalRConstants.CALL_NEGOTIATION, (cid, sid, msg) => Hub_CallsNegotiation(cid, sid, msg));
            _hubConnection.On<ItemId>(SignalRConstants.CALL_TERMINATED, (id) => Hub_CallTerminated(id));
            _hubConnection.On(SignalRConstants.CLIENTKEEPALIVE, () => Hub_RespondKeepAlive());

            _hubConnection.Reconnecting += HubConnection_Reconnecting;
            _hubConnection.Reconnected += HubConnection_Reconnected;
            _hubConnection.Closed += HubConnection_Closed;

            await _hubConnection.StartAsync();
            _connected.State = true;

        }

        private Task Hub_RespondKeepAlive()
        {
#if HUBDEBUGLOGGING
            Console.WriteLine($"{SignalRConstants.CLIENTKEEPALIVE}");
#endif
            return _hubConnection?.InvokeAsync(SignalRConstants.HUBKEEPALIVE) ?? Task.CompletedTask;
        }

        private Task Hub_CallTerminated(ItemId callId)
        {
#if HUBDEBUGLOGGING
            Console.WriteLine($"{SignalRConstants.CALL_TERMINATED} : {callId}");
#endif
            return OnCallTerminated?.InvokeAsync(callId) ?? Task.CompletedTask;
        }

        private Task Hub_CallsNegotiation(ItemId callId, ItemId senderId, NegotiationMessage msg)
        {
#if HUBDEBUGLOGGING
            Console.WriteLine($"{SignalRConstants.CALL_NEGOTIATION} : {callId}, {senderId}, {JsonSerializer.Serialize(msg)}");
#endif
            return OnCallNegotiation?.InvokeAsync(callId, senderId, msg) ?? Task.CompletedTask;
        }

        private Task Hub_PendingCallsListChanged()
        {
#if HUBDEBUGLOGGING
            Console.WriteLine($"{SignalRConstants.CALLS_PENDINGCALLSLISTCHANGED}");
#endif
            return OnPendingCallsListChanged?.InvokeAsync() ?? Task.CompletedTask;
        }

        private Task Hub_MessagesRead(ItemId channelId, ItemId userId, long timestamp)
        {
#if HUBDEBUGLOGGING
            Console.WriteLine($"{SignalRConstants.CHANNEL_MESSAGESREAD} : C {channelId} U {userId} T {DateTimeOffset.FromUnixTimeMilliseconds(timestamp)}");
#endif
            return OnMessageReadUpdate?.InvokeAsync(channelId, userId, timestamp) ?? Task.CompletedTask;
        }

        private Task Hub_MessageUpdated(Message message)
        {
#if HUBDEBUGLOGGING
            Console.WriteLine($"{SignalRConstants.MESSAGE_UPDATED} : {JsonSerializer.Serialize(message)}");
#endif
            return OnMessageUpdated?.InvokeAsync(message) ?? Task.CompletedTask;
        }

        private Task Hub_MessageIncoming(Message message)
        {
#if HUBDEBUGLOGGING
            Console.WriteLine($"{SignalRConstants.MESSAGE_INCOMING} : {JsonSerializer.Serialize(message)}");
#endif
            return OnMessageReceived?.InvokeAsync(message) ?? Task.CompletedTask;
        }

        private Task Hub_MessageDeleted(ItemId channelId, ItemId messageId)
        {
#if HUBDEBUGLOGGING
            Console.WriteLine($"{SignalRConstants.MESSAGE_DELETED} : {messageId}");
#endif
            return OnMessageDeleted?.InvokeAsync(channelId, messageId) ?? Task.CompletedTask;
        }

        private Task Hub_ChannelListChanged()
        {
#if HUBDEBUGLOGGING
            Console.WriteLine(SignalRConstants.CHANNEL_LISTCHANGED);
#endif
            return OnChannellistChanged?.InvokeAsync() ?? Task.CompletedTask;
        }

        private Task Hub_ChannelUpdated(ItemId channelId)
        {
#if HUBDEBUGLOGGING
            Console.WriteLine($"{SignalRConstants.CHANNEL_UPDATED} : {channelId}");
#endif
            return OnChannelUpdated?.InvokeAsync(channelId) ?? Task.CompletedTask;
        }
        private Task Hub_UserUpdated(ItemId userId)
        {
#if HUBDEBUGLOGGING
            Console.WriteLine($"{SignalRConstants.USER_UPDATED} : {userId}");
#endif
            return OnUserUpdated?.InvokeAsync(userId) ?? Task.CompletedTask;
        }

        private Task Hub_UserPresence(ItemId userId, bool online)
        {
#if HUBDEBUGLOGGING
            Console.WriteLine($"{SignalRConstants.USER_PRESENCE} : {userId}, {online}");
#endif
            return OnUserPresence?.InvokeAsync(userId, online) ?? Task.CompletedTask;
        }

        private Task HubConnection_Closed(Exception? e)
        {
            Console.WriteLine($"Connection to Server closed: {(e == null ? "unknown error" : e.Message)}");
            _connected.State = false;
            return Task.CompletedTask;
        }

        private Task HubConnection_Reconnected(string? _)
        {
            _connected.State = true;
            return Task.CompletedTask;
        }

        private Task HubConnection_Reconnecting(Exception? e)
        {
            Console.WriteLine($"Connection to Server lost: {(e == null ? "unknown error" : e.Message)}");
            _connected.State = false;
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
            {
                await this._hubConnection.DisposeAsync();
                this._hubConnection = null;
            }
            _apiService.LoginState.StateChanged -= LoginState_StateChanged;
        }

        public Task<bool> SendNegotiation(ItemId callId, ItemId recipientId, NegotiationMessage message)
        {
            Trace.Assert(_hubConnection != null);
            return _hubConnection!.InvokeAsync<bool>(SignalRConstants.CALL_FORWARD_NEGOTIATION, callId, recipientId, message);
        }
    }
}
