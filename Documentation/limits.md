# Security Concerns
There are multiple issues where the software as is would be easy for attackers to exploit:
* Missing rate-limiting on any of the Apis makes it easy to generate massive amounts of "fake" data and DDoSing the server.
* Very limited verification of uploaded files makes it easy for attackers to upload malicious content.
* Missing account verification process allows registering unlimited amount of "fake" users (User registration should be done via the Admin Api).
* Multiple Apis don't consider large amounts of simultaneous requests resulting in race conditions.
* Multi-stage data altering operations lack rollback for when a late operation fails.

# Blazor WebAssembly
* Blazor web apps are big on first load:

    * Firefox 100: 3.60 MB transferred, 16.40 MB uncompressed
    * Edge 101: 8.4 MB transferred, 38.2 MB uncompressed (for an unknown reason Edge's dev tools lists most resources twice. Real download size is probably much smaller, closer to firefox)

* Blazor is quite limited by not having direct built-in access to the Browser Api. Any use of the Browser Api has to be done via the JS Interop feature, which is annoying to setup, debug and has inconsistent handling of errors. This also adds a lot of complexity.

    This project uses the Browser Api to manage scroll state of the message list (scroll messages into view and determine when newer/older messages should be loaded), and for WebRTC to enable P2P video calls.

* Unlike Javascript and Typescript, Blazor's C# WebAssembly can not be debugged directly in the browser. In theory [debugging in Visual Studio](https://docs.microsoft.com/en-us/aspnet/core/blazor/debug?view=aspnetcore-6.0&tabs=visual-studio) is possible, but in practice this rarely even establishes browser link correctly. This leaves the browsers console as the only consistently available solution.

As a consequence, it is the opinion of the author that writing the frontend with a Typescript-based web app library such as Angular, Vue or React is preferable over Blazor compiled to WebAssembly. All used functions are available natively or as libraries and debugging is well supported.

# Audio & Video Calls

* A TURN server is required for NAT traversal. For testing a free service such as [Viagenie NUMB](https://numb.viagenie.ca/) can be used, but for production a dedicated solution needs to be setup (for example [coturn](https://github.com/coturn/coturn)). Right now TURN credentials are hardcoded and therefor shipped as effectively plain text as part of the client application. In a production environment, temporary credentials should be generated dynamically and provided to the client only as needed.
* The video call interface as implemented right now is very crude. The way CSS and videos interact is very complicated. A polished feature-rich UI can be expected to be a lot of work and research.

# Performance
* No effort was spent optimising the application.