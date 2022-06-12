using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.JSInterop;
using BlazorChat.Client;
using BlazorChat.Shared;
using System.Text.Json;
using MudBlazor;
using BlazorChat.Client.Components.Calls;

namespace BlazorChat.Client.Components.Chat
{
    public sealed partial class ChatRoot : IAsyncDisposable
    {
        public List<Channel> Channels { get; } = new List<Channel>();
        public Channel? CurrentChannel { get; private set; }
        public User? SelfUser { get; set; }

        private MessageListParams _messageListParams = default;
        private ChannelListParams _channelListParams = default;
        private SendControlParams _sendControlParams = default;
        private ParticipantListParams _userListParams = default;
        private ChannelInfoParams _channelInfoParams = default;

        private Snackbar? _connectionLostSnackbar { get; set; } = null;
        private Snackbar? _callIncomingSnackbar { get; set; } = null;

        #region Init

        protected override Task OnInitializedAsync()
        {
            { // Subscribe to events
                ChatApiService.SelfUser.StateChanged += SelfUser_StateChanged;
                SelfUser_StateChanged(ChatApiService.SelfUser.State);
                ChatStateService.ChannelCache.StateChanged += Channels_StateChanged;
                Channels_StateChanged(ChatStateService.ChannelCache.State);
                ChatStateService.CurrentChannel.StateChanged += CurrentChannel_StateChanged;
                CurrentChannel_StateChanged(ChatStateService.CurrentChannel.State);
                ChatStateService.LoadedMessages.StateChanged += LoadedMessages_StateChanged;
                LoadedMessages_StateChanged(ChatStateService.LoadedMessages.State);
                ChatStateService.UserCache.StateChanged += UserCache_StateChanged;
                UserCache_StateChanged(ChatStateService.UserCache.State);
                ChatStateService.PendingCalls.StateChanged += PendingCalls_StateChanged;
                PendingCalls_StateChanged(ChatStateService.PendingCalls.State);
                ChatHubService.Connected.StateChanged += HubConnected_StateChanged;
                HubConnected_StateChanged(ChatHubService.Connected.State);
                ChatHubService.OnMessageReceived += Hub_OnMessageReceived;
                ChatHubService.OnMessageReadUpdate += Hub_OnMessageReadUpdate;
            }
            { // Catch current state
                this.SelfUser = ChatApiService.SelfUser.State;
                this.Channels.AddRange(ChatStateService.ChannelCache.State.Values);
                this.CurrentChannel = ChatStateService.CurrentChannel.State;
            }
            updateChannelListParams();
            updateMessageListParams();
            return base.OnInitializedAsync();
        }

        #endregion
        #region Event Handlers

        private void PendingCalls_StateChanged(IReadOnlyCollection<PendingCall> value)
        {
            if (_callIncomingSnackbar != null)
            {
                _Snackbar.Remove(_callIncomingSnackbar);
                _callIncomingSnackbar?.Dispose();
                _callIncomingSnackbar = null;
            }
            if (value.Count > 0)
            {
                PendingCall call = value.First();
                if (ChatStateService.UserCache.State.TryGetValue(call.CallerId, out User? user))
                {
                    _callIncomingSnackbar = _Snackbar.Add($"{user.Name} is calling you!", Severity.Success, options =>
                    {
                        options.Onclick = (_) =>
                        {
                            if (ChatStateService.PendingCalls.State.Any(c => c.Id == call.Id))
                            {
                                DialogParameters parameters = new DialogParameters();
                                parameters.Add(nameof(CallInitDialog.PendingCall), call);
                                parameters.Add(nameof(CallInitDialog.RemoteUser), user);
                                var dialog = DialogService.Show<CallInitDialog>("", parameters);
                            }
                            return Task.CompletedTask;
                        };
                        options.Icon = Icons.Filled.Call;
                        options.VisibleStateDuration = int.MaxValue;
                    });
                }
            }
        }

