# WebRTC in a Blazor Application
Blazor Webassembly has no access to the Browser Api through C#. The WebRTC implementation built into modern browsers therefor has to be accessed through helper functions in Javascript.

## WebRTC
WebRTC is a highlevel api which allows managing a realtime connection between two clients in a peer 2 peer manner (a "direct" connection without a server, albeit a TURN server for forwarding is required in case NAT traversals prevent a direct connection). The browser apis implementation is specifically catering to video and audio transmitting, having built in support for the browsers MediaStream / MediaStreamTrack types.
## P2P Call Process
1. Request access to camera and audio:

    Any script wanting to utilise camera or microphone input has to request access from the browser and in extension the user. Some browsers do not expose the list of devices without this access.

    1. Request camera and microphone access
    1. Get the list of audio and video input devices
    1. The user chooses which inputs (if any) to activate
1. Configuring the RTC Peer connection
    1. Get a list of STUN / TURN configurations from the servers
    1. Attach Streams and configure the data channel
1. Establishing connection
    
    Set local/remote descriptions and Forward negotiation messages. [Useful Guide on the topic](https://developer.mozilla.org/en-US/docs/Web/API/WebRTC_API/Perfect_negotiation).

    Negotiation messages are transmitted between peers via the servers SignalR hub.
## Issues
* Whenever the browser Api is invoked from JS interop, promise rejections or other exceptions are not guaranteed to end up in the browser console (unlike .NET WASM exceptions). As a solution browser Api calls are wrapped in try catch blocks.
* There are instances where a browser api call simply never returns. As a solution calls to the browser api are raced against a 1 second timeout.
* RTCPeerConnection doesn't begin negotiating if there is no payload (track or datachannel) attached. A solution is to attach a datachannel by default (wether its actually used or not)
* There is no way of marking streams attached to a RTCPeerConnection
    * Stream Ids, Track Ids, Track labels sometimes are preserved, sometimes are not depending on the clients browser.
    * Changes to the Sdp string work with some browsers, but on other browsers they cause irrecoverable errors or are simply removed.

    As a consequence it is a big challenge to determine wether an incoming video stream is a camera or a screen capture. The final "solution" for this implementation was to just not label incoming streams in the UI.

## STUN/TURN

A [STUN](https://en.wikipedia.org/wiki/STUN) server is required for clients to discover their public Ip. Plenty of free services exist, such as `stun:stun.l.google.com:19302`. 

A [TURN](https://en.wikipedia.org/wiki/Traversal_Using_Relays_around_NAT) server is required for NAT traversal. For testing a free service such as [Viagenie NUMB](https://numb.viagenie.ca/) can be used, but for production a dedicated solution needs to be setup (for example [coturn](https://github.com/coturn/coturn)). Right now TURN credentials are fixed environment variables and therefor shipped as effectively plain text as part of the client application. In a production environment, temporary credentials should be generated dynamically and provided to the client only as needed.
