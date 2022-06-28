# Client State Management
The clients state is completely determined by its services, which maintain connection with the server and cache data available to the user. 
* The **ChatApiService** is responsible of managing the users session (enabling communication with the Api in the first place) and wrapping Http requests to the Api in a simplified interface for other services and components.
* The **ChatHubService** maintains connection to the SignalR hub and provides C# events for SignalR events
* The **ChatStateService** maintains all other state information known to the client and provides interfaces for manipulating this state.
    * Collections of Users and Channels known to the account.
    * Pending and active RTC calls.
    * Currently viewed channel.
    * Messages of the currently viewed channel (often a subset of all available messages, within a timewindow)

A common concept used is the Observable (defined in Client/Observable.cs). It is a simple helper object combining a state with an event which is invoked whenever the state changes. This helper class is utilised to keep the state services and the UI components in sync.
