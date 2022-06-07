import { DotNet } from "./definitions/@microsoft/dotnet-js-interop"

interface IWindowExtensions {
    makeNewInfiniteListHelper?: (
        id: string,
        scrollElId: string,
        topElId: string,
        bottomElId: string,
        dnetobj: DotNet.DotNetObject,
        callbackTopInView?: string,
        callbackBottomInView?: string
    ) => void;
    webRtcHelper?: RtcManager;
    queryDevices?: () => Promise<DeviceQuery>;
    hasRequestedUserMedia?: boolean;

}

// This class describes objects used for intersection observing and scroll control
class InfiniteListHelper {
    private observer: IntersectionObserver | null;
    private scrollEl: HTMLElement;
    private topEl: HTMLElement;
    private bottomEl: HTMLElement;
    private dnetobj: DotNet.DotNetObject;
    private callbackTopInView: string;
    private callbackBottomInView: string;

    constructor(
        scrollEl: HTMLElement,
        topEl: HTMLElement,
        bottomEl: HTMLElement,
        dnetobj: DotNet.DotNetObject,
        callbackTopInView?: string,
        callbackBottomInView?: string,
    ) {
        this.scrollEl = scrollEl;
        this.topEl = topEl;
        this.bottomEl = bottomEl;
        this.dnetobj = dnetobj
        this.callbackTopInView = callbackTopInView ?? "JS_TopInView";
        this.callbackBottomInView = callbackBottomInView ?? "JS_BottomInView";

        this.observer = new IntersectionObserver((entries) => this.onIntersect(entries), {
            root: this.scrollEl
        });
        this.observer.observe(this.topEl);
        this.observer.observe(this.bottomEl);

        this.initialReport();
    }

    private initialReport(): Promise<void> {
        return this.onIntersect(this.observer.takeRecords());
    }

    private async onIntersect(entries: IntersectionObserverEntry[]): Promise<void> {
        for (const entry of entries) {
            if (entry.isIntersecting) {
                if (entry.target === this.topEl) {
                    await this.dnetobj.invokeMethodAsync<void>(this.callbackTopInView);
                }
                if (entry.target === this.bottomEl) {
                    await this.dnetobj.invokeMethodAsync<void>(this.callbackBottomInView);
                }
            }
        }
    }

    public scrollTo(percentage: number) {
        let y = (this.scrollEl.scrollHeight - this.scrollEl.clientHeight) * percentage;
        this.scrollEl.scrollTo(0, y);
    }

    public scrollIntoView(id: string): boolean {
        const el = document.getElementById(id);
        el?.scrollIntoView(true);
        return Boolean(el !== undefined);
    }

    public dispose(): void {
        this.observer.disconnect();
        this.observer = null;
    }
}

function MakeNewInfiniteListHelper(
    id: string,
    scrollElId: string,
    topElId: string,
    bottomElId: string,
    dnetobj: DotNet.DotNetObject,
    callbackTopInView?: string,
    callbackBottomInView?: string,
): void {
    var obj = new InfiniteListHelper(
        document.getElementById(scrollElId),
        document.getElementById(topElId),
        document.getElementById(bottomElId),
        dnetobj,
        callbackTopInView,
        callbackBottomInView
    );
    globalThis[id] = obj;
}


(window as IWindowExtensions).makeNewInfiniteListHelper = MakeNewInfiniteListHelper;

declare type RtcMessageType = "begin" | "icecandidate" | "offer" | "answer" | "video-offer" | "end";

declare type MediaType = "audio" | "camera" | "capture";

interface RtcMessage {
    type: RtcMessageType;
    callid?: string;
    sdp?: string | null;
    candidate?: string | null;
    sdpMid?: string | null;
    sdpMLineIndex?: number;
}

interface TransmitState {
    audio: string | null;
    camera: string | null;
    capture: string | null;
}

interface RemoteTrack {
    track: MediaStreamTrack,
    stream: MediaStream
}

interface Device {
    id: string;
    label: string;
}

interface DeviceQuery {
    videoDevices: Device[];
    audioDevices: Device[];
}

class MediaTrack {
    public readonly type: MediaType;
    public readonly name: string;

