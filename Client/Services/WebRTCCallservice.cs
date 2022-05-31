using BlazorChat.Shared;
using Microsoft.JSInterop;
using System.Text.Json;

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
        public string id { get; set; } = "";
        public string label { get; set; } = "";
        public override string ToString()
        {
            return label;
        }
    }

    public class DeviceQuery
    {
        public Device[] videoDevices { get; set; } = Array.Empty<Device>();
        public Device[] audioDevices { get; set; } = Array.Empty<Device>();
    }

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
        public IReadOnlyObservable<ItemId> CallId => callId;
        public IReadOnlyObservable<ECallState> Status => status;
        public IReadOnlyObservable<ItemId> RemotePeerId => remotePeerId;
        public IReadOnlyObservable<Device?> VideoDevice => videoDevice;
        public IReadOnlyObservable<Device?> AudioDevice => audioDevice;
        public IReadOnlyObservable<bool> AudioTransmitEnabled => audioTransmitEnabled;


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
            await _jsRuntime.InvokeVoidAsync(
                "webRtcHelper.init",
                dnetObjRef,
                _apiService.SelfUser.State!.Id.ToString(),
                videoDevice.State?.id ?? null,
                audioDevice.State?.id ?? null
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
            await _jsRuntime.InvokeVoidAsync("webRtcHelper.setAudioDevice", device?.id);
            audioDevice.State = device;
        }

        public async Task SetVideoDevice(Device? device)
        {
            await _jsRuntime.InvokeVoidAsync("webRtcHelper.setVideoDevice", device?.id);
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
            await initiateJS();
            callId.State = await _apiService.InitCall(remotePeerId);
            if (callId.State != default)
            {
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
                await _apiService.TerminateCall(callId.State);
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
        public async Task dispatchMessage(NegotiationMessage message)
        {
            var result = await _hubService.SendNegotiation(callId.State, remotePeerId.State, message);
            if (status.State == ECallState.Pending)
            {
                status.State = ECallState.Ongoing;
            }
            if (!result)
            {
                Console.Error.WriteLine($"Failed to send negotiation message!");
            }
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
