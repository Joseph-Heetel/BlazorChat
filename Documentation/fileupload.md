# File Upload Features
Currently these features utilise file upload functionality:
* Embedding images in messages
* Embedding pdf documents in messages
* Uploading avatar images

Principles
* Files are always bound to a domain
    * a channel if attached to a message
    * a user if an avatar image

    When the domain is destroyed, all bound files are destroyed aswell
* File name and extension are controlled by the server
* Access to files requires authentication and access to the domain the file is bound to (enforced via temporary link generation)

# File Uploading Procedures
1. Client (see `Client/Components/Chat/SendControl.razor`)
    * Use Blazors InputFile component to scope input to valid types
    * On select: Validate file content type and size
    * Upload file to server by encoding it directly as the http post request content
1. Server 
    
    see `Server/Controllers/StorageController.cs`
    * Make sure the invoking client is attempting to upload to a valid channel that they are member of
    * Read and validate content type from http header
    * Pull file into a memory stream for further processing
    * Validate file size
    
    see `Server/Services/IStorageServices.cs`
    * Create/get container
    * Upload file with automatically generated file name:
        
        `{fileId}.{ext}` where `fileId` is the internal randomly generated Id and `ext` is the extension selected with the content mime type

## Security implications

**Disclaimer: The author has no training in cyber security!**

See articles from [Microsoft](https://docs.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-6.0#security-considerations)
and [OWASP](https://owasp.org/www-community/vulnerabilities/Unrestricted_File_Upload)
* Mitigations by controlling the file name
    * File extensions are set by the server and generally can only be one of `.png`, `.jpg`, `.webm`, `.tiff`, `.bmp`, `.pdf`
    * File names are set by the server exclusively

    These prevent attackers from uploading files which could be (accidently) executed on another clients machine.
* No server-local storage of files prevents attacks requiring a file in the servers file system
* Non-exhaustive list of **remaining vulnerabilities**:
    * File content is processed in **server memory**. Given modern security in .NET applications this being exploitable is unlikely, but never impossible.
    * The file content is not being scanned for malicious content. Controlling file name and extension mitigates many vectors, but still allows exploiting **vulnerabilities in client-side software**
    * The file upload endpoint is very expensive to process server side, making it ideal for **denial of service attacks**

# File Access
Generally, files managed by the chat service are provided to users only temporarily:
* Reduces chance of content escaping the control of chat services. Simply copying url is not sufficient, a separate copy needs to be shared via other services to share files to destinations outside of the chat service.
* Significantly reduces viability of abusing the chat services upload feature as permanent file storage

Currently, temporary links provided by the server expire after 10 minutes. The clients IMediaResolverService `Client/Services/MediaResolverService.cs` provides an implementation which automatically refreshes temporary links as long as a certain resource is needed.

Alternatively to providing temporary links to azure blob storage entries the server could provide the file contents itself. This would allow authenticating any access to the resource, but would cause a lot of stress on the servers resources (forwarding large amounts of data).

## Generating temporary links
1. Make sure requesting client is allowed to retrieve the resource
1. Generate an automatically expiring [Sas Uri](https://docs.microsoft.com/en-us/azure/storage/blobs/sas-service-create?tabs=dotnet) for the Azure Blobstorage object