    private _element: HTMLMediaElement | null;
    public get element(): HTMLMediaElement | null { return this._element; }
    public set element(el: HTMLMediaElement | null) {
        if (this._element === el) {
            return;
        }
        this._element = el;
        if (this._element && this.stream && !this.track.muted) {
            this._element.srcObject = this.stream;
            this._element.muted = true;
            this._element.play();
        }
    }

    public readonly stream: MediaStream;
    public readonly track: MediaStreamTrack;

    public constructor(type: MediaType, track: MediaStreamTrack, stream: MediaStream) {
        this.type = type;
        this.name = track.label ?? "unknown track";
        this.track = track;
        this.track.contentHint = this.type;
        this.stream = stream;
        this.track.onunmute = () => {
            if (this.element) {
                this._element.srcObject = this.stream;
                this._element.autoplay = true;
                this._element.play();
            }
        };
        this.track.onmute = () => {
            if (this.element) {
                this.element.srcObject = null;
            }
        }
    }

    public dispose() {
        this.track.stop();
        this.track.onunmute = null;
        this.track.onmute = null;
        this.element = null;
    }
}

class LocalTrack extends MediaTrack {
    private _enabled: boolean;
    public get enabled(): boolean { return this._enabled; }
    public set enabled(val: boolean) {
        this._enabled = val;
        this.track.enabled = val;
    }

    private _connection: RTCPeerConnection | null;
    public get connection(): RTCPeerConnection | null { return this._connection; }
    public set connection(connection: RTCPeerConnection | null) {
        if (this._connection === connection) {
            return;
        }
        if (this._connection && this._sender && !connection) {
            this.removeTrack();
        }
        else if (connection) {
            this.addTrack(connection)
        }
        else {
            this._connection = null;
        }
    }
    private _sender: RTCRtpSender | null;
    public get sender(): RTCRtpSender | null { return this._sender; }

    public readonly device: Device;

    public constructor(type: MediaType, device: Device, track: MediaStreamTrack, stream: MediaStream) {
        super(type, track, stream);
        this.device = device;
    }

    private async addTrack(connection: RTCPeerConnection) {
        if (this._connection && this._connection !== connection) {
            throw "LocalTrack.addTrack: Already has different connection";
        }
        if (this._sender) {
            throw "LocalTrack.addTrack: Already added (_sender == true)";
        }
        this._connection = connection;
        try {
            this._sender = await wrapCallWithTimeout(this._connection, this._connection.addTrack, this.track, this.stream);
        }
        catch (e) {
            throw "LocalTrack.addTrack: RtcPeerConnection.addTrack exception" + e;
        }
    }

    private async removeTrack() {
        if (!this._connection) {
            throw "LocalTrack.removeTrack: (_connection != true)";
        }
        if (!this._sender) {
            throw "LocalTrack.removeTrack: (_sender != true)";
        }
        try {
            await wrapCallWithTimeout(this._connection, this._connection.removeTrack, this._sender);
            this._sender = null;
        }
        catch (e) {
            throw "LocalTrack.removeTrack: RtcPeerConnection.addTrack exception" + e;
        }
    }
}

class MediaElements {
    localVideo: HTMLVideoElement | null = null;
    remoteAudio: HTMLAudioElement | null = null;
    remoteVideo0: HTMLVideoElement | null = null;
    remoteVideo1: HTMLVideoElement | null = null;
}

declare type SignalingRole = "polite" | "impolite" | "unassigned";

class RtcManager {
    private elements: MediaElements = new MediaElements();
    private localCameraTrack: LocalTrack | null = null;
    private localCaptureTrack: LocalTrack | null = null;
    private localAudioTrack: LocalTrack | null = null;
    private remoteAudioTrack: MediaTrack | null = null;
    private remoteVideo0Track: MediaTrack | null = null;
    private remoteVideo1Track: MediaTrack | null = null;
    private dnetobj: DotNet.DotNetObject | null = null;
    private userId: string | null = null;
    private role: SignalingRole = "unassigned";

    private localUserMediaStream: MediaStream | null = null;
    private connection: RTCPeerConnection | null = null;
    private datachannel: RTCDataChannel | null = null;
    private makingOffer: boolean = false;
    private remoteTransmitState: TransmitState | null = null;
    private remoteTracks: RemoteTrack[] = new Array<RemoteTrack>();
    private iceServers: RTCIceServer[] = new Array<RTCIceServer>();

