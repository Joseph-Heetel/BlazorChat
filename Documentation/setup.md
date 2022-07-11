# Setup

**⚠ As described in [Limits](./limits.md) this project is merely a proof of concept prototype, and not fit for production use!**

## Clone
```
git clone https://github.com/Soloplan-Innovation-Lab/BlazorChat
```
## Setup CosmosDB Emulator

1. Get [Azure CosmosDB Emulator](https://docs.microsoft.com/de-de/azure/cosmos-db/local-emulator?tabs=ssl-netstd21). This allows you to test most of the functionality without having to use a microsoft account and having to set up a CosmosDB account.
1. Open data explorer (CosmosDB Emulator Tray Icon) and copy your primary connection string. Provide this connection string as an environment variable when running by adding it to Server/Properties/launchSettings.json as `AzureCosmosConnectionString`

## Build and Run


Configure compiler flags and optional environment variables if you want. Initialise your dotnet environment and Build the project. Whenever the database service does not find the required tables present it creates those and adds some default data.

Open your browser and navigate to [https:://localhost:7196/chat](https:://localhost:7196/chat). You may need to trust the dev environment https certificate (`dotnet dev-certs https --trust`).

In the chat login page, authenticate with `testuser` and `password`. Refer to the AdminApi to generate and configure more users.

(You can connect from other devices in the local network via `https://<HostingComputersLocalIp>:7196/chat`)

## Compile Flag Reference
### Client
|Flag|Description|Files|
|-|-|-|
|`ENABLE_ENDUSER_SELFREGISTER`|If set, shows the "register" option in the login screen. Be sure to set it on the server project too to enable the register Api endpoint.|Client/Components/Root.razor.cs|
|`HUBDEBUGLOGGING`|If set, prints to the browser console everytime the server invokes a SignalR hub on the client.|Client/Services/ChatHubService.cs|
### Server
|Flag|Description|Files|
|-|-|-|
|`ENABLE_ENDUSER_SELFREGISTER`|If set, allows users to invoke the register endpoint, allowing anyone to create user accounts. Be sure to set it on the client too to enable the UI.|Server/Controllers/SessionController.cs|
|`ENABLE_ADMINAPI_AUTH`|If set, all requests to the admin api need to be authorized by providing a bearer token matching the environment variable `AdminApiBearerToken`|Server/Program.cs|

## Environment Variable Reference
For debugging you may set environment variables via `Server/Properties/launchSettings.json`
|Variable|Description|
|-|-|
|`AdminApiBearerToken`|The bearer token string that requests to the admin api need to match if `ENABLE_ADMINAPI_AUTH` compiler flag is set|
|`AzureCosmosConnectionString` (required)|Connection string used to contact the database backend|
|`AzureBlobConnectionString`|Connection string used to contact the blob storage|
|`IceConfigurations`|Array of [RTCIceServer](https://developer.mozilla.org/en-US/docs/Web/API/RTCIceServer) json objects (urls property must be array!). Provided to the client for initializing the RTCPeerConnection with (enabling STUN and TURN support accordingly). |
|`AzureTranslatorKey`|Key used to authenticate with Azure translator services. |
|`AzureTranslatorLocation`|Location of Azure Translator Service.|
|`JwtSecret`|Key used for signing JWTs (for token sign-in urls generated via `adminapi/users/makesession` endpoint). JWTs are signed with HMAC-SHA256 and the key passed as UTF8 represantation byte array.|

### Example Iceconfigurations Env Variable
```
"IceConfigurations": 
"[
    {\"urls\": [\"stun:stun.l.google.com:19302\"]},
    {
        \"urls\":[\"stun:s.example.com\",\"turn:t.example.com\"],
        \"username\":\"myusername\",
        \"credentialType\":\"password\",
        \"credential\":\"mypassword\"
    }
]"
```