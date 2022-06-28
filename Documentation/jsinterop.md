# JS Interop in Blazor WASM
Modern browser expose much of their functionality to frontend code via the Javascript Browser Api. Blazor unfortunately does a pretty bad job at providing these tools to WASM code. As a bandaid solution you are expected to provide the bridge to these tools yourself, via [Blazors JS Interop feature](https://docs.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/?view=aspnetcore-6.0).

## General Implementation Notes
Whenever WASM calls to Javascript it can find Javascript objects by addressing objects attached to javascripts static window/global object. A script needs to attach objects/functions to the window object in order for WASM -> JS calls to function.

If calls from JS to WASM are required, then the JS side requires a handle to the WASM object (`DotNet.DotNetObject` type in Typescript, `DotNetObjectReference<TValue>` in .NET). Ideally the first call from WASM to JS includes this reference.

Whenever a .NET function calls a Javascript function or vice versa, the parameters are first serialized to JSON (using the default serializers for the respective language) and then parsed again in the other language. The same process is applied to a functions return value. This allows passing complex types, just make sure both implementations match and are (de-)serialized in an expected manner.

Exceptions thrown in JS functions usually are logged to the console (but sometimes not, especially if these exceptions occur in other modules or the browser api).

Microsofts Build Tools for Typescript ([Microsoft.TypeScript.MSBuild](https://www.nuget.org/packages/Microsoft.TypeScript.MSBuild/)) is usable for smaller implementations, but for large typescript codebases you would want to consider setting up a javascript package manager with typescript compiler and webpack to compact the scripts.

## Infinite List
An infinite list is a data structure which displays a large data set in smaller sections. Unlike a paged list however it seemlessly loads in adjacent sections to the currently shown sections as the user scrolls. 

In the context of a chat application this is needed for messages:
* The client may not have sufficient memory or compute power to load or display all messages in a channel
* Transmitting an entire channel worth of messages may not be feasible
* A paginated user interface would break the flow of conversation
* The user is unlikely to be interested in the entire chat message history at once

### Implementation Notes
The implementation needs to be able to determine the scroll position of the user interface. 
Blazor WASM native solutions do not work:
* No way of knowing the scroll offset of an element
* No way of knowing wether a component is visible (intersects with the viewport)
* Message rendering is too complex to manually calculate sizes and "scroll" the container using CSS

Due to these shortcomings using JS Interop is mandatory. This is implemented as follows through a Typescript helper type called `InfiniteListHelper`, defined in `Client/Scripts/Interop.ts`:

* Two 1 pixel high transparent divs for detection
    * At the top above all messages
    * At the bottom below all messages
* [Browser Api Intersection Observer](https://developer.mozilla.org/en-US/docs/Web/API/Intersection_Observer_API) observing both of the detection elements
* When one of the detection elements intersects with the viewport, the intersection observer is triggered and in turn notifies the WASM side.
* When the WASM knows one of the elements is being rendered it can request newer/older messages from the server

Alternatively an implementation in Javascript through scroll events and scroll offsets is also possible.
## Jump to Message
Message components mark their primary boundary HTML element with the message id: `<div id="80280315cf877762">...</div>`. This allows querying for the component on the javascript side. The javascript code in turn can instruct the browser to scroll an element into view via [Element.scrollIntoView()](https://developer.mozilla.org/en-US/docs/Web/API/Element/scrollIntoView).