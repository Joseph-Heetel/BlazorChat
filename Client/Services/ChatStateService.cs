using BlazorChat.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.AspNetCore.Components.Routing;
using System.Web;

namespace BlazorChat.Client.Services
{
    public sealed class ChatStateService : IChatStateService, IAsyncDisposable
    {
        /*
        Users
            _userCache maintains a mapping of user Ids to User objects across all loaded channels 
                (User objects maintain global per-user information)
        
            Users are loaded as these are discovered when loading channels or the hub notifies about the channel changing (indicating a possible new user)
            Per-channel specific user information is maintained in the channels' Participation structure
        
        Messages
            _currentChannel marks the currently loaded channel.
            _loadedMessageIds and _loadedMessagesSorted maintain loaded messages. 
        
            Only one channel has more than meta information loaded at one time.
            When the current channel is updated, message loading is automatically initiated.
            The UI can initiate loading of newer or older messages dynamically (based on scroll state of the main message display)

            When Highlighted Message is set, the state service will automatically switch channel if necessary, then load the message and surrounding messages.
            The UI is expected to scroll the highlighted message into view, and clear it once this succeeds.
         */

        private readonly IChatApiService _apiService;
        private readonly IChatHubService _hubService;
        private readonly ILocalCacheService _cacheService;
        private readonly NavigationManager _navManager;

        /// <summary>
        /// HashSet for efficient lookup of currently loaded messages
        /// </summary>
        private readonly HashSet<ItemId> _loadedMessagesIds = new HashSet<ItemId>();
        private readonly SortedList<long, Message> _loadedMessagesSorted = new SortedList<long, Message>();
        private Observable<IDictionary<ItemId, Channel>> _channelCache { get; } = new Observable<IDictionary<ItemId, Channel>>(new Dictionary<ItemId, Channel>());
        private Observable<Channel?> _currentChannel { get; } = new Observable<Channel?>(null);
        private Observable<IDictionary<ItemId, User>> _userCache { get; } = new Observable<IDictionary<ItemId, User>>(new Dictionary<ItemId, User>()
        {
            [new ItemId()] = new User() { Id = ItemId.SystemId, Name = "System", Online = true }
        });
        private Observable<IReadOnlyCollection<Message>> _loadedMessages { get; } = new Observable<IReadOnlyCollection<Message>>(Array.Empty<Message>());
        private Observable<ItemId> _highlightedMessageId { get; } = new Observable<ItemId>(default);
        private Observable<IReadOnlyCollection<PendingCall>> _pendingCalls { get; } = new Observable<IReadOnlyCollection<PendingCall>>(Array.Empty<PendingCall>());

        public IReadOnlyObservable<IDictionary<ItemId, Channel>> ChannelCache => _channelCache;
        public IReadOnlyObservable<Channel?> CurrentChannel => _currentChannel;
        public IReadOnlyObservable<IDictionary<ItemId, User>> UserCache => _userCache;
        public IReadOnlyObservable<IReadOnlyCollection<Message>> LoadedMessages => _loadedMessages;
        public IReadOnlyObservable<ItemId> HighlightedMessageId => _highlightedMessageId;
        public IReadOnlyObservable<IReadOnlyCollection<PendingCall>> PendingCalls => _pendingCalls;

        public ChatStateService(IChatApiService chatApi, IChatHubService chatHub, ILocalCacheService cache, NavigationManager nav)
        {
            this._apiService = chatApi;
            this._hubService = chatHub;
            this._cacheService = cache;
            this._navManager = nav;
            this._apiService.LoginState.StateChanged += LoginState_StateChanged;
            LoginState_StateChanged(_apiService.LoginState.State);
            this._hubService.OnMessageReceived += ChatHub_OnMessageReceived;
            this._hubService.OnMessageUpdated += ChatHub_OnMessageUpdated;
            this._hubService.OnMessageDeleted += ChatHub_OnMessageDeleted;
            this._hubService.OnMessageReadUpdate += ChatHub_OnMessageReadUpdate;
            this._hubService.OnChannellistChanged += ChatHub_OnChannellistChanged;
            this._hubService.OnUserUpdated += ChatHub_OnUserUpdated;
            this._hubService.OnChannelUpdated += ChatHub_OnChannelUpdated;
            this._hubService.OnUserPresence += ChatHub_OnUserPresence;
            this._hubService.OnPendingCallsListChanged += RefreshPendingCalls;
            this._hubService.OnCallTerminated += ChatHub_OnCallTerminated;
            this._navManager.LocationChanged += NavManager_LocationChanged;
        }

