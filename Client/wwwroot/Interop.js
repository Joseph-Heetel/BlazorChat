var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
// This class describes objects used for intersection observing and scroll control
class InfiniteListHelper {
    constructor(scrollEl, topEl, bottomEl, dnetobj, callbackTopInView, callbackBottomInView) {
        this.scrollEl = scrollEl;
        this.topEl = topEl;
        this.bottomEl = bottomEl;
        this.dnetobj = dnetobj;
        this.callbackTopInView = callbackTopInView !== null && callbackTopInView !== void 0 ? callbackTopInView : "JS_TopInView";
        this.callbackBottomInView = callbackBottomInView !== null && callbackBottomInView !== void 0 ? callbackBottomInView : "JS_BottomInView";
        this.observer = new IntersectionObserver((entries) => this.onIntersect(entries), {
            root: this.scrollEl
        });
        this.observer.observe(this.topEl);
        this.observer.observe(this.bottomEl);
        this.initialReport();
    }
    initialReport() {
        return this.onIntersect(this.observer.takeRecords());
    }
    onIntersect(entries) {
        return __awaiter(this, void 0, void 0, function* () {
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    if (entry.target === this.topEl) {
                        yield this.dnetobj.invokeMethodAsync(this.callbackTopInView);
                    }
                    if (entry.target === this.bottomEl) {
                        yield this.dnetobj.invokeMethodAsync(this.callbackBottomInView);
                    }
                }
            }
        });
    }
    scrollTo(percentage) {
        let y = (this.scrollEl.scrollHeight - this.scrollEl.clientHeight) * percentage;
        this.scrollEl.scrollTo(0, y);
    }
    scrollIntoView(id) {
        const el = document.getElementById(id);
        el === null || el === void 0 ? void 0 : el.scrollIntoView(true);
        return Boolean(el !== undefined);
    }
    dispose() {
        this.observer.disconnect();
        this.observer = null;
    }
}
function MakeNewInfiniteListHelper(id, scrollElId, topElId, bottomElId, dnetobj, callbackTopInView, callbackBottomInView) {
    var obj = new InfiniteListHelper(document.getElementById(scrollElId), document.getElementById(topElId), document.getElementById(bottomElId), dnetobj, callbackTopInView, callbackBottomInView);
    globalThis[id] = obj;
}
window.makeNewInfiniteListHelper = MakeNewInfiniteListHelper;
class MediaTrack {
    constructor(type, track, stream) {
        var _a;
        this.type = type;
        this.name = (_a = track.label) !== null && _a !== void 0 ? _a : "unknown track";
        this.track = track;
        this.track.contentHint = this.type;
        this.stream = stream;
        this.track.onunmute = () => {
            //console.log(`${this.type} track "${this.name}" unmuted`);
            if (this.element) {
                this._element.srcObject = this.stream;
                this._element.autoplay = true;
                this._element.play();
            }
        };
        this.track.onmute = () => {
            //console.log(`${this.type} track "${this.name}" muted`);
            if (this.element) {
                this.element.srcObject = null;
            }
        };
    }
    get element() { return this._element; }
    set element(el) {
        if (this._element === el) {
            return;
        }
        if (this._element) {
            this._element.srcObject = null;
        }
        this._element = el;
        if (this._element && this.stream && !this.track.muted) {
            this._element.srcObject = this.stream;
            this._element.muted = true;
            this._element.play();
        }
    }
    dispose() {
        this.track.stop();
        this.track.onunmute = null;
        this.track.onmute = null;
        this.element = null;
    }
}
class LocalTrack extends MediaTrack {
    constructor(type, device, track, stream) {
        super(type, track, stream);
        this.device = device;
    }
    get enabled() { return this._enabled; }
    set enabled(val) {
        this._enabled = val;
        this.track.enabled = val;
    }
    get connection() { return this._connection; }
    set connection(connection) {
        if (this._connection === connection) {
            return;
        }
        if (this._connection && this._sender && !connection) {
            this.removeTrack();
        }
        else if (connection) {
            this.addTrack(connection);
        }
        else {
            this._connection = null;
        }
    }
    get sender() { return this._sender; }
    addTrack(connection) {
        return __awaiter(this, void 0, void 0, function* () {
            if (this._connection && this._connection !== connection) {
                throw "LocalTrack.addTrack: Already has different connection";
            }
            if (this._sender) {
                throw "LocalTrack.addTrack: Already added (_sender == true)";
            }
            this._connection = connection;
            try {
                this._sender = yield wrapCallWithTimeout(this._connection, this._connection.addTrack, this.track, this.stream);
            }
            catch (e) {
                throw "LocalTrack.addTrack: RtcPeerConnection.addTrack exception" + e;
            }
        });
    }
    removeTrack() {
        return __awaiter(this, void 0, void 0, function* () {
            if (!this._connection) {
                throw "LocalTrack.removeTrack: (_connection != true)";
            }
            if (!this._sender) {
                throw "LocalTrack.removeTrack: (_sender != true)";
            }
            try {
                yield wrapCallWithTimeout(this._connection, this._connection.removeTrack, this._sender);
                this._sender = null;
            }
            catch (e) {
                throw "LocalTrack.removeTrack: RtcPeerConnection.addTrack exception" + e;
            }
        });
    }
}
class MediaElements {
    constructor() {
        this.localVideoElement = null;
        this.remoteAudioElement = null;
        this.remoteCameraElement = null;
        this.remoteCaptureElement = null;
    }
}
class RtcManager {
    constructor() {
        this.elements = new MediaElements();
        this.localCameraTrack = null;
        this.localCaptureTrack = null;
        this.localAudioTrack = null;
        this.remoteAudioTrack = null;
        this.remoteCameraTrack = null;
        this.remoteCaptureTrack = null;
        this.dnetobj = null;
        this.userId = null;
        this.role = "unassigned";
        this.localUserMediaStream = null;
        this.connection = null;
        this.datachannel = null;
        this.makingOffer = false;
        this.remoteTransmitState = null;
        this.remoteTracks = new Array();
    }
    // Setup pre call
    init(dnetobj, userid, videoDeviceId, audioDeviceId) {
        return __awaiter(this, void 0, void 0, function* () {
            this.dnetobj = dnetobj;
            this.userId = userid;
            // configure constraints to select video/audio device
            let constraints;
            if (videoDeviceId || audioDeviceId) {
                constraints = {
                    audio: audioDeviceId ? { deviceId: audioDeviceId } : false,
                    video: videoDeviceId ? { deviceId: videoDeviceId } : false
                };
            }
            else {
                constraints = null;
            }
            // get local media
            if (constraints) {
                this.localUserMediaStream = yield wrapCallWithTimeout(navigator.mediaDevices, navigator.mediaDevices.getUserMedia, constraints);
                // setup audio
                const localAudioTracks = this.localUserMediaStream.getAudioTracks();
                if (localAudioTracks.length) {
                    const localaudio = localAudioTracks[0];
                    this.localAudioTrack = new LocalTrack("audio", { id: audioDeviceId, label: localaudio.label }, localaudio, this.localUserMediaStream);
                    this.localAudioTrack.enabled = true;
                }
                // setup video
                const localVideoTracks = this.localUserMediaStream.getVideoTracks();
                if (localVideoTracks.length) {
                    const localvideo = localVideoTracks[0];
                    this.localCameraTrack = new LocalTrack("camera", { id: videoDeviceId, label: localvideo.label }, localvideo, this.localUserMediaStream);
                    this.localCameraTrack.enabled = true;
                }
            }
            this.handleTransmitStateChanged();
        });
    }
    // Sets browser DOM elements. if elements === null, then it reapplies existing elements
    setElements(elements = null) {
        if (elements) {
            this.elements = elements;
        }
        if (!this.elements) {
            this.elements = new MediaElements();
        }
        if (this.localCameraTrack) {
            this.localCameraTrack.element = this.elements.localVideoElement;
        }
        if (this.remoteAudioTrack) {
            this.remoteAudioTrack.element = this.elements.remoteAudioElement;
        }
        if (this.remoteCameraTrack) {
            this.remoteCameraTrack.element = this.elements.remoteCameraElement;
        }
        if (this.remoteCaptureTrack) {
            this.remoteCaptureTrack.element = this.elements.remoteCaptureElement;
        }
    }
    // Sets DOM element ids
    setElementIds(lvideoid, raudioid, rcameraid, rcaptureid) {
        var _a;
        (_a = this.elements) !== null && _a !== void 0 ? _a : (this.elements = new MediaElements());
        this.elements.localVideoElement = document.querySelector(`video#${lvideoid}`);
        this.elements.remoteAudioElement = document.querySelector(`audio#${raudioid}`);
        this.elements.remoteCameraElement = document.querySelector(`video#${rcameraid}`);
        this.elements.remoteCaptureElement = document.querySelector(`video#${rcaptureid}`);
        this.setElements(this.elements);
    }
    // Sets video transmit device
    setVideoDevice(id) {
        return __awaiter(this, void 0, void 0, function* () {
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
            const constraints = {
                video: {
                    deviceId: id
                }
            };
            // get local media
            this.localUserMediaStream = yield wrapCallWithTimeout(navigator.mediaDevices, navigator.mediaDevices.getUserMedia, constraints);
            // setup video
            const localVideoTracks = this.localUserMediaStream.getVideoTracks();
            if (localVideoTracks.length) {
                const localvideo = localVideoTracks[0];
                this.localCameraTrack = new LocalTrack("camera", { id: id, label: localvideo.label }, localvideo, this.localUserMediaStream);
                this.localCameraTrack.connection = this.connection;
                this.localCameraTrack.enabled = true;
            }
            // set local elements
            this.setElements(null);
            yield this.handleTransmitStateChanged();
        });
    }
    // Sets video transmit device
    setAudioDevice(id) {
        return __awaiter(this, void 0, void 0, function* () {
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
            const constraints = {
                audio: {
                    deviceId: id
                }
            };
            // get local media
            this.localUserMediaStream = yield wrapCallWithTimeout(navigator.mediaDevices, navigator.mediaDevices.getUserMedia, constraints);
            // setup video
            const localAudioTracks = this.localUserMediaStream.getAudioTracks();
            if (localAudioTracks.length) {
                const localaudio = localAudioTracks[0];
                this.localAudioTrack = new LocalTrack("audio", { id: id, label: localaudio.label }, localaudio, this.localUserMediaStream);
                this.localAudioTrack.connection = this.connection;
                this.localAudioTrack.enabled = true;
            }
            // set local elements
            this.setElements(null);
            yield this.handleTransmitStateChanged();
        });
    }
    // build the rtc connection
    buildRtcConnection() {
        return __awaiter(this, void 0, void 0, function* () {
            if (this.connection) {
                throw ["RtcManager.buildRtcConnection: connection == true!"];
            }
            // apply rtc config
            const config = {
                iceServers: [{
                        urls: "stun:stun.l.google.com:19302"
                    },
                    //{
                    //    urls: ["stun:numb.viagenie.ca", "turn:numb.viagenie.ca"],
                    //    username: "",
                    //    credentialType: "password",
                    //    credential: ""
                    //}
                ]
            };
            yield wrapCallWithTimeout(null, () => __awaiter(this, void 0, void 0, function* () { return this.connection = new RTCPeerConnection(config); }));
            // assign connection to local tracks
            if (this.localCameraTrack) {
                this.localCameraTrack.connection = this.connection;
            }
            if (this.localAudioTrack) {
                this.localAudioTrack.connection = this.connection;
            }
            // Subscribe to events
            this.connection.onicecandidate = e => {
                let msg;
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
            this.connection.ontrack = (e) => {
                var _a, _b, _c, _d;
                if (e.track) {
                    if (e.track.kind === "audio") {
                        (_a = this.remoteAudioTrack) === null || _a === void 0 ? void 0 : _a.dispose();
                        this.remoteAudioTrack = new MediaTrack("audio", e.track, e.streams[0]);
                    }
                    else {
                        if (((_b = this.remoteCameraTrack) === null || _b === void 0 ? void 0 : _b.track) && !this.remoteCameraTrack.track.muted) {
                            (_c = this.remoteCaptureTrack) === null || _c === void 0 ? void 0 : _c.dispose();
                            this.remoteCaptureTrack = new MediaTrack("capture", e.track, e.streams[0]);
                        }
                        else {
                            (_d = this.remoteCameraTrack) === null || _d === void 0 ? void 0 : _d.dispose();
                            this.remoteCameraTrack = new MediaTrack("camera", e.track, e.streams[0]);
                        }
                        //this.mapRemoteTracks();
                    }
                    console.warn("Recv Video track", e.track.id, e.streams[0].id);
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
            const init = {
                id: 0,
                negotiated: true
            };
            this.datachannel = yield wrapCallWithTimeout(this.connection, this.connection.createDataChannel, "main", init);
            this.datachannel.onmessage = (msg) => {
                this.handleDatachannelMessage(msg.data);
            };
            this.datachannel.onopen = () => {
                this.handleTransmitStateChanged();
            };
            return this.connection;
        });
    }
    // Handles the connections event to start negotiation
    handleNegotiationNeeded() {
        return __awaiter(this, void 0, void 0, function* () {
            try {
                this.makingOffer = true;
                const offer = yield wrapCallWithTimeout(this.connection, this.connection.createOffer);
                //offer.sdp = this.encodeMediaTypes(offer.sdp, ["smokeweed", "everyday"]);
                yield wrapCallWithTimeout(this.connection, this.connection.setLocalDescription, offer);
                //console.log(this.connection.localDescription.sdp);
                this.sendRtcSignal({ type: "offer", sdp: this.connection.localDescription.sdp });
            }
            finally {
                this.makingOffer = false;
            }
        });
    }
    // Begins the call building
    makeCall() {
        return __awaiter(this, void 0, void 0, function* () {
            this.role = "impolite";
            yield this.buildRtcConnection();
        });
    }
    handleScreenShare() {
        return __awaiter(this, void 0, void 0, function* () {
            const constraints = {
                video: true
            };
            try {
                const stream = yield navigator.mediaDevices.getDisplayMedia(constraints);
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
        });
    }
    endScreenShare() {
        var _a;
        (_a = this.localCaptureTrack) === null || _a === void 0 ? void 0 : _a.dispose();
        this.localCaptureTrack = null;
    }
    // #region Datachannel
    handleTransmitStateChanged() {
        var _a, _b, _c, _d, _e, _f, _g;
        return __awaiter(this, void 0, void 0, function* () {
            if (((_a = this.datachannel) === null || _a === void 0 ? void 0 : _a.readyState) === "open") {
                const transmitstate = {
                    audio: null,
                    camera: null,
                    capture: null
                };
                if ((_c = (_b = this.localAudioTrack) === null || _b === void 0 ? void 0 : _b.track) === null || _c === void 0 ? void 0 : _c.enabled) {
                    transmitstate.audio = this.localAudioTrack.stream.id;
                }
                if ((_e = (_d = this.localCameraTrack) === null || _d === void 0 ? void 0 : _d.track) === null || _e === void 0 ? void 0 : _e.enabled) {
                    transmitstate.camera = this.localCameraTrack.stream.id;
                }
                if ((_g = (_f = this.localCaptureTrack) === null || _f === void 0 ? void 0 : _f.track) === null || _g === void 0 ? void 0 : _g.enabled) {
                    transmitstate.capture = this.localCaptureTrack.stream.id;
                }
                yield this.sendDatachannelMessage(transmitstate);
            }
        });
    }
    handleDatachannelMessage(data) {
        if (typeof data === "string") {
            const obj = JSON.parse(data);
            this.remoteTransmitState = obj;
            //this.mapRemoteTracks();
            console.warn("Transmitstate update", obj);
        }
    }
    //private mapRemoteTracks() {
    // Does not work because neither Stream nor Track Id are guaranteed!
    //    if (!this.remoteTransmitState) {
    //        return;
    //    }
    //    for (const track of this.remoteTracks) {
    //        if (this.remoteTransmitState.camera === track.stream.id) {
    //            if (this.remoteCameraTrack?.stream.id !== track.stream.id) {
    //                this.remoteCameraTrack?.dispose();
    //                this.remoteCameraTrack = new MediaTrack("camera", track.track, track.stream);
    //            }
    //        }
    //        if (this.remoteTransmitState.capture === track.stream.id) {
    //            if (this.remoteCaptureTrack?.stream.id !== track.stream.id) {
    //                this.remoteCaptureTrack?.dispose();
    //                this.remoteCaptureTrack = new MediaTrack("capture", track.track, track.stream);
    //            }
    //        }
    //    }
    //    this.setElements(null);
    //}
    sendDatachannelMessage(data) {
        return __awaiter(this, void 0, void 0, function* () {
            if (!this.datachannel || this.datachannel.readyState !== "open") {
                throw "Cannot send datachannel message: Datachannel not ready";
            }
            this.datachannel.send(JSON.stringify(data));
        });
    }
    // #endregion
    // #region Signaling
    // Sends a signalling message
    sendRtcSignal(msg) {
        return __awaiter(this, void 0, void 0, function* () {
            if (!this.dnetobj) {
                console.error("Attempting to send RtcMessage with dnet object bridge not set!");
                return false;
            }
            return yield this.dnetobj.invokeMethodAsync("dispatchMessage", msg);
        });
    }
    // Called whenever a signalling message is received
    handleRtcSignal(msg) {
        return __awaiter(this, void 0, void 0, function* () {
            if (msg.type === undefined || msg.type === null) {
                return;
            }
            switch (msg.type) {
                case "icecandidate":
                    this.handleCandidate(msg);
                    break;
                case "offer":
                    this.handleOffer(msg);
                    break;
                case "answer":
                    this.handleAnswer(msg);
                    break;
                case "end":
                    break;
            }
        });
    }
    // Handles incoming ICE Candidates
    handleCandidate(candidate) {
        return __awaiter(this, void 0, void 0, function* () {
            if (!this.connection) {
                console.error('no peerconnection');
                return;
            }
            if (!candidate.candidate) {
                yield wrapCallWithTimeout(this.connection, this.connection.addIceCandidate, null);
            }
            else {
                yield wrapCallWithTimeout(this.connection, this.connection.addIceCandidate, candidate);
            }
        });
    }
    // Handles incoming SDP offers
    handleOffer(offer) {
        return __awaiter(this, void 0, void 0, function* () {
            if (!this.connection) {
                yield this.buildRtcConnection();
            }
            if (this.role === "unassigned") {
                this.role = "polite";
            }
            const offerCollision = this.makingOffer || this.connection.signalingState != "stable";
            const ignoreOffer = this.role === "impolite" && offerCollision;
            if (ignoreOffer) {
                return;
            }
            for (const line of offer.sdp.split("\n")) {
                if (line.startsWith("a=msid")) {
                    console.warn("SDP MSID", line);
                }
            }
            yield wrapCallWithTimeout(this.connection, this.connection.setRemoteDescription, offer);
            let answer = null;
            answer = yield wrapCallWithTimeout(this.connection, this.connection.createAnswer);
            const msg = { type: "answer", sdp: answer.sdp };
            this.sendRtcSignal(msg);
            yield wrapCallWithTimeout(this.connection, this.connection.setLocalDescription, answer);
        });
    }
    // Handles incoming SDP answers
    handleAnswer(answer) {
        return __awaiter(this, void 0, void 0, function* () {
            if (this.connection === null) {
                console.error('no peerconnection');
                return;
            }
            if (this.connection.signalingState != "have-local-offer") {
                console.error('state mismatch. Received answer but signalingstate is ', this.connection.signalingState);
                return;
            }
            yield wrapCallWithTimeout(this.connection, this.connection.setRemoteDescription, answer);
        });
    }
    // #endregion
    // #region SDP manipulation
    encodeMediaTypes(sdp, types) {
        let result = "";
        let seekmsid = false;
        for (let line of sdp.split("\n")) {
            if (line.startsWith("m=video")) {
                seekmsid = true;
            }
            if (seekmsid && line.startsWith("a=msid")) {
                line = `a=msid:{${types[0]}} {${types[0]}}`;
                types.splice(0, 1);
                seekmsid = false;
            }
            result += line + "\n";
        }
        console.log(result);
        return result;
    }
    // #endregion
    // disposes all states and resets the RtcManager 
    cleanUp() {
        var _a, _b, _c, _d, _e, _f;
        return __awaiter(this, void 0, void 0, function* () {
            (_a = this.localAudioTrack) === null || _a === void 0 ? void 0 : _a.dispose();
            this.localAudioTrack = null;
            (_b = this.localCameraTrack) === null || _b === void 0 ? void 0 : _b.dispose();
            this.localCameraTrack = null;
            (_c = this.remoteAudioTrack) === null || _c === void 0 ? void 0 : _c.dispose();
            this.remoteAudioTrack = null;
            (_d = this.remoteCameraTrack) === null || _d === void 0 ? void 0 : _d.dispose();
            this.remoteCameraTrack = null;
            (_e = this.remoteCaptureTrack) === null || _e === void 0 ? void 0 : _e.dispose();
            this.remoteCaptureTrack = null;
            (_f = this.connection) === null || _f === void 0 ? void 0 : _f.close();
            this.connection = null;
            this.datachannel = null;
            this.localUserMediaStream = null;
            this.role = "unassigned";
        });
    }
}
// Wraps any function allowing successful catching of rejection and "getting stuck". Very useful for debugging use of Web API
function wrapCallWithTimeout(thisArg, call, ...args) {
    return __awaiter(this, void 0, void 0, function* () {
        try {
            const timeout = new Promise((resolve => { setTimeout(resolve, 1000, "timeout"); }));
            const action = call.apply(thisArg, args);
            const result = yield Promise.race([timeout, action]);
            if (result === "timeout") {
                throw "timeout";
            }
            return result;
        }
        catch (e) {
            console.error("Call threw exception!", e, call, args);
            throw e;
        }
    });
}
// Wraps any function allowing successful catching of rejection and "getting stuck". Very useful for debugging use of Web API
function wrapCall(thisArg, call, ...args) {
    return __awaiter(this, void 0, void 0, function* () {
        try {
            return yield call.apply(thisArg, args);
        }
        catch (e) {
            console.error("Call threw exception!", e, call, args);
            throw e;
        }
    });
}
window.webRtcHelper = new RtcManager();
// Queries mediaDevices
function queryDevices() {
    return __awaiter(this, void 0, void 0, function* () {
        console.log("Query devices ...");
        const result = {
            videoDevices: new Array(),
            audioDevices: new Array()
        };
        if (!window.hasRequestedUserMedia) {
            const constraints = {
                audio: true,
                video: true
            };
            try {
                yield navigator.mediaDevices.getUserMedia(constraints);
            }
            catch (e) {
                return result;
            }
            window.hasRequestedUserMedia = true;
        }
        const devices = yield wrapCallWithTimeout(navigator.mediaDevices, navigator.mediaDevices.enumerateDevices);
        function makeDevice(device) {
            const result = {
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
            }
            return result;
        }
        for (const device of devices) {
            if (device.kind === 'audioinput') {
                result.audioDevices.push(makeDevice(device));
            }
            if (device.kind === 'videoinput') {
                result.videoDevices.push(makeDevice(device));
            }
        }
        console.log("Device Query", result);
        return result;
    });
}
window.queryDevices = queryDevices;
window.hasRequestedUserMedia = false;
export {};