        private void LoadedMessages_StateChanged(IReadOnlyCollection<Message> value)
        {
            updateMessageListParams();
        }

        private Task Hub_OnMessageReadUpdate(ItemId channelId, ItemId userId, long timestamp)
        {
            if (CurrentChannel?.Id == channelId)
            {
                updateMessageListParams();
            }
            return Task.CompletedTask;
        }

        private void CurrentChannel_StateChanged(Channel? value)
        {
            CurrentChannel = value;
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
            if (CurrentChannel != null)
            {
                _cts = new CancellationTokenSource();
                _canLoadNewer = true;
                _canLoadOlder = true;
                _loadOlderTask = null;
                _loadNewerTask = null;
                _ = Task.Run(async () =>
                {
                    await attemptLoadOlderMessages();
                }, _cts!.Token);
            }
            _sendControlParams = new SendControlParams()
            {
                CurrentChannelId = CurrentChannel?.Id ?? default
            };
            updateMessageListParams();
            updateChannelListParams();
            updateUserListParams();
            updateChannelInfoParams();
        }

        private Task Hub_OnMessageReceived(Message arg)
        {
            if (arg.ChannelId != CurrentChannel?.Id)
            {
                // Not currently watched channel, display snackbar notification
                displayNewMessageNotification(arg);
            }
            return Task.CompletedTask;
        }

        private void Channels_StateChanged(IDictionary<ItemId, Channel> value)
        {
            Channels.Clear();
            Channels.AddRange(value.Values);
            updateChannelListParams();
        }

        private void UserCache_StateChanged(IDictionary<ItemId, User> value)
        {
            updateUserListParams();
        }

        private void SelfUser_StateChanged(BlazorChat.Shared.User? value)
        {
            this.SelfUser = value;
            updateMessageListParams();
        }

        private void HubConnected_StateChanged(bool value)
        {
            if (value)
            {
                if (_connectionLostSnackbar != null)
                {
                    _Snackbar.Remove(_connectionLostSnackbar);
                    _connectionLostSnackbar.Dispose();
                    _connectionLostSnackbar = null;
                }
            }
            else
            {
                _connectionLostSnackbar = this._Snackbar.Add("Connection to server lost. The application will automatically reconnect as soon as possible.", Severity.Warning, (config) =>
                {
                    config.CloseAfterNavigation = false;
                    config.ShowCloseIcon = false;
                    config.VisibleStateDuration = int.MaxValue;
                });
            }
        }

        private void ChannelList_OnChannelClicked(ItemId channelId)
        {
            var channel = Channels.Find(c => c.Id == channelId);
            ChatStateService.SetCurrentChannel(channel);
        }

        #endregion
        #region UI Control

        private void displayNewMessageNotification(Message message)
        {
            string channelName = Channels.FirstOrDefault(c => c.Id == message.ChannelId)?.Name ?? $"[{message.ChannelId}]";
            string authorName = $"[{message.AuthorId}]";
            if (ChatStateService.UserCache.State.TryGetValue(message.AuthorId, out User? author))
            {
                authorName = author.Name;
            }
            string body = string.Empty;
            if (message.HasAttachment())
            {
                body = message.Attachment!.FileName();
            }
            else
            {
                body = message.Body ?? "";
                if (body.Length > 50)
                {
                    body = body.Substring(0, 50);
                }
            }
            string snack = $"{channelName} - {authorName} > {body}";
            var snackbar = _Snackbar.Add(snack, Severity.Info, c =>
            {
                c.Icon = Icons.Filled.Message;
                c.Onclick = (_) =>
                {
                    return ChatStateService.SetHighlightedMessage(message.ChannelId, message.Id);
                    //ChatStateService.SetCurrentChannel(Channels.FirstOrDefault(c => c.Id == message.ChannelId));
                    //return Task.CompletedTask;
                };
            });
        }


        private void openSearchDialog()
        {
            DialogParameters parameters = new DialogParameters();
            var dialog = DialogService.Show<SearchDialog>("", parameters);
        }

