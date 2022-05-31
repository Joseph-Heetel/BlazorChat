# About
This project is the result of cooperation between the [Allgäu Research Center](https://www.hs-kempten.de/en/research/allgaeu-research-centre) (an institute of the [University of Applied Sciences Kempten](https://hs-kempten.de)) and [Soloplan GmbH](https://www.soloplan.de/). The goal is to evaluate complexity and feasibility of developing a **completely custom chat solution** from the ground up by implementing a prototype.

## Motivation
It is commonplace in the logistics branche for companies to employ external contractors (independent drivers).
- Sometimes the default communication path between contractors and the commissioning company is unclear (the initiating party will need to perform lookups of contact information)
- Oftentimes the default communication path is inefficient due to being restricted to just text (example: e-mail) or just voice (telephone)
- Oftentimes the default communication path is inefficient due to not automatically providing context. A custom default communication solution allows automatically maintaining context information (information about the tour the driver is currently on, the cargo being carried etc.), saving on overhead from needing to provide this after initiating communication
- A custom communication solution allows developing of features specific to the logistics branche

# Project Overview
## Features
### Group text chat
![Chat](./Documentation/chat-overview.png)

* Send **text** messages, upload **images** and**files**
* **Realtime** updates of chat messages
* Customizable **userprofiles** (name and profile pictures)
* Per-user **read horizon**
* **Search** for messages
### Peer to peer video chat
* Utilises Browser **WebRTCApi**
* Transmit microphone, camera and screen capture
### Embedded forms
* Forms attached to messages
* Forms are defined as [Json Schemas](https://json-schema.org/)
### Miscellaneous
* All **data access is scoped** to channels (no data leaks between users who don't share channels)
* **Admin REST Api** for managing users, channels and forms

## Base Technology
The project is primarily written in C#. 
- The server application maintains and provides all statically available information. 
- The client project is served by the server, and manages UI states. It is a Blazor single page application (C# compiled to WebAssembly)
- A shared project primarily contains REST Api / Hub interface types and constants

Encapsulated behind services are multiple Microsoft Azure products:
- CosmosDB document database for storing all static information
- BlobStorage for storing uploaded images/files

These are encapsulated such that replacing with alternatives should be relatively simple

## Client <-> Server communication
- A REST Api allows the client to fetch data from the server
- A SignalR Hub is used for all paths of communications initiated by the server, enabling realtime communication

# Cloning and Configuration

**⚠ As described in [Limits](./Documentation/limits.md) this project is merely a proof of concept prototype, and not fit for production use!**

[See setup article here](./Documentation/setup.md)