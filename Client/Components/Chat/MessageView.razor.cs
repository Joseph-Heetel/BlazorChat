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
using BlazorChat.Client.Services;
using BlazorChat.Shared;
using System.Text.Json;
using BlazorChat.Client.Components.Forms;
using MudBlazor;

namespace BlazorChat.Client.Components.Chat
{
    public sealed partial class MessageView : IDisposable
    {
        [Parameter]
        public MessageViewParams Params { get; set; }

        [CascadingParameter]
        public IThemeInfo? ThemeInfo { get; set; }

        private bool _disposed = false;
        private string[] _text = Array.Empty<string>();
        private ItemId _attachmentId = default;
        private string _attachmentUrl = string.Empty;
        private bool _hasAttachment = false;
        private bool _attachmentIsImage = false;
        private string _timedisplay = string.Empty;
        private bool _alignRight = false;
        private bool _accent = false;
        private ItemId _formRequestId = default;
        private string _cardClass = "darkenedcard";
        private ItemId _channelId = default;

        private UserViewParams _authorViewParams;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                ChatStateService.UserCache.StateChanged -= UserCache_StateChanged;
                if (!_attachmentId.IsZero)
                {
                    MediaResolver.Unsubscribe(_attachmentId, onAttachmentUrlChanged);
                }
            }
        }

        protected override void OnInitialized()
        {
            ChatStateService.UserCache.StateChanged += UserCache_StateChanged;
        }

        private void IsDarkMode_StateChanged(bool value)
        {
            updateAccent();
            this.StateHasChanged();
        }

        protected override void OnParametersSet()
        {
            _authorViewParams = new UserViewParams()
            {
                UserId = Params.Message.AuthorId,
                SelfUserId = Params.SelfUserId
            };
            UserCache_StateChanged(ChatStateService.UserCache.State);

            _channelId = Params.Message.ChannelId;
            _hasAttachment = Params.Message.Attachment != null;
            if (_hasAttachment)
            {
                _attachmentId = Params.Message.Attachment!.Id;
                _attachmentUrl = MediaResolver.GetAndSubscribe(Params.Message.ChannelId, Params.Message.Attachment!, onAttachmentUrlChanged);
                _attachmentIsImage = Params.Message.Attachment!.IsImage;
            }

            _formRequestId = Params.Message.FormRequestId;

            _text = Params.Message.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            var sendDateDiff = DateTimeOffset.Now - Params.Message.Created;
            if (sendDateDiff > TimeSpan.FromHours(48))
            {
                // 48h+ ago, display date only
                this._timedisplay = Params.Message.Created.LocalDateTime.ToString("d");
            }
            else if (sendDateDiff > TimeSpan.FromHours(18))
            {
                // 18h+ ago, display date and time
                this._timedisplay = Params.Message.Created.LocalDateTime.ToString("g");
            }
            else
            {
                // display time only
                this._timedisplay = Params.Message.Created.LocalDateTime.ToString("t");
            }

            _alignRight = Params.Message.AuthorId == Params.SelfUserId;
            updateAccent();
        }

        private void onAttachmentUrlChanged(string value)
        {
            _attachmentUrl = value;
            this.StateHasChanged();
        }

        private void UserCache_StateChanged(IDictionary<ItemId, User> value)
        {
            bool dirty = false;
            if (_authorViewParams.User == null)
            {
                if (value.TryGetValue(Params.Message.AuthorId, out User? author))
                {
                    _authorViewParams = new UserViewParams()
                    {
                        User = author,
                        UserId = Params.Message.AuthorId,
                        SelfUserId = Params.SelfUserId,
                    };
                    dirty = true;
                }
            }
            if (dirty)
            {
                this.StateHasChanged();
            }
        }

        private void viewImage()
        {
            if (_attachmentUrl != null)
            {
                DialogParameters parameters = new DialogParameters()
                {
                    [nameof(ImageViewDialog.DomainId)] = _channelId,
                    [nameof(ImageViewDialog.Attachment)] = Params.Message.Attachment
                };
                DialogOptions options = new DialogOptions() { NoHeader = true };
                _dialogService.Show<ImageViewDialog>("", parameters, options);
            }
        }

        void updateAccent()
        {
            _accent = (!Params.SelfUserId.IsZero && Params.Message?.AuthorId == Params.SelfUserId);
            if (_accent)
            {
                _cardClass = (ThemeInfo?.IsDarkMode ?? false) ? "darkbackgroundaccent" : "lightbackgroundaccent";
            }
            else
            {
                _cardClass = "darkenedcard";
            }
        }

        void translate()
        {
            _ = Task.Run(() => ChatStateService.TranslateMessage(Params.Message.ChannelId, Params.Message.Id));
        }
    }
}