        private void LoginState_StateChanged(LoginState value)
        {
            if (value == LoginState.Connected)
            {
                // Do setup
                _ = Task.Run(() => discoverChannelsAndUsers(true));
            }
            else
            {
                // Do cleanup
                _ = Task.Run(Cleanup);
            }
        }

        #region Navbar Linking

        /// <summary>
        /// Updates query in nav Uri to include current channel id
        /// </summary>
        private void updateNavBarQuery()
        {
            Dictionary<string, object?> query = new Dictionary<string, object?>();
            query.Add("channel", _currentChannel.State?.Id.ToString());

            _navManager.NavigateTo(_navManager.GetUriWithQueryParameters(query), false);
        }

        private void NavManager_LocationChanged(object? sender, LocationChangedEventArgs e)
        {
            onNavLocationChanged(e.Location);
        }

        /// <summary>
        /// Checks query string for channel parameter. If it exists and is valid, sets it as current channel
        /// </summary>
        /// <param name="location"></param>
        private void onNavLocationChanged(string location)
        {
            int indexOfQuery = location.IndexOf('?');
            if (indexOfQuery >= 0)
            {
                var query = HttpUtility.ParseQueryString(location.Substring(indexOfQuery));
                string? channelParam = query.Get("channel");
                if (ItemId.TryParse(channelParam, out ItemId channelId))
                {
                    if (channelId != _currentChannel.State?.Id && _channelCache.State.ContainsKey(channelId))
                    {
                        _ = Task.Run(() => SetCurrentChannel(_channelCache.State[channelId], default));
                    }
                }
            }
        }

        #endregion
        #region Helpers

        private void resetUserCache()
        {
            this._userCache.State.Clear();
            this._userCache.State[ItemId.SystemId] = new User() { Id = ItemId.SystemId, Name = "System", Online = true };
            this._userCache.TriggerChange();
        }

        /// <summary>
        /// Oldest loaded message, if any
        /// </summary>
        private DateTimeOffset? _oldest
        {
            get
            {
                if (_loadedMessagesSorted.Count == 0)
                {
                    return null;
                }
                return _loadedMessagesSorted.Values[0].Created;
            }
        }

        /// <summary>
        /// Newest loaded message, if any
        /// </summary>
        private DateTimeOffset? _newest
        {
            get
            {
                if (_loadedMessagesSorted.Count == 0)
                {
                    return null;
                }
                return _loadedMessagesSorted.Values[_loadedMessagesSorted.Count - 1].Created;
            }
        }

        /// <summary>
        /// Helper method part of the initialization process. First reads cache (browser local storage, very fast). 
        /// Allows the user to select a channel etc.
        /// Because no delta information is stored, all this info needs to be pulled anyhow, so that is the next step.
        /// </summary>
        /// <returns></returns>
        private async Task discoverChannelsAndUsers(bool usecache)
        {
            resetUserCache();
            _channelCache.State.Clear();

            if (usecache)
            {
                // Get cached information for the moment
                await discoverCache();
            }

            // Request the real information from the api
            var apiresponse = await _apiService.GetChannels();
            if (apiresponse.TryGet(out var channels))
            {

                // Synchronise the cache with the real information
                await _cacheService.Maintain(channels);

                // Clear memory channel and user cache (that way we don't need to find the difference between cached items from browser local storage and reality)
                _channelCache.State.Clear();
                resetUserCache();

                // Collect users from channels that need to be fetched from the Api
                HashSet<ItemId> userIds = new HashSet<ItemId>();
                // Push real information to channel cache
                foreach (var channel in channels)
                {
                    _channelCache.State.Add(channel.Id, channel);
                    foreach (var participation in channel.Participants)
                    {
                        if (!_userCache.State.ContainsKey(participation.Id))
                        {
                            userIds.Add(participation.Id);
                        }
                    }
                }

                // Update UI with new state of channel cache
                _channelCache.TriggerChange();

                // Fetch users
                if (userIds.Count > 0)
                {
                    var userapiresponse = await _apiService.GetUsers(userIds);
                    if (userapiresponse.TryGet(out User[] users))
                    {
                        foreach (User user in users)
                        {
                            _userCache.State[user.Id] = user;
                        }
                    }
                }

                // Update UI with new state of user cache
                _userCache.TriggerChange();
            }

            // Current channel may have already contained a channel which is now an old invalid object, so replace if necessary
            if (_currentChannel.State != null)
            {
                _channelCache.State.TryGetValue(_currentChannel.State.Id, out Channel? channel);
                _currentChannel.TriggerChange(channel);
            }

            // If URI query included a channel setting we catch it and navigate to it
            onNavLocationChanged(_navManager.Uri);

            await RefreshPendingCalls();
        }

