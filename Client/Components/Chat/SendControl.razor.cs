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

namespace BlazorChat.Client.Components.Chat
{
    public partial class SendControl
    {
        [Parameter]
        public SendControlParams Params { get; set; }

        private IBrowserFile? _file;
        private string _newMessageBody = string.Empty;

        enum SendControlState
        {
            Disabled,
            Sending,
            Ready
        }

        private SendControlState _state = SendControlState.Disabled;

        protected override void OnParametersSet()
        {
            if (_state != SendControlState.Sending)
            {
                _state = Params.CurrentChannelId.IsZero ? SendControlState.Disabled : SendControlState.Ready;
            }
        }

        private async Task onSubmit()
        {
            if (_file == null && string.IsNullOrWhiteSpace(_newMessageBody))
            {
                return;
            }
            Debug.Assert(!Params.CurrentChannelId.IsZero);
            _state = SendControlState.Sending;
            this.StateHasChanged();
            Message? result = null;
            FileAttachment? attachment = null;
            bool error = false;
            if (_file != null)
            {
                IBrowserFile file = _file;
                _file = null;
                attachment = await ChatApiService.UploadFile(Params.CurrentChannelId, file);
                if (attachment == null)
                {
                    _Snackbar.Add("Unable to upload file", Severity.Error);
                    error = true;
                }
                Console.WriteLine($"Upload complete: {JsonSerializer.Serialize(attachment)}");
            }
            if (!error)
            {
                string messageBody = _newMessageBody.Trim(' ', '\n');
                _newMessageBody = string.Empty;
                result = await ChatApiService.CreateMessage(Params.CurrentChannelId, messageBody, attachment);
                if (result == null)
                {
                    _Snackbar.Add("Unable to send message", Severity.Error);
                }
            }
            _state = Params.CurrentChannelId.IsZero ? SendControlState.Disabled : SendControlState.Ready;
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
            this.StateHasChanged();
        }

        private string getFileInfo()
        {
            Debug.Assert(_file != null);
            string fileSize = "";
            const int oneKiB = 1024;
            const int oneMiB = oneKiB * 1024;
            const int oneGiB = oneMiB * 1024;
            const long oneTiB = (long)oneGiB * 1024L;
            if (_file.Size >= oneKiB && _file.Size < oneMiB)
            {
                fileSize = $"{_file.Size / oneKiB} KiB";
            }
            else if (_file.Size >= oneMiB && _file.Size < oneGiB)
            {
                fileSize = $"{_file.Size / oneMiB} MiB";
            }
            else if (_file.Size >= oneGiB && _file.Size < oneTiB)
            {
                fileSize = $"{_file.Size / oneGiB} GiB";
            }
            else
            {
                fileSize = $"{_file.Size} B";
            }
            string fileInfo = $"{_file.Name} ({fileSize})";

            return fileInfo;
        }

        private void clearFile()
        {
            _file = null;
            this.StateHasChanged();
        }
    }
}