    // Setup pre call
    public async init(dnetobj: DotNet.DotNetObject, userid: string, videoDeviceId?: string, audioDeviceId?: string, iceServers?: RTCIceServer[]) {
        this.dnetobj = dnetobj;
        this.userId = userid;

        // configure constraints to select video/audio device
        let constraints: MediaStreamConstraints | null;
        if (videoDeviceId || audioDeviceId) {
            constraints = {
                audio: audioDeviceId ? { deviceId: audioDeviceId } : false,
                video: videoDeviceId ? { deviceId: videoDeviceId } : false
            }
        }
        else {
            constraints = null;
        }

        // get local media
        if (constraints) {
            this.localUserMediaStream = await wrapCallWithTimeout(navigator.mediaDevices, navigator.mediaDevices.getUserMedia, constraints);

            // setup audio
            const localAudioTracks: MediaStreamTrack[] = this.localUserMediaStream.getAudioTracks();
            if (localAudioTracks.length) {
                const localaudio = localAudioTracks[0];
                this.localAudioTrack = new LocalTrack(
                    "audio",
                    { id: audioDeviceId, label: localaudio.label },
                    localaudio, this.localUserMediaStream);
                this.localAudioTrack.enabled = true;
            }

            // setup video
            const localVideoTracks: MediaStreamTrack[] = this.localUserMediaStream.getVideoTracks();
            if (localVideoTracks.length) {
                const localvideo = localVideoTracks[0];
                this.localCameraTrack = new LocalTrack(
                    "camera",
                    { id: videoDeviceId, label: localvideo.label },
                    localvideo, this.localUserMediaStream);
                this.localCameraTrack.enabled = true;
            }
        }

        if (iceServers) {
            this.iceServers = iceServers;
            // WebRTC does not like nulled properties!
            for (let server of this.iceServers) {
                for (let property in server) {
                    if (server[property] === null) {
                        server[property] = undefined;
                    }
                }
            }
        }

        this.handleTransmitStateChanged();
    }

    // Sets browser DOM elements. if elements === null, then it reapplies existing elements
    public setElements(elements: MediaElements | null = null) {
        if (elements) {
            this.elements = elements;
        }
        if (!this.elements) {
            this.elements = new MediaElements();
        }

        if (this.localCameraTrack) {
            this.localCameraTrack.element = this.elements.localVideo;
        }
        if (this.remoteAudioTrack) {
            this.remoteAudioTrack.element = this.elements.remoteAudio;
        }
        if (this.remoteVideo0Track) {
            this.remoteVideo0Track.element = this.elements.remoteVideo0;
        }
        if (this.remoteVideo1Track) {
            this.remoteVideo1Track.element = this.elements.remoteVideo1;
        }
    }

    // Sets DOM element ids
    public setElementIds(lvideoid: unknown, raudioid: unknown, rcameraid: unknown, rcaptureid: unknown) {
        this.elements ??= new MediaElements();
        this.elements.localVideo = document.querySelector(`video#${lvideoid}`) as HTMLVideoElement;
        this.elements.remoteAudio = document.querySelector(`audio#${raudioid}`) as HTMLAudioElement;
        this.elements.remoteVideo0 = document.querySelector(`video#${rcameraid}`) as HTMLVideoElement;
        this.elements.remoteVideo1 = document.querySelector(`video#${rcaptureid}`) as HTMLVideoElement;
        this.setElements(this.elements);
    }

    // Sets video transmit device
    public async setVideoDevice(id: string | null) {

        if (this.localCameraTrack) {
            if (this.localCameraTrack.device.id === id) {
                return; // bail if device is already set
            }
            // destroy previous device
            this.localCameraTrack.connection = null;
            this.localCameraTrack.dispose();
            this.localCameraTrack = null;
        }
        if (!id) {
            return; // bail if video is to be disabled
        }
        const constraints: MediaStreamConstraints = {
            video: {
                deviceId: id
            }
        };

        // get local media
        this.localUserMediaStream = await wrapCallWithTimeout(navigator.mediaDevices, navigator.mediaDevices.getUserMedia, constraints);

        // setup video
        const localVideoTracks: MediaStreamTrack[] = this.localUserMediaStream.getVideoTracks();
        if (localVideoTracks.length) {
            const localvideo = localVideoTracks[0];
            this.localCameraTrack = new LocalTrack(
                "camera",
                { id: id, label: localvideo.label },
                localvideo, this.localUserMediaStream);
            this.localCameraTrack.connection = this.connection;
            this.localCameraTrack.enabled = true;
        }

        // set local elements
        this.setElements(null);
        await this.handleTransmitStateChanged();
    }

