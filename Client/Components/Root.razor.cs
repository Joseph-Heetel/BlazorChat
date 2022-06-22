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
using System.Security.Cryptography;
using System.Text;
using MudBlazor;

namespace BlazorChat.Client.Components
{
    public sealed partial class Root : IAsyncDisposable
    {
        private string _login { get; set; } = string.Empty;
        private string _password { get; set; } = string.Empty;
        private string _username { get; set; } = string.Empty;
        private Services.LoginState _state;
        private bool _registerInsteadOfLogin = false;

        private bool _callInProgress;

#if ENABLE_ENDUSER_SELFREGISTER
        private const bool enableEnduserSelfregister = true;
#else
        private const bool enableEnduserSelfregister = false;
#endif

        protected override Task OnParametersSetAsync()
        {
            ChatApiService.LoginState.StateChanged += State_StateChanged;
            CallService.Status.StateChanged += Status_StateChanged;
            return Task.CompletedTask;
        }

        private void Status_StateChanged(Services.ECallState value)
        {
            this._callInProgress = value != Services.ECallState.None;
            if (_callInProgress)
            {
                // TODO close all dialogs
            }
            this.StateHasChanged();
        }

        private void State_StateChanged(Services.LoginState value)
        {
            _state = value;
            this.StateHasChanged();
        }

        private async Task onSubmit()
        {
            bool success = false;
            string errorMessage = "";
            if (_registerInsteadOfLogin)
            {
                errorMessage = "Register failed";
                success = await ChatApiService.Register(_login, _password, string.IsNullOrWhiteSpace(_username) ? _login : _username);
            }
            else
            {
                errorMessage = "Login failed. Login or password may be incorrect!";
                success = await ChatApiService.Login(_login, _password);
            }

            if (!success)
            {
                Snackbar.Add(errorMessage, Severity.Error);
            }
        }

        public ValueTask DisposeAsync()
        {
            ChatApiService.LoginState.StateChanged -= State_StateChanged;
            return ValueTask.CompletedTask;
        }
    }
}