        #endregion
        #region Parameter Updates

        private void updateUserListParams()
        {
            _userListParams = new ParticipantListParams()
            {
                Participants = CurrentChannel?.Participants,
                Channel = CurrentChannel
            };
            this.StateHasChanged();
        }

        private void updateChannelInfoParams()
        {
            _channelInfoParams = new ChannelInfoParams()
            {
                ChannelId = CurrentChannel?.Id ?? default,
                Channel = CurrentChannel
            };
        }

        private void updateMessageListParams()
        {
            if (CurrentChannel == null)
            {
                _messageListParams = new MessageListParams()
                {
                    Messages = Array.Empty<Message>(),
                    Participants = Array.Empty<Participation>(),
                    SelfUserId = SelfUser?.Id ?? default
                };
            }
            else
            {
                _messageListParams = new MessageListParams()
                {
                    Messages = ChatStateService.LoadedMessages.State,
                    Participants = CurrentChannel.Participants,
                    SelfUserId = SelfUser?.Id ?? default
                };
            }
            this.StateHasChanged();
        }

        private void updateChannelListParams()
        {
            List<ChannelListParamEntry> entries = new List<ChannelListParamEntry>(
                Channels.Select(c => new ChannelListParamEntry()
                {
                    Id = c.Id,
                    Name = c.Name,
                    HasUnread = c.HasUnread
                }
                ));
            _channelListParams = new ChannelListParams()
            {
                Channels = entries,
                CurrentChannelId = CurrentChannel?.Id ?? default
            };
            this.StateHasChanged();
        }

        #endregion
        #region Infinite List

        private CancellationTokenSource? _cts = new CancellationTokenSource();
        private Task? _loadOlderTask;
        private Task? _loadNewerTask;
        private bool _canLoadOlder = true;
        private bool _canLoadNewer = true;

        private Task attemptLoadOlderMessages()
        {
            if (CurrentChannel != null)
            {
                bool initiateLoad = _canLoadOlder && (_loadOlderTask == null || _loadOlderTask.IsCompleted);
                if (initiateLoad)
                {
                    _loadOlderTask = Task.Run(async () =>
                    {
                        try
                        {
                            _canLoadOlder = await ChatStateService.LoadOlderMessages() > 0;
                            _loadOlderTask = null;
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine(ex);
                        }
                    }, _cts!.Token);

                }
            }
            return Task.CompletedTask;
        }

        private Task attemptLoadNewerMessages()
        {
            if (CurrentChannel != null)
            {
                bool initiateLoad = _canLoadNewer && (_loadNewerTask == null || _loadNewerTask.IsCompleted);
                if (initiateLoad)
                {
                    _loadNewerTask = Task.Run(async () =>
                    {
                        try
                        {
                            _canLoadNewer = await ChatStateService.LoadNewerMessages() > 0;
                            _loadNewerTask = null;
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine(ex);
                        }

                    }, _cts!.Token);
                }
            }
            return Task.CompletedTask;
        }

        #endregion

        public ValueTask DisposeAsync()
        {
            ChatApiService.SelfUser.StateChanged -= SelfUser_StateChanged;
            ChatStateService.ChannelCache.StateChanged -= Channels_StateChanged;
            ChatStateService.CurrentChannel.StateChanged -= CurrentChannel_StateChanged;
            ChatStateService.LoadedMessages.StateChanged -= LoadedMessages_StateChanged;
            ChatStateService.UserCache.StateChanged -= UserCache_StateChanged;
            ChatStateService.PendingCalls.StateChanged -= PendingCalls_StateChanged;
            ChatHubService.OnMessageReceived -= Hub_OnMessageReceived;
            ChatHubService.OnMessageReadUpdate -= Hub_OnMessageReadUpdate;
            ChatHubService.Connected.StateChanged -= HubConnected_StateChanged;
            _cts?.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}