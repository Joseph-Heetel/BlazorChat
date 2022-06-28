# Client Side Caching
As an effort to improve application launch performance, a client side cache using local storage was implemented. See `Client/Services/LocalCacheService.cs`.

* The cache stores users, channels, messages
* Before fetching the current state from the server, the ChatStateService is initialized with the data from local cache
* After fetching the current state from the server, the cache service is updated and resynced
* Since total local storage data size is limited to < 5MB (depending on browser and browser settings) a maximum of items is enforced.

Local Storage access requires the Browser Api. Normally this can only be accessed via Javascript interop, but a Nuget package conveniently wrapping this exists: [Blazored.LocalStorage](https://www.nuget.org/packages/Blazored.LocalStorage/).

The cache implementation is quite simplistic and has some drawbacks and shortcuts

* Once a maximum of entries is reached, the earliest touched items are discarded first (quite a simple caching mode)
* After the initial setup the cache is not used to accelerate anything
* The cache does not allow reducing the network traffic needed between server and client, as there is no way of determining the delta between cached state and server state without transmitting the full data.