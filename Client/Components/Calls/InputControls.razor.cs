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
using BlazorChat.Client.Services;

namespace BlazorChat.Client.Components.Calls
{
    public partial class InputControls : IDisposable
    {
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
                _calls.SetVideoDevice(value);
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
                _calls.SetAudioDevice(value);
            }
        }
        private string _audioDeviceButtonText => _audioDevice == null ? "Enable Microphone" : "Mute Microphone";
        private string _audioDeviceButtonIcon => _audioDevice == null ? Icons.Filled.Mic : Icons.Filled.MicOff;
        private Color _audioDeviceButtonIconColor => _audioDevice == null ? Color.Success : Color.Error;
        private string _videoDeviceButtonText => _videoDevice == null ? "Enable Camera" : "Disable Camera";
        private string _videoDeviceButtonIcon => _videoDevice == null ? Icons.Filled.Videocam : Icons.Filled.VideocamOff;
        private Color _videoDeviceButtonIconColor => _videoDevice == null ? Color.Success : Color.Error;

        private string? _screenshare = "";

        protected override void OnInitialized()
        {
            _calls.VideoDevice.StateChanged += VideoDevice_StateChanged;
            VideoDevice_StateChanged(_calls.VideoDevice.State);
            _calls.AudioDevice.StateChanged += AudioDevice_StateChanged;
            AudioDevice_StateChanged(_calls.AudioDevice.State);
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

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                var devices = await _calls.QueryDevices();
                this._availableVideoDevices.Clear();
                this._availableVideoDevices.AddRange(devices.VideoDevices);
                this._availableAudioDevices.Clear();
                this._availableAudioDevices.AddRange(devices.AudioDevices);
                this.StateHasChanged();
            }
        }

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

        private async Task beginScreenShare()
        {
            this._screenshare = await _calls.BeginScreenCapture();
        }

        private async Task endScreenShare()
        {
            await _calls.EndScreenCapture();
            this._screenshare = null;
        }

        public void Dispose()
        {
            _calls.VideoDevice.StateChanged -= VideoDevice_StateChanged;
            _calls.AudioDevice.StateChanged -= AudioDevice_StateChanged;
        }
    }
}