    // Sets video transmit device
    public async setAudioDevice(id: string | null) {

        if (this.localAudioTrack) {
            if (this.localAudioTrack.device.id === id) {
                return; // bail if device is already set
            }
            // destroy previous device
            this.localAudioTrack.connection = null;
            this.localAudioTrack.dispose();
            this.localAudioTrack = null;
        }
        if (!id) {
            return; // bail if video is to be disabled
        }
        const constraints: MediaStreamConstraints = {
            audio: {
                deviceId: id
            }
        };

        // get local media
        this.localUserMediaStream = await wrapCallWithTimeout(navigator.mediaDevices, navigator.mediaDevices.getUserMedia, constraints);

        // setup video
        const localAudioTracks: MediaStreamTrack[] = this.localUserMediaStream.getAudioTracks();
        if (localAudioTracks.length) {
            const localaudio = localAudioTracks[0];
            this.localAudioTrack = new LocalTrack(
                "audio",
                { id: id, label: localaudio.label },
                localaudio, this.localUserMediaStream);
            this.localAudioTrack.connection = this.connection;
            this.localAudioTrack.enabled = true;
        }

        // set local elements
        this.setElements(null);
        await this.handleTransmitStateChanged();
    }

    // build the rtc connection
    private async buildRtcConnection(): Promise<RTCPeerConnection> {
        if (this.connection) {
            throw ["RtcManager.buildRtcConnection: connection == true!"];
        }

        // apply rtc config
        let config: RTCConfiguration | undefined;
        if (this.iceServers?.length) {
            config = {
                iceServers: this.iceServers
            };
        }
        await wrapCallWithTimeout(null, async () => this.connection = new RTCPeerConnection(config));

        // assign connection to local tracks
        if (this.localCameraTrack) {
            this.localCameraTrack.connection = this.connection;
        }
        if (this.localAudioTrack) {
            this.localAudioTrack.connection = this.connection;
        }

        // Subscribe to events
        this.connection.onicecandidate = e => {
            let msg: RtcMessage;
            if (e.candidate) {
                msg = {
                    type: "icecandidate",
                    candidate: e.candidate.candidate,
                    sdpMid: e.candidate.sdpMid,
                    sdpMLineIndex: e.candidate.sdpMLineIndex
                };
            }
            else {
                msg = {
                    type: "icecandidate",
                    candidate: null
                };
            }
            this.sendRtcSignal(msg);
        };
        this.connection.ontrack = (e: RTCTrackEvent) => {
            if (e.track) {
                if (e.track.kind === "audio") {
                    this.remoteAudioTrack?.dispose();
                    this.remoteAudioTrack = new MediaTrack("audio", e.track, e.streams[0])
                }
                else {
                    if (this.remoteVideo0Track?.track && !this.remoteVideo0Track.track.muted) {
                        this.remoteVideo1Track?.dispose();
                        this.remoteVideo1Track = new MediaTrack("camera", e.track, e.streams[0]);
                    }
                    else {
                        this.remoteVideo0Track?.dispose();
                        this.remoteVideo0Track = new MediaTrack("camera", e.track, e.streams[0]);
                    }
                    //this.mapRemoteTracks();
                }
            }
            this.setElements(null);
        };
        this.connection.onconnectionstatechange = (e) => {
            if (this.connection.connectionState === "closed") {
                this.cleanUp();
            }
            if (this.connection.connectionState === "connected") {
                this.handleTransmitStateChanged();
            }
        };
        this.connection.onnegotiationneeded = (e) => this.handleNegotiationNeeded();
        this.connection.oniceconnectionstatechange = (e) => {
            if (this.connection.iceConnectionState === 'failed') {
                this.connection.restartIce();
            }
        };

        // data channel
        const init: RTCDataChannelInit = {
            id: 0,
            negotiated: true
        };
        this.datachannel = await wrapCallWithTimeout(this.connection, this.connection.createDataChannel, "main", init);
        this.datachannel.onmessage = (msg) => {
            this.handleDatachannelMessage(msg.data);
        }
        this.datachannel.onopen = () => {
            this.handleTransmitStateChanged();
        }

        return this.connection;
    }

