# Limits

## Security Concerns
There are multiple issues where the software as is would be easy for attackers to exploit:
* Missing rate-limiting on any of the Apis makes it easy to generate massive amounts of "fake" data and DDoSing the server.
* Very limited verification of uploaded files makes it easy for attackers to upload malicious content.
* Missing account verification process allows registering unlimited amount of "fake" users (User registration should be done via the Admin Api).
* Multiple Apis don't consider large amounts of simultaneous requests resulting in race conditions.
* Multi-stage data altering operations lack rollback for when a late operation fails.

## Blazor WebAssembly
* Blazor web apps are big on first load:

    * Firefox 100: 3.60 MB transferred, 16.40 MB uncompressed
    * Edge 101: 8.4 MB transferred, 38.2 MB uncompressed (for an unknown reason Edge's dev tools lists most resources twice. Real download size is probably much smaller, closer to firefox)

* Blazor is quite limited by not having direct built-in access to the Browser Api. Any use of the Browser Api has to be done via the JS Interop feature, which is annoying to setup, debug and has inconsistent handling of errors. This also adds a lot of complexity.
* Unlike Javascript and Typescript, Blazor's C# WebAssembly can not be debugged directly in the browser. In theory [debugging in Visual Studio](https://docs.microsoft.com/en-us/aspnet/core/blazor/debug?view=aspnetcore-6.0&tabs=visual-studio) is possible, but in practice this rarely even establishes browser link correctly. This leaves the browsers console as the only consistently available solution.

As a consequence, it is the opinion of the author that writing the frontend with a Typescript-based web app library such as Angular, Vue or React is preferable over Blazor compiled to WebAssembly. All used functions are available natively or as libraries and debugging is well supported.

## Performance
Minimal effort was spent optimising the application. Here are some ideas on how it can be improved:
* Server side caching. Keeping frequently requested resources in memory would greatly improve server response times.
* Sharding: Find a way to split the whole chat service across multiple servers.

## Data Consistency
There are multiple server side services which manipulate multiple storage domains in a sequential manner. Failure late into the process of data manipulation can cause inconsistent data across these storage domains. This in turn can cause junk data taking up database or storage space or in the worst case security issues etc.

For a production environment many server side processes would have to be rewritten to feature adequate error recovery.

Furthermore, the current ItemId unique Id system may not be sufficiently collision proof. In a production environment special attention should be given to the consequences of collisions, or an Id scheme with even reduced collision chance or none at all should be used. Some ideas:
* GUIDs would be perfectly fine but a good bit larger
* A symmetric encryption algorithm configured with a secret key and fed with an incrementing number would generate seemingly random but guaranteed unique Ids (until exhaustion of the Id space, but that's a given)