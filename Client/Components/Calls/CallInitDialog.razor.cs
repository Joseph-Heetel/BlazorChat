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
using CustomBlazorApp.Client;
using CustomBlazorApp.Client.Shared;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using CustomBlazorApp.Client.Services;
using CustomBlazorApp.Shared;
using System.Diagnostics;

namespace CustomBlazorApp.Client.Components.Calls
{
    public partial class CallInitDialog
    {
        [CascadingParameter]
        public MudDialogInstance? MudDialog { get; set; }

        [Parameter]
        public User? RemoteUser { get; set; } = null;
        [Parameter]
        public PendingCall? PendingCall { get; set; } = null;

        private Device? __videoDevice;
        private Device? _videoDevice
        {
            get
            {
                return __videoDevice;
            }
            set
            {
                Calls.SetVideoDevice(value);
            }
        }
        private Device? __audioDevice;
        private Device? _audioDevice
        {
            get
            {
                return __audioDevice;
            }
            set
            {
                Calls.SetAudioDevice(value);
            }
        }

        private bool _devicesLoaded = false;
        private Device[] _videoInputs = Array.Empty<Device>();
        private Device[] _audioInputs = Array.Empty<Device>();

        private string _cancelString = "Cancel";
        private string _acceptString = "Call";
        private string _errorMessage = "";

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Calls.VideoDevice.StateChanged += VideoDevice_StateChanged;
            VideoDevice_StateChanged(Calls.VideoDevice.State);
            Calls.AudioDevice.StateChanged += AudioDevice_StateChanged;
            AudioDevice_StateChanged(Calls.AudioDevice.State);
        }
        private void AudioDevice_StateChanged(Device? value)
        {
            this.__audioDevice = value;
            this.StateHasChanged();
        }

        private void VideoDevice_StateChanged(Device? value)
        {
            this.__videoDevice = value;
            this.StateHasChanged();
        }


        protected override void OnParametersSet()
        {
            _cancelString = PendingCall == null ? "Cancel" : "Decline";
            _acceptString = PendingCall == null ? "Call" : "Accept";
            _ = Task.Run(async () =>
            {
                if (_videoInputs.Length == 0 && _audioInputs.Length == 0)
                {
                    var devices = await Calls.QueryDevices();
                    _videoInputs = devices.videoDevices;
                    _audioInputs = devices.audioDevices;
                    _videoDevice = _videoInputs.FirstOrDefault();
                    _audioDevice = _audioInputs.FirstOrDefault();
                    if (_videoInputs.Length == 0 && _audioInputs.Length == 0)
                    {
                        _errorMessage = "Failed to detect input devices!";
                    }
                    _devicesLoaded = true;
                    this.StateHasChanged();
                }
            });
        }

        private async Task onAccept()
        {
            if (MudDialog != null)
            {
                MudDialog.Close();
            }
            if (PendingCall == null)
            {
                if (RemoteUser != null)
                {
                    await Calls.InitiateCall(RemoteUser.Id, __videoDevice, __audioDevice);
                }
            }
            else
            {
                await Calls.AcceptCall(PendingCall, __videoDevice, __audioDevice);
            }
        }

        private async Task onCancel()
        {
            if (MudDialog != null)
            {
                MudDialog.Close();
            }
            if (PendingCall != null)
            {
                await Api.TerminateCall(PendingCall.Id);
            }
        }
    }
}