    // Handles the connections event to start negotiation
    private async handleNegotiationNeeded() {
        try {
            this.makingOffer = true;
            const offer = await wrapCallWithTimeout(this.connection, this.connection.createOffer);
            await wrapCallWithTimeout(this.connection, this.connection.setLocalDescription, offer);
            this.sendRtcSignal({ type: "offer", sdp: this.connection.localDescription.sdp })
        } finally {
            this.makingOffer = false;
        }
    }

    // Begins the call building
    public async makeCall() {
        this.role = "impolite";
        await this.buildRtcConnection();
    }

    public async handleScreenShare(): Promise<string | null> {
        const constraints: DisplayMediaStreamConstraints = {
            video: true
        };
        try {
            const stream = await navigator.mediaDevices.getDisplayMedia(constraints);
            const track = stream.getVideoTracks()[0];
            if (track) {
                this.localCaptureTrack = new LocalTrack("capture", { id: track.id, label: track.label }, track, stream);
                this.localCaptureTrack.connection = this.connection;
                this.setElements(null);
                return track.label;
            }
        }
        catch (e) {
            console.error("Could not grab screen", e);
        }
        return null;
    }

    public endScreenShare() {
        this.localCaptureTrack?.dispose();
        this.localCaptureTrack = null;
    }

    // #region Datachannel

    private async handleTransmitStateChanged() {
        if (this.datachannel?.readyState === "open") {
            const transmitstate: TransmitState =
            {
                audio: null,
                camera: null,
                capture: null
            };
            if (this.localAudioTrack?.track?.enabled) {
                transmitstate.audio = this.localAudioTrack.stream.id;
            }
            if (this.localCameraTrack?.track?.enabled) {
                transmitstate.camera = this.localCameraTrack.stream.id;
            }
            if (this.localCaptureTrack?.track?.enabled) {
                transmitstate.capture = this.localCaptureTrack.stream.id;
            }
            await this.sendDatachannelMessage(transmitstate);
        }
    }

    private handleDatachannelMessage(data: any) {
        if (typeof data === "string") {
            const obj: TransmitState = JSON.parse(data) as TransmitState;
            this.remoteTransmitState = obj;
        }
    }

    private async sendDatachannelMessage(data: object) {
        if (!this.datachannel || this.datachannel.readyState !== "open") {
            throw "Cannot send datachannel message: Datachannel not ready";
        }
        this.datachannel.send(JSON.stringify(data));
    }

    // #endregion
    // #region Signaling

    // Sends a signalling message
    private async sendRtcSignal(msg: RtcMessage): Promise<boolean> {
        if (!this.dnetobj) {
            console.error("Attempting to send RtcMessage with dnet object bridge not set!");
            return false;
        }
        return await this.dnetobj.invokeMethodAsync<boolean>("dispatchMessage", msg);
    }

    // Called whenever a signalling message is received
    public async handleRtcSignal(msg: RtcMessage): Promise<void> {
        if (msg.type === undefined || msg.type === null) {
            return;
        }
        switch (msg.type) {
            case "icecandidate":
                this.handleCandidate(msg as RTCIceCandidateInit);
                break;
            case "offer":
                this.handleOffer(msg as RTCSessionDescriptionInit);
                break;
            case "answer":
                this.handleAnswer(msg as RTCSessionDescriptionInit);
                break;
            case "end":
                break;
        }
    }

    // Handles incoming ICE Candidates
    private async handleCandidate(candidate: RTCIceCandidateInit) {
        if (!this.connection) {
            console.error('no peerconnection');
            return;
        }
        if (!candidate.candidate) {
            await wrapCallWithTimeout(this.connection, this.connection.addIceCandidate, null);
        } else {
            await wrapCallWithTimeout(this.connection, this.connection.addIceCandidate, candidate);
        }
    }

