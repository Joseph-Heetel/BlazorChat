# Realtime Features
Some features of a *realtime* chat app require the ability of notifying the client about server states changing. When the notifications subject is small enough, it is also feasible to include the new state in the notification.

In the context of a chat application there are multiple such states which can change, including but not limited to:
* A channels message list is appended (New message received)
* A channels properties (name, participants) changed
* A message object changed
* A users properties (name, avatar) changed

All notification events used in this implementation are listed and documented in `Shared/SignalRConstants.cs`

## SignalR
Microsofts SignalR SDK is the chosen implementation for this, originally selected for:
* Automatic transport negotiation
* Highly configurable Server/Client architecture

## SignalR Issues
* The clients retry policy can be configured with a custom object. This is required, since by default the client won't reconnect at all, and the default automatic reconnect option won't try more than 4 times. A chat application should be expected to automatically reestablish connection even after hours of disconnect.

    What caused some headaches is that an exception thrown in the custom retry policy function will be silently caught and interpreted as no longer retrying. A simple error here may never be caught and cause the client to stop attempting to reconnect unexpectedly.
* The hub exposes functions, events and data structures to manage connections only partially:
    * An event fired when a new connection to the hub is established.
    * An event fired when a connection is terminated. This one does not fire reliably, see below
    * Groups
        * The ability to add or remove connections to/from groups.
        * The ability to notify all connections in a group
        * Impossible of determining which connections are in a group.
        * Impossible to clear a group
    * Impossible to determine which connections exist at one time. This in particular is a problem because a new group needs to be created when a new channel is created.
* When accessing the hub from within other services, it has to be injected as `IHubContext<THub>`. A context interface without type parameter exists, but is deprecated!
* For some reason client methods can not directly return data back to the hub.

### Hub.OnDisconnectedAsync
The hubs `OnDisconnectedAsync` callback is NOT guaranteed to fire (see a [Github issue of 2018 on the topic](https://github.com/aspnet/SignalR/issues/1290#issuecomment-375434028)). There are various technical reasons for why, resulting from the vastly different transport methods, client implementations and operating systems signalr supports. The encouraged way to solve this is by implementing a custom ping pong so one can track a custom connection status. 

Our implementation has this feature as part of the [Server/Hubs/HubManager class](../Server/Hubs/HubManager_Watch.cs). Here we periodically ask the client to return a keepalive, which in turn bumps the expiration time of the connection. Inactive connections will eventually run over the expiration limit, allowing the associated user to be set to offline (if it is the only connection associated with them).