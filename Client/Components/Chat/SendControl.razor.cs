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
using Microsoft.AspNetCore.Components.Authorization;
using BlazorChat.Shared;
using System.Text.Json;
using System.Diagnostics;
using MudBlazor;
using BlazorChat.Client.Services;

namespace BlazorChat.Client.Components.Chat
{
    public partial class SendControl : IDisposable
    {
        [Parameter]
        public SendControlParams Params { get; set; }

        private IBrowserFile? _file;
        private string _newMessageBody = string.Empty;

        private int _queuedMessagesCount = 0;

        enum SendControlState
        {
            Disabled,
            Sending,
            Ready
        }

        private SendControlState _state = SendControlState.Disabled;

        protected override void OnInitialized()
        {
            _messageDispatcher.Count.StateChanged += QueuedMessagesCount_StateChanged;
            QueuedMessagesCount_StateChanged(_messageDispatcher.Count.State);
        }

        private void QueuedMessagesCount_StateChanged(int value)
        {
            _queuedMessagesCount = value;
            StateHasChanged();
        }

        protected override void OnParametersSet()
        {
            if (_state != SendControlState.Sending)
            {
                _state = Params.CurrentChannelId.IsZero ? SendControlState.Disabled : SendControlState.Ready;
            }
        }

        private async Task onSubmit()
        {
            if (_state != SendControlState.Ready)
            {
                return;
            }
            if (_file == null && string.IsNullOrWhiteSpace(_newMessageBody))
            {
                return;
            }
            Debug.Assert(!Params.CurrentChannelId.IsZero);
            _state = SendControlState.Sending;
            this.StateHasChanged();
            IBrowserFile? file = _file;
            string? body = _newMessageBody;
            ItemId channelId = Params.CurrentChannelId;
            _file = null;
            _newMessageBody = string.Empty;
            _state = Params.CurrentChannelId.IsZero ? SendControlState.Disabled : SendControlState.Ready;
            _ = Task.Run(async () =>
            {
                var dispatchState = _messageDispatcher.Postmessage(channelId, body, file);
                var state = await dispatchState.Task;
                if (state == EMessageDispatchState.Failure)
                {
                    _Snackbar.Add("Failure", Severity.Error, options =>
                    {
                        options.VisibleStateDuration = 5000;
                    });
                }
            });
            this.StateHasChanged();
        }

        private void onKeyPress(KeyboardEventArgs e)
        {
            if (e.Key == "Enter" && !e.ShiftKey)
            {
                _ = Task.Run(onSubmit);
            }
        }

        private void onInputFileChanged(InputFileChangeEventArgs e)
        {
#pragma warning disable CA1826 // Do not use Enumerable methods on indexable collections
            _file = e.GetMultipleFiles().FirstOrDefault();
#pragma warning restore CA1826 // Do not use Enumerable methods on indexable collections
            if (_file != null)
            {
                bool valid = true;
                if (_file.Size > ChatConstants.MAX_FILE_SIZE)
                {
                    string message = string.Format(Loc["send_file_toobig"], FileHelper.MakeHumanReadableFileSize(ChatConstants.MAX_FILE_SIZE));
                    _Snackbar.Add(message, Severity.Error);
                    valid = false;
                }
                if (!FileHelper.IsValidMimeType(_file.ContentType))
                {
                    string message = string.Format(Loc["send_file_invalidtype"], _file.ContentType);
                    _Snackbar.Add(message, Severity.Error);
                    valid = false;
                }
                if (!valid)
                {
                    _file = null;
                }
            }
            this.StateHasChanged();
        }

        private string getFileInfo()
        {
            Debug.Assert(_file != null);

            string fileInfo = $"{_file.Name} ({FileHelper.MakeHumanReadableFileSize(_file.Size)})";

            return fileInfo;
        }

        private void clearFile()
        {
            _file = null;
            this.StateHasChanged();
        }

        public void Dispose()
        {
            _messageDispatcher.Count.StateChanged -= QueuedMessagesCount_StateChanged;
        }
    }
}