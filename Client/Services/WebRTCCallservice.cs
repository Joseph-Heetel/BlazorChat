using BlazorChat.Shared;
using Microsoft.JSInterop;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorChat.Client.Services
{
    public enum ECallState
    {
        None,
        Pending,
        Ongoing
    }

    public class Device
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        [JsonPropertyName("label")]
        public string Label { get; set; } = "";
        public override string ToString()
        {
            return Label;
        }
    }

    public class DeviceQuery
    {
        [JsonPropertyName("videoDevices")]
        public Device[] VideoDevices { get; set; } = Array.Empty<Device>();
        [JsonPropertyName("audioDevices")]
        public Device[] AudioDevices { get; set; } = Array.Empty<Device>();
    }

    public class TransmitState
    {
        [JsonPropertyName("audio")]
        public bool Audio { get; set; }
        [JsonPropertyName("camera")]
        public bool Camera { get; set; }
        [JsonPropertyName("capture")]
        public bool Capture { get; set; }
    };

    /// <summary>
    /// Service maintaining call state and handling communication with JS and the browser API
    /// </summary>
    public interface ICallService
    {
        /// <summary>
        /// Currently active call id. Zeroed Id if no call is active.
        /// </summary>
        public IReadOnlyObservable<ItemId> CallId { get; }
        /// <summary>
        /// Current call status
        /// </summary>
        public IReadOnlyObservable<ECallState> Status { get; }
        /// <summary>
        /// Id of the remote peer
        /// </summary>
        public IReadOnlyObservable<ItemId> RemotePeerId { get; }
        /// <summary>
        /// Currently selected video input device
        /// </summary>
        IReadOnlyObservable<Device?> VideoDevice { get; }
        /// <summary>
        /// Currently selected audio input device
        /// </summary>
        IReadOnlyObservable<Device?> AudioDevice { get; }
        /// <summary>
        /// Current state of audio transmit
        /// </summary>
        IReadOnlyObservable<bool> AudioTransmitEnabled { get; }
        /// <summary>
        /// Transmit state of the remote peer (which sources are they transmitting)
        /// </summary>
        IReadOnlyObservable<TransmitState?> RemoteTransmitState { get; }

        /// <summary>
        /// Initiate a call to remote peer
        /// </summary>
        /// <param name="remotePeerId"></param>
        /// <param name="video"></param>
        /// <param name="audio"></param>
        /// <returns></returns>
        public Task<bool> InitiateCall(ItemId remotePeerId, Device? video = null, Device? audio = null);
        /// <summary>
        /// Accept a pending call
        /// </summary>
        /// <param name="pending"></param>
        /// <param name="video"></param>
        /// <param name="audio"></param>
        /// <returns></returns>
        public Task AcceptCall(PendingCall pending, Device? video = null, Device? audio = null);
        /// <summary>
        /// Terminate the currently ongoing call
        /// </summary>
        /// <returns></returns>
        public Task TerminateCall();
        /// <summary>
        /// Pass element ids over to JS side
        /// </summary>
        /// <param name="localVideo"></param>
        /// <param name="remoteAudio"></param>
        /// <param name="remoteCamera"></param>
        /// <param name="remoteCapture"></param>
        /// <returns></returns>
        public Task SetVideoElements(string localVideo, string remoteAudio, string remoteCamera, string remoteCapture);
        /// <summary>
        /// Clears element ids
        /// </summary>
        /// <returns></returns>
        public Task ClearVideoElements();
        /// <summary>
        /// Sets the audio input device
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        public Task SetAudioDevice(Device? device);
        /// <summary>
        /// Sets the video input device
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        public Task SetVideoDevice(Device? device);
        /// <summary>
        /// Gets a list of input devices from the browser api
        /// </summary>
        /// <returns></returns>
        public Task<DeviceQuery> QueryDevices();
        /// <summary>
        /// Initiates screen capture
        /// </summary>
        /// <returns></returns>
        public Task<string?> BeginScreenCapture();
        /// <summary>
        /// Terminates screen capture
        /// </summary>
        /// <returns></returns>
        public Task EndScreenCapture();
        public event Action? OnIceConnectFailed;
    }

    public class WebRTCCallservice : ICallService, IAsyncDisposable
    {
        private readonly IChatApiService _apiService;
        private readonly IChatHubService _hubService;
        private readonly IJSRuntime _jsRuntime;

        private readonly DotNetObjectReference<WebRTCCallservice> dnetObjRef;

        private readonly Observable<ItemId> callId = new Observable<ItemId>(default);
        private readonly Observable<ECallState> status = new Observable<ECallState>(default);
        private readonly Observable<ItemId> remotePeerId = new Observable<ItemId>(default);
        private readonly Observable<bool> audioTransmitEnabled = new Observable<bool>(false);
        private readonly Observable<Device?> videoDevice = new Observable<Device?>(null);
        private readonly Observable<Device?> audioDevice = new Observable<Device?>(null);
        private readonly Observable<TransmitState?> remoteTransmitState = new Observable<TransmitState?>(null);

        public event Action? OnIceConnectFailed;

        public IReadOnlyObservable<ItemId> CallId => callId;
        public IReadOnlyObservable<ECallState> Status => status;
        public IReadOnlyObservable<ItemId> RemotePeerId => remotePeerId;
        public IReadOnlyObservable<Device?> VideoDevice => videoDevice;
        public IReadOnlyObservable<Device?> AudioDevice => audioDevice;
        public IReadOnlyObservable<bool> AudioTransmitEnabled => audioTransmitEnabled;
        public IReadOnlyObservable<TransmitState?> RemoteTransmitState => remoteTransmitState;


        public WebRTCCallservice(IChatApiService api, IChatHubService hub, IJSRuntime js)
        {
            this._apiService = api;
            this._hubService = hub;
            this._jsRuntime = js;
            dnetObjRef = DotNetObjectReference.Create(this);
            hub.OnCallNegotiation += Hub_OnCallNegotiation;
            hub.OnCallTerminated += Hub_OnCallTerminated;
        }

        private async Task Hub_OnCallNegotiation(ItemId callId, ItemId senderId, NegotiationMessage msg)
        {
            if (senderId != _apiService.SelfUser.State!.Id)
            {
                await _jsRuntime.InvokeVoidAsync("webRtcHelper.handleRtcSignal", msg);
            }
        }

        private Task Hub_OnCallTerminated(ItemId callId)
        {
            return cleanLocal();
        }

        private async Task initiateJS()
        {
            IceConfiguration[]? iceConfigs = await _apiService.GetIceConfigurations(callId.State);
            iceConfigs ??= Array.Empty<IceConfiguration>();
            await _jsRuntime.InvokeVoidAsync(
                    "webRtcHelper.init",
                    dnetObjRef,
                    videoDevice.State?.Id ?? null,
                    audioDevice.State?.Id ?? null,
                    iceConfigs
                );
        }

        public async Task AcceptCall(PendingCall pending, Device? video = null, Device? audio = null)
        {
            if (status.State != ECallState.None)
            {
                await TerminateCall();
            }
            // JS makeCall
            status.State = ECallState.Ongoing;
            callId.State = pending.Id;
            remotePeerId.State = pending.CallerId;
            videoDevice.State = video;
            audioDevice.State = audio;
            await initiateJS();
            await _apiService.ElevateCall(pending.Id);
            await _jsRuntime.InvokeVoidAsync("webRtcHelper.makeCall");
        }

        public async Task ClearVideoElements()
        {
            await _jsRuntime.InvokeVoidAsync("webRtcHelper.setElementIds", "disabled", "disabled", "disabled");
        }

        public async Task SetAudioDevice(Device? device)
        {
            await _jsRuntime.InvokeVoidAsync("webRtcHelper.setAudioDevice", device?.Id);
            audioDevice.State = device;
        }

        public async Task SetVideoDevice(Device? device)
        {
            await _jsRuntime.InvokeVoidAsync("webRtcHelper.setVideoDevice", device?.Id);
            videoDevice.State = device;
        }

        public async ValueTask DisposeAsync()
        {
            if (Status.State != ECallState.None)
            {
                await TerminateCall();
            }
        }

        public async Task<bool> InitiateCall(ItemId remotePeerId, Device? video = null, Device? audio = null)
        {
            if (status.State != ECallState.None)
            {
                await TerminateCall();
            }
            videoDevice.State = video;
            audioDevice.State = audio;
            callId.State = await _apiService.InitCall(remotePeerId);
            if (callId.State != default)
            {
                await initiateJS();
                status.State = ECallState.Pending;
                this.remotePeerId.State = remotePeerId;
                return true;
            }
            return false;
        }

        public async Task SetVideoElements(string localVideo, string remoteAudio, string remoteCamera, string remoteCapture)
        {
            await _jsRuntime.InvokeVoidAsync("webRtcHelper.setElementIds", localVideo, remoteAudio, remoteCamera, remoteCapture);
        }

        public async Task TerminateCall()
        {
            if (status.State != ECallState.None)
            {
                ItemId terminateCallId = callId.State;
                // Do this out of sync because the api call to terminate may be expected to fail
                // in certain situations (server restarted during call for example)
                // The result of the call is not important
                _ = Task.Run(() => _apiService.TerminateCall(terminateCallId));
            }
            await cleanLocal();
        }

        private async Task cleanLocal()
        {
            await _jsRuntime.InvokeVoidAsync("webRtcHelper.cleanUp");
            callId.State = default;
            remotePeerId.State = default;
            status.State = ECallState.None;
        }

        [JSInvokable]
        public async Task<bool> dispatchMessage(NegotiationMessage message)
        {
            bool result = false;
            try
            {
                result = await _hubService.SendNegotiation(callId.State, remotePeerId.State, message);
            }
            catch (Exception)
            {
                // 
                _ = Task.Run(TerminateCall);
                return false;
            }
            if (status.State == ECallState.Pending)
            {
                status.State = ECallState.Ongoing;
            }
            if (!result)
            {
                Console.Error.WriteLine($"Failed to send negotiation message!");
                return false;
            }
            return true;
        }

        [JSInvokable]
        public void remoteTransmitStateChanged(TransmitState? state)
        {
            if (state != null)
            {
                remoteTransmitState.State = state;
            }
        }

        [JSInvokable]
        public void iceConnectFailed()
        {
            OnIceConnectFailed?.Invoke();
        }

        public async Task<DeviceQuery> QueryDevices()
        {
            return await _jsRuntime.InvokeAsync<DeviceQuery>("window.queryDevices");
        }

        public async Task<string?> BeginScreenCapture()
        {
            return await _jsRuntime.InvokeAsync<string?>("webRtcHelper.handleScreenShare");
        }

        public async Task EndScreenCapture()
        {
            await _jsRuntime.InvokeVoidAsync("webRtcHelper.endScreenShare");
        }
    }
}