    // Handles incoming SDP offers
    private async handleOffer(offer: RTCSessionDescriptionInit) {
        if (!this.connection) {
            await this.buildRtcConnection();
        }
        if (this.role === "unassigned") {
            this.role = "polite";
        }
        const offerCollision = this.makingOffer || this.connection.signalingState != "stable";
        const ignoreOffer = this.role === "impolite" && offerCollision;

        if (ignoreOffer) {
            return;
        }
        await wrapCallWithTimeout(this.connection, this.connection.setRemoteDescription, offer);

        let answer: RTCSessionDescriptionInit | null = null;
        answer = await wrapCallWithTimeout(this.connection, this.connection.createAnswer);
        const msg: RtcMessage = { type: "answer", sdp: answer.sdp };
        this.sendRtcSignal(msg);
        await wrapCallWithTimeout(this.connection, this.connection.setLocalDescription, answer);
    }

    // Handles incoming SDP answers
    private async handleAnswer(answer: RTCSessionDescriptionInit) {
        if (this.connection === null) {
            console.error('no peerconnection');
            return;
        }
        if (this.connection.signalingState != "have-local-offer") {
            console.error('state mismatch. Received answer but signalingstate is ', this.connection.signalingState);
            return;
        }
        await wrapCallWithTimeout(this.connection, this.connection.setRemoteDescription, answer);
    }

    // #endregion

    // disposes all states and resets the RtcManager 
    public async cleanUp() {
        this.localAudioTrack?.dispose();
        this.localAudioTrack = null;
        this.localCameraTrack?.dispose();
        this.localCameraTrack = null;
        this.remoteAudioTrack?.dispose();
        this.remoteAudioTrack = null;
        this.remoteVideo0Track?.dispose();
        this.remoteVideo0Track = null;
        this.remoteVideo1Track?.dispose();
        this.remoteVideo1Track = null;
        this.connection?.close();
        this.connection = null;
        this.datachannel = null;
        this.localUserMediaStream = null;
        this.role = "unassigned";
    }

}

// Wraps any function allowing successful catching of rejection and "getting stuck". Very useful for debugging use of Web API
async function wrapCallWithTimeout(thisArg: any, call: any, ...args: any[]): Promise<any> {
    try {
        const timeout = new Promise((resolve => { setTimeout(resolve, 1000, "timeout"); }));
        const action = call.apply(thisArg, args);
        const result = await Promise.race([timeout, action]);
        if (result === "timeout") {
            throw "timeout";
        }
        return result;
    }
    catch (e) {
        console.error("Call threw exception!", e, call, args);
        throw e;
    }
}

// Wraps any function allowing successful catching of rejection and "getting stuck". Very useful for debugging use of Web API
async function wrapCall(thisArg: any, call: any, ...args: any[]): Promise<any> {
    try {
        return await call.apply(thisArg, args);
    }
    catch (e) {
        console.error("Call threw exception!", e, call, args);
        throw e;
    }
}

(window as IWindowExtensions).webRtcHelper = new RtcManager();

// Queries mediaDevices
async function queryDevices(): Promise<DeviceQuery> {

    const result: DeviceQuery = {
        videoDevices: new Array<Device>(),
        audioDevices: new Array<Device>()
    };
    if (!(window as IWindowExtensions).hasRequestedUserMedia) {
        const constraints: MediaStreamConstraints = {
            audio: true,
            video: true
        };
        try {
            await navigator.mediaDevices.getUserMedia(constraints);
        }
        catch (e) {
            return result;
        }
        (window as IWindowExtensions).hasRequestedUserMedia = true;
    }
    const devices = await wrapCallWithTimeout(navigator.mediaDevices, navigator.mediaDevices.enumerateDevices);

    function makeDevice(device: MediaDeviceInfo): Device {
        const result: Device = {
            id: device.deviceId,
            label: String(device.label)
        };
        if (result.label.length === 0) {
            switch (device.kind) {
                case "audioinput":
                    result.label = "Microphone";
                    break;
                case "audiooutput":
                    result.label = "Audio Output";
                    break;
                case "videoinput":
                    result.label = "Camera";
                    break;
            }
        } return result;
    }


    for (const device of devices) {
        if (device.kind === 'audioinput') {

            result.audioDevices.push(makeDevice(device));
        }
        if (device.kind === 'videoinput') {
            result.videoDevices.push(makeDevice(device));
        }
    }

    return result;
}


(window as IWindowExtensions).queryDevices = queryDevices;
(window as IWindowExtensions).hasRequestedUserMedia = false;