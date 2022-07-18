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
using MudBlazor;
using System.ComponentModel;
using System.Diagnostics;
using BlazorChat.Shared;
using System.Text.Json;
using BlazorChat.Client.Services;

namespace BlazorChat.Client.Components.Calls
{
    public partial class CallRoot : IDisposable
    {
        private ECallState _state;

        private UserViewParams _remoteUser;
        private UserViewParams _localUser;


        private TransmitState? _remoteTransmitState;
        private bool _multipleVideos => _remoteTransmitState != null && _remoteTransmitState.Camera && _remoteTransmitState.Capture;

        private bool _showControls = false;

        void toggleVideoControls()
        {
            _showControls = !_showControls;
            this.StateHasChanged();
        }
        protected override void OnInitialized()
        {
            Calls.Status.StateChanged += Status_StateChanged;
            Status_StateChanged(Calls.Status.State);
            Calls.RemotePeerId.StateChanged += RemotePeerId_StateChanged;
            RemotePeerId_StateChanged(Calls.RemotePeerId.State);
            Calls.RemoteTransmitState.StateChanged += RemoteTransmitState_StateChanged;
            RemoteTransmitState_StateChanged(Calls.RemoteTransmitState.State);
            Calls.OnIceConnectFailed += Calls_OnIceConnectFailed;
            Api.SelfUser.StateChanged += SelfUser_StateChanged;
            SelfUser_StateChanged(Api.SelfUser.State);
            base.OnInitialized();
        }

        private void Calls_OnIceConnectFailed()
        {
            _snackbar.Add(Loc["call_icefailed"], Severity.Error, options => options.VisibleStateDuration = 5000);
        }

        private void RemoteTransmitState_StateChanged(TransmitState? value)
        {
            _remoteTransmitState = value;
            this.StateHasChanged();
        }

        private void RemotePeerId_StateChanged(ItemId value)
        {
            State.UserCache.State.TryGetValue(value, out User? user);
            this._remoteUser = new UserViewParams()
            {
                UserId = user?.Id ?? default,
                User = user,
                SelfUserId = default,
                DisplayOnlineState = false
            };
            this.StateHasChanged();
        }

        private void SelfUser_StateChanged(User? value)
        {
            this._localUser = new UserViewParams()
            {
                UserId = value?.Id ?? default,
                User = value,
                SelfUserId = value?.Id ?? default,
                DisplayOnlineState = false
            };
            this.StateHasChanged();
        }

        private void Status_StateChanged(Services.ECallState value)
        {
            this._state = value;
            this.StateHasChanged();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            List<Task> tasks = new List<Task>();
            if (_videoElementsSwapped && _multipleVideos)
            {
                tasks.Add(Calls.SetVideoElements("LocalVideo", "RemoteAudio", "RemoteVideoPreview", "RemoteVideoLarge"));
            }
            else
            {
                tasks.Add(Calls.SetVideoElements("LocalVideo", "RemoteAudio", "RemoteVideoLarge", "RemoteVideoPreview"));
            }
            await Task.WhenAll(tasks);
        }

        bool _videoElementsSwapped = false;

        private async Task swapVideoElements()
        {
            _videoElementsSwapped = !_videoElementsSwapped;
            if (_videoElementsSwapped)
            {
                await Calls.SetVideoElements("LocalVideo", "RemoteAudio", "RemoteVideoPreview", "RemoteVideoLarge");
            }
            else
            {
                await Calls.SetVideoElements("LocalVideo", "RemoteAudio", "RemoteVideoLarge", "RemoteVideoPreview");
            }
        }

        private async Task hangup()
        {
            await Calls.TerminateCall();
        }

        public void Dispose()
        {
            Calls.Status.StateChanged -= Status_StateChanged;
            Calls.RemotePeerId.StateChanged -= RemotePeerId_StateChanged;
            Calls.RemoteTransmitState.StateChanged -= RemoteTransmitState_StateChanged;
            Calls.OnIceConnectFailed -= Calls_OnIceConnectFailed;
            Api.SelfUser.StateChanged -= SelfUser_StateChanged;
        }
    }
}