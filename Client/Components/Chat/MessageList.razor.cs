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
using BlazorChat.Client.Shared;
using BlazorChat.Shared;

namespace BlazorChat.Client.Components.Chat
{
    public sealed partial class MessageList : IAsyncDisposable
    {
        [Parameter]
        public MessageListParams Params { get; set; } = default;
        [Parameter]
        public Func<Task>? OnTopVisible { get; set; }
        [Parameter]
        public Func<Task>? OnBottomVisible { get; set; }
        private List<MessageViewParams> _messages { get; } = new List<MessageViewParams>();
        private DotNetObjectReference<MessageList>? _objectReference;
        private string _observerInteropId = string.Empty;
        private string? _scrollTarget = string.Empty;

        protected override void OnInitialized()
        {
            ChatState.HighlightedMessageId.StateChanged += HighlightedMessageId_StateChanged;
            _messageDispatch.ActiveMessageDispatches.StateChanged += ActiveMessageDispatches_StateChanged;
        }

        private void ActiveMessageDispatches_StateChanged(IReadOnlyCollection<Services.IMessageDispatchState> value)
        {
            RefreshParameters();
            StateHasChanged();
        }

        private void HighlightedMessageId_StateChanged(ItemId value)
        {
            if (value != default)
            {
                _scrollTarget = value.ToString();
                StateHasChanged();
            }
            else
            {
                _scrollTarget = null;
            }
        }

        protected override void OnParametersSet()
        {
            RefreshParameters();
        }

        private void RefreshParameters()
        {
            // Construct the messageviewparam list including read state information
            _messages.Clear();
            _messages.Capacity = Params.Messages.Count + _messageDispatch.Count.State;

            // Keep track of prev. messages author and timestamp so we can hide if they're not necessary
            ItemId prevAuthorId = default;
            DateTimeOffset prevMessageTime = default;

            foreach (var message in Params.Messages)
            {
                // Check how many participants have read this message
                long messageTimestamp = message.CreatedTS;
                int readcount = 0;
                foreach (var participant in Params.Participants)
                {
                    if (participant.LastReadTS >= messageTimestamp)
                    {
                        readcount++;
                    }
                }

                bool authorChanged = message.AuthorId != prevAuthorId;
                bool longMessageDelay = (message.Created - prevMessageTime) > TimeSpan.FromMinutes(10);
                bool remoteAuthor = message.AuthorId != Params.SelfUserId;

                _messages.Add(new MessageViewParams()
                {
                    Message = message,
                    ReadCount = readcount,
                    SelfUserId = Params.SelfUserId,
                    TotalCount = Params.Participants.Count,
                    ShowAuthor = remoteAuthor && (authorChanged || longMessageDelay)
                });

                prevAuthorId = message.AuthorId;
                prevMessageTime = message.Created;
            }
            foreach (var message in _messageDispatch.AsPendingMessages())
            {
                _messages.Add(new MessageViewParams()
                {
                    Message = message,
                    ReadCount = 0,
                    SelfUserId = Params.SelfUserId,
                    TotalCount = Params.Participants.Count,
                    ShowAuthor = false
                });
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                // Initialize JS side
                _observerInteropId = Guid.NewGuid().ToString().Replace(".", "");
                _objectReference = DotNetObjectReference.Create(this);
                await JS.InvokeVoidAsync(
                    "makeNewInfiniteListHelper",
                    _observerInteropId,
                    "topMarker",
                    "bottomMarker",
                    _objectReference,
                    nameof(JS_TopVisible),
                    nameof(JS_BottomVisible));
            }
            if (!string.IsNullOrEmpty(_scrollTarget))
            {
                bool hasScrolled = await ScrollIntoView(_scrollTarget);
                if (hasScrolled)
                {
                    _scrollTarget = null;
                    await ChatState.ClearHighlightedMessage();
                }
            }
        }

        // Called by JS side whenever the top element has become visible
        [JSInvokable(nameof(JS_TopVisible))]
        public void JS_TopVisible()
        {
            _ = Task.Run(OnTopVisible.InvokeAsync);
        }

        // Called by JS side whenever the bottom element has become visible
        [JSInvokable(nameof(JS_BottomVisible))]
        public void JS_BottomVisible()
        {
            _ = Task.Run(OnBottomVisible.InvokeAsync);
        }

        /// <summary>
        /// Call to the JS side to scroll an element into view
        /// </summary>
        /// <param name="elId"></param>
        /// <returns></returns>
        public async Task<bool> ScrollIntoView(string elId)
        {
            return await JS.InvokeAsync<bool>(_observerInteropId + ".scrollIntoView", elId);
        }

        public async ValueTask DisposeAsync()
        {
            _objectReference?.Dispose();
            ChatState.HighlightedMessageId.StateChanged -= HighlightedMessageId_StateChanged;
            _messageDispatch.ActiveMessageDispatches.StateChanged -= ActiveMessageDispatches_StateChanged;
            if (!string.IsNullOrEmpty(_observerInteropId))
            {
                await JS.InvokeVoidAsync($"{_observerInteropId}.dispose");
            }
        }
    }
}