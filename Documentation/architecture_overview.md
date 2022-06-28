# Client
## Source Code Structure
```
/Client
   | - /Components : All Blazor components rendering the UI
   | - /Pages : Blazor components with dedicated page Url
   | - /Scripts : Interop typescript source
   | - /Services : Services managing the actual data displayed by the UI
```
## User Interface

Chat and Call Interfaces are entirely separate (if a call is active, the ChatRoot component is replaced by the CallRoot component in the Root component).

Components can be split roughly into two types:
* Viewers which primarily just display data set via parameters and usually don't interface directly with services
* Controlling elements which display more complex information provided by the service interfaces by arranging other controlling elements and viewers

# Server
## Source Code Structure
```
```
## File Upload
See dedicated [file upload notes file](./fileupload.md)
# Client <-> Server communication
All types used in Client <-> Server communication are defined in the Shared library package.
## Translation
See dedicated [translation notes file](./translation.md)
## SignalR
See dedicated [SignalR notes file](./signalr.md)