        /// <summary>
        /// Reads cached information from browser local storage
        /// </summary>
        /// <returns></returns>
        private async Task discoverCache()
        {
            var channels = await _cacheService.CachedChannels;
            foreach (var channel in channels)
            {
                _channelCache.State.Add(channel.Id, channel);
            }

            var users = await _cacheService.CachedUsers;
            foreach (var user in users)
            {
                _userCache.State[user.Id] = user;
            }
            _channelCache.TriggerChange();
            _userCache.TriggerChange();
        }

        #endregion
        #region Message Loading

        public bool HasMessageLoaded(ItemId id)
        {
            return _loadedMessagesIds.Contains(id);
        }

        public async Task CheckoutTimestamp(DateTimeOffset reference)
        {
            if (reference > _oldest && reference < _newest)
            {
                return;
            }
            else
            {
                List<Task<Message[]>> tasks = new List<Task<Message[]>>()
                {
                    loadNewerMessagesInternal(reference),
                    loadOlderMessagesInternal(reference)
                };
                var results = await Task.WhenAll(tasks);
                await integrateMessages(results.SelectMany(m => m).ToList(), reference >= _newest);
            }
        }

        /// <summary>
        /// Internal function to load newer messages relative to the <paramref name="reference"/> timestamp
        /// </summary>
        /// <param name="reference"></param>
        /// <returns></returns>
        private async Task<Message[]> loadNewerMessagesInternal(DateTimeOffset? reference)
        {
            Trace.Assert(_currentChannel.State != null);
            var apiresult = await _apiService.GetMessages(_currentChannel.State!.Id, reference ?? DateTimeOffset.UtcNow, false, ChatConstants.MESSAGE_FETCH_DEFAULT);

            if (apiresult.TryGet(out Message[] messages) && messages.Length > 0)
            {
                // Update read horizon if necessary
                Message newest = messages.MaxBy(x => x.CreatedTS)!;
                updateReadHorizonIfNecessary(_currentChannel.State, newest.Created);
                return messages;
            }
            return Array.Empty<Message>();
        }

        /// <summary>
        /// Internal function to load older messages relative to the <paramref name="reference"/> timestamp
        /// </summary>
        /// <param name="reference"></param>
        /// <returns></returns>
        private async Task<Message[]> loadOlderMessagesInternal(DateTimeOffset? reference)
        {
            Trace.Assert(_currentChannel.State != null);
            var apiresult = await _apiService.GetMessages(_currentChannel.State!.Id, reference ?? DateTimeOffset.UtcNow, true, ChatConstants.MESSAGE_FETCH_DEFAULT);

            if (apiresult.TryGet(out Message[] messages) && messages.Length > 0)
            {
                // Update read horizon if necessary
                Message newest = messages.MaxBy(x => x.CreatedTS)!;
                updateReadHorizonIfNecessary(_currentChannel.State, newest.Created);
                return messages;
            }
            return Array.Empty<Message>();
        }

        /// <summary>
        /// Update read horizon in channel, if this is required
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="readTimestamp"></param>
        private void updateReadHorizonIfNecessary(Channel channel, DateTimeOffset readTimestamp)
        {
            channel.HasUnread = false;
            Participation participation = channel.Participants.First(item => item.Id == _apiService.SelfUser.State!.Id);
            if (participation.LastRead < readTimestamp)
            {
                _ = Task.Run(async () =>
                {
                    _ = await _apiService.UpdateReadHorizon(channel.Id, readTimestamp);
                });
            }
        }

        private void clearMessages()
        {
            this._loadedMessagesIds.Clear();
            this._loadedMessagesSorted.Clear();
            this._loadedMessages.State = Array.Empty<Message>();
        }

        /// <summary>
        /// Integrates new messages and manages old messages as necessary
        /// </summary>
        /// <param name="messages">new messages</param>
        /// <param name="truncateOlder">if true, older messages are truncated if max message storage limit is exceeded. Otherwise, newer messages are removed instead.</param>
        private async Task integrateMessages(IReadOnlyCollection<Message> messages, bool truncateOlder, bool replaceOld = false)
        {
            bool alreadyHadMessages = this._loadedMessagesIds.Count > 0;

            // Include new messages
            foreach (var message in messages)
            {
                // Add or replace message

                if (this._loadedMessagesIds.Add(message.Id))
                {
                    this._loadedMessagesSorted.Add(message.CreatedTS, message);
                }
                else if (replaceOld)
                {
                    this._loadedMessagesSorted[message.CreatedTS] = message;
                }
            }

            // Remove messages as necessary
            int removed = 0;
            while (this._loadedMessagesIds.Count > ChatConstants.MESSAGE_STORE_MAX)
            {
                Message toclear;
                if (truncateOlder)
                {
                    toclear = this._loadedMessagesSorted.Values[0];
                }
                else
                {
                    toclear = this._loadedMessagesSorted.Values[this._loadedMessagesSorted.Count - 1];
                }
                this._loadedMessagesIds.Remove(toclear.Id);
                this._loadedMessagesSorted.Remove(toclear.CreatedTS);
                removed++;
            }

            // Trigger update to UI if any change occured
            if (messages.Count > 0 || removed > 0)
            {
                this._loadedMessages.TriggerChange(new List<Message>(this._loadedMessagesSorted.Values));
            }

            // If there were no previous messages and the highlighted message is not already set,
            // the UI is expected to scroll to the newest loaded message
            // (usually occurs when the channel has just been switched)
            if (messages.Count > 0 && !alreadyHadMessages && this._highlightedMessageId.State == default)
            {
                var lastmessage = this._loadedMessagesSorted.Values[this._loadedMessagesSorted.Count - 1];
                await this.SetHighlightedMessage(lastmessage.ChannelId, lastmessage.Id);
            }
        }

        public async Task<int> LoadNewerMessages(DateTimeOffset? reference)
        {
            reference = reference ?? _newest;
            var messages = await loadNewerMessagesInternal(reference);
            await integrateMessages(messages, true);
            return messages.Length;
        }

        public async Task<int> LoadOlderMessages(DateTimeOffset? reference)
        {
            reference = reference ?? _oldest;
            var messages = await loadOlderMessagesInternal(reference);
            await integrateMessages(messages, false);
            return messages.Length;
        }

        #endregion

        public async Task SetCurrentChannel(Channel? channel, DateTimeOffset? reference)
        {
            _currentChannel.State = channel;
            clearMessages();
            if (channel != null)
            {
                _ = Task.Run(() => this.CheckoutTimestamp(reference ?? DateTimeOffset.UtcNow));
                if (reference == null)
                {
                    var messages = await _cacheService.CachedMessages(channel.Id);
                    await integrateMessages(messages, true);
                }
            }
            updateNavBarQuery();
        }

        public Task ClearHighlightedMessage()
        {
            _highlightedMessageId.State = default;
            return Task.CompletedTask;
        }

        public async Task SetHighlightedMessage(ItemId channelId, ItemId messageId)
        {
            Message? message = null;
            bool switchChannel = true;

            if (CurrentChannel.State != null && CurrentChannel.State.Id == channelId)
            {
                // Requested channel is already loaded
                switchChannel = false;

                // Check if we already have the message stored
                message = _loadedMessagesSorted.Values.FirstOrDefault(item => item.Id == messageId);
            }

            if (message == null)
            {
                // Get message from api
                message = await _apiService.GetMessage(channelId, messageId);

                if (message == null)
                {
                    throw new ArgumentException("Cannot map to a valid message!", $"{nameof(channelId)} | {nameof(messageId)}");
                }
            }


            var timeref = message.Created;
            if (switchChannel)
            {
                // switch to the requested channel, load messages near the creation time
                await SetCurrentChannel(ChannelCache.State[channelId], timeref);
            }
            else
            {
                // load messages near the creation time
                await CheckoutTimestamp(timeref);
            }

            _highlightedMessageId.State = messageId;
        }

        #region Hub Connection

        private Task ChatHub_OnMessageReadUpdate(ItemId channelId, ItemId userId, long timestamp)
        {
            // find application, update timestamp, notify any channel watchers
            Channel channel = _channelCache.State[channelId];
            Participation? participation = channel.Participants.FirstOrDefault(item => item.Id == userId);
            if (participation != null)
            {
                participation.LastReadTS = timestamp;
                _channelCache.TriggerChange();
            }
            return Task.CompletedTask;
        }

        private async Task ChatHub_OnMessageReceived(Message message)
        {
            if (_currentChannel.State?.Id == message.ChannelId)
            {
                // channel is loaded, add it to the list and scroll to it
                await integrateMessages(new Message[] { message }, true);
                await SetHighlightedMessage(message.ChannelId, message.Id);
                if (message.AuthorId != _apiService.SelfUser.State!.Id)
                {
                    // Mark the message as read
                    _ = Task.Run(async () =>
                    {
                        await _apiService.UpdateReadHorizon(message.ChannelId, message.Created);
                    });
                }
            }
            else
            {
                // Notify channel cache watchers that there is a channel with unread messages
                _channelCache.State[message.ChannelId].HasUnread = true;
                _channelCache.TriggerChange();
            }
        }

        private Task ChatHub_OnMessageUpdated(Message arg)
        {
            if (_currentChannel.State?.Id == arg.ChannelId && _loadedMessagesIds.Contains(arg.Id))
            {
                // Message is loaded, so do replace the previous version of it
                return integrateMessages(new Message[] { arg }, true, true);
            }
            return Task.CompletedTask;
        }

        private Task ChatHub_OnMessageDeleted(ItemId channelId, ItemId messageId)
        {
            if (_currentChannel.State?.Id == channelId & _loadedMessagesIds.Contains(messageId))
            {
                Message? message = _loadedMessagesSorted.Values.FirstOrDefault(msg => msg.Id == messageId);
                if (message != null)
                {
                    // Message was indeed loaded, remove it from the list
                    _loadedMessagesSorted.Remove(message.CreatedTS);
                    _loadedMessagesIds.Remove(messageId);
                    _loadedMessages.TriggerChange(new List<Message>(_loadedMessagesSorted.Values));
                }
            }
            return Task.CompletedTask;
        }

        private Task ChatHub_OnChannellistChanged()
        {
            return discoverChannelsAndUsers(false);
        }

        private async Task ChatHub_OnChannelUpdated(ItemId channelId)
        {
            var response = await _apiService.GetChannel(channelId);
            if (response.TryGet(out Channel channel))
            {
                // Replace users and channel meta data
                List<ItemId> userIds = new List<ItemId>(channel.Participants.Select(p => p.Id));
                User[]? users = await _apiService.GetUsers(userIds);
                if (users != null && users.Length > 0)
                {
                    foreach (User user in users)
                    {
                        _userCache.State[user.Id] = user;
                    }
                    _userCache.TriggerChange();
                }
                this._channelCache.State[channelId] = channel;
                this._channelCache.TriggerChange();
            }
        }
        private async Task ChatHub_OnUserUpdated(ItemId userId)
        {
            var apiresponse = await _apiService.GetUser(userId);
            if (apiresponse.TryGet(out User user))
            {
                // replace the user
                _userCache.State[user.Id] = user;
                _userCache.TriggerChange();
            }
        }
        private Task ChatHub_OnUserPresence(ItemId userId, bool online)
        {
            if (this._userCache.State.TryGetValue(userId, out User? user))
            {
                if (user.Online != online)
                {
                    user.Online = online;
                    this._userCache.TriggerChange();
                }
            }
            return Task.CompletedTask;
        }

        private Task ChatHub_OnCallTerminated(ItemId arg)
        {
            return RefreshPendingCalls();
        }

        private async Task RefreshPendingCalls()
        {
            var apiresponse = await _apiService.GetCalls();
            if (apiresponse.TryGet(out PendingCall[] calls))
            {
                this._pendingCalls.State = calls;
            }
        }


        #endregion


        private Task Cleanup()
        {
            this._currentChannel.TriggerChange(null);
            this._loadedMessagesIds.Clear();
            this._loadedMessagesSorted.Clear();
            this._loadedMessages.TriggerChange(Array.Empty<Message>());
            resetUserCache();
            this._channelCache.State.Clear();
            this._channelCache.TriggerChange();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            this._apiService.LoginState.StateChanged -= LoginState_StateChanged;
            this._hubService.OnMessageReceived -= ChatHub_OnMessageReceived;
            this._hubService.OnMessageUpdated -= ChatHub_OnMessageUpdated;
            this._hubService.OnMessageDeleted -= ChatHub_OnMessageDeleted;
            this._hubService.OnMessageReadUpdate -= ChatHub_OnMessageReadUpdate;
            this._hubService.OnChannellistChanged -= ChatHub_OnChannellistChanged;
            this._hubService.OnUserUpdated -= ChatHub_OnUserUpdated;
            this._hubService.OnChannelUpdated -= ChatHub_OnChannelUpdated;
            this._hubService.OnUserPresence -= ChatHub_OnUserPresence;
            this._hubService.OnPendingCallsListChanged -= RefreshPendingCalls;
            this._hubService.OnCallTerminated -= ChatHub_OnCallTerminated;
            this._navManager.LocationChanged -= NavManager_LocationChanged;
            return ValueTask.CompletedTask;
        }

        public async Task TranslateMessage(ItemId channelId, ItemId messageId)
        {
            Message? message = await _apiService.GetMessageTranslated(channelId, messageId);
            if (message != null && _loadedMessagesIds.Contains(messageId))
            {
                await integrateMessages(new Message[] { message }, true, true);
            }
        }
    }
}
