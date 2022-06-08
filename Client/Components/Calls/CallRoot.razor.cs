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
    public partial class CallRoot
    {
        private ECallState _state;

        private UserViewParams _remoteUser;
        private UserViewParams _localUser;

        private List<Device> _availableVideoDevices = new List<Device>();
        private Device? __videoDevice;
        private Device? _previouslyUsedVideoDevice;

        private string _localVideoStyle => $"max-width: calc(100% - 1rem); {(_videoDevice == null ? "display: none;" : "")}";
        private bool _canTransmitVideo => _availableVideoDevices.Count > 0;
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
        private List<Device> _availableAudioDevices = new List<Device>();
        private Device? __audioDevice;
        private Device? _previouslyUsedAudioDevice;
        private bool _canTransmitAudio => _availableAudioDevices.Count > 0;
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
        private string _audioDeviceButtonText => _audioDevice == null ? "Enable Microphone" : "Mute Microphone";
        private string _audioDeviceButtonIcon => _audioDevice == null ? Icons.Filled.Mic : Icons.Filled.MicOff;
        private Color _audioDeviceButtonIconColor => _audioDevice == null ? Color.Success : Color.Error;
        private string _videoDeviceButtonText => _videoDevice == null ? "Enable Camera" : "Disable Camera";
        private string _videoDeviceButtonIcon => _videoDevice == null ? Icons.Filled.Videocam : Icons.Filled.VideocamOff;
        private Color _videoDeviceButtonIconColor => _videoDevice == null ? Color.Success : Color.Error;

        private TransmitState? _remoteTransmitState;
        private bool _multipleVideos => _remoteTransmitState != null && _remoteTransmitState.Camera && _remoteTransmitState.Capture;
        private void toggleAudioMute()
        {
            if (_audioDevice != null)
            {
                _previouslyUsedAudioDevice = _audioDevice;
                _audioDevice = null;
            }
            else
            {
                Device? device = _previouslyUsedAudioDevice ?? _availableAudioDevices.FirstOrDefault();
                if (device != null)
                {
                    _audioDevice = device;
                }
            }
        }

        private void toggleVideoMute()
        {
            if (_videoDevice != null)
            {
                _previouslyUsedVideoDevice = _videoDevice;
                _videoDevice = null;
            }
            else
            {
                Device? device = _previouslyUsedVideoDevice ?? _availableVideoDevices.FirstOrDefault();
                if (device != null)
                {
                    _videoDevice = device;
                }
            }
        }

        private string? _screenshare = "";

        protected override void OnInitialized()
        {
            Calls.Status.StateChanged += Status_StateChanged;
            Status_StateChanged(Calls.Status.State);
            Calls.RemotePeerId.StateChanged += RemotePeerId_StateChanged;
            RemotePeerId_StateChanged(Calls.RemotePeerId.State);
            Calls.VideoDevice.StateChanged += VideoDevice_StateChanged;
            VideoDevice_StateChanged(Calls.VideoDevice.State);
            Calls.AudioDevice.StateChanged += AudioDevice_StateChanged;
            AudioDevice_StateChanged(Calls.AudioDevice.State);
            Calls.RemoteTransmitState.StateChanged += RemoteTransmitState_StateChanged;
            RemoteTransmitState_StateChanged(Calls.RemoteTransmitState.State);
            Api.SelfUser.StateChanged += SelfUser_StateChanged;
            SelfUser_StateChanged(Api.SelfUser.State);
            base.OnInitialized();
        }

        private void RemoteTransmitState_StateChanged(TransmitState? value)
        {
            _remoteTransmitState = value;
            this.StateHasChanged();
        }

        private void AudioDevice_StateChanged(Device? value)
        {
            this.__audioDevice = value;
            if (this.__audioDevice != null && this._availableAudioDevices.Count == 0)
            {
                this._availableAudioDevices.Add(this.__audioDevice);
            }
            this.StateHasChanged();
        }

        private void VideoDevice_StateChanged(Device? value)
        {
            this.__videoDevice = value;
            if (this.__videoDevice != null && this._availableVideoDevices.Count == 0)
            {
                this._availableVideoDevices.Add(this.__videoDevice);
            }
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
            if (firstRender)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var devices = await Calls.QueryDevices();
                    this._availableVideoDevices.Clear();
                    this._availableVideoDevices.AddRange(devices.VideoDevices);
                    this._availableAudioDevices.Clear();
                    this._availableAudioDevices.AddRange(devices.AudioDevices);
                    this.StateHasChanged();
                }));
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

        private async Task beginScreenShare()
        {
            this._screenshare = await Calls.BeginScreenCapture();
        }

        private async Task endScreenShare()
        {
            await Calls.EndScreenCapture();
            this._screenshare = null;
        }

        private async Task hangup()
        {
            await Calls.TerminateCall();
        }
    }
}