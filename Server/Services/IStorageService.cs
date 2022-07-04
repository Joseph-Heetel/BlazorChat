using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using BlazorChat.Shared;
using System.Diagnostics;

namespace BlazorChat.Server.Services
{
    public interface IStorageService
    {
        /// <summary>
        /// Deletes the container with domain id
        /// </summary>
        public Task<bool> DeleteContainer(ItemId domainId);
        /// <summary>
        /// Removes all entries of container with domain id
        /// </summary>
        public Task ClearContainer(ItemId domainId);
        /// <summary>
        /// Uploads a file to container with domain id
        /// </summary>
        /// <returns>Item id of uploaded file if success, zero otherwise</returns>
        public Task<ItemId> UploadFile(ItemId domainId, FileUploadInfo file);
        /// <summary>
        /// Get a temporary url for a file
        /// </summary>
        public Task<TemporaryURL?> GetTemporaryFileURL(ItemId domainId, ItemId fileId, string mimeType);
    }

    public class AzureBlobStorageService : IStorageService
    {
        private BlobServiceClient? _blobServiceClient { get; set; }
        private IIdGeneratorService _idGenService { get; }

        public AzureBlobStorageService(IIdGeneratorService idgen)
        {
            _idGenService = idgen;
            Initialize();
        }

        public void Initialize()
        {
            string? connectionStr = Environment.GetEnvironmentVariable("AzureBlobConnectionString");
            Trace.Assert(connectionStr != null);
            _blobServiceClient = new BlobServiceClient(connectionStr);
        }

        public async Task<BlobContainerClient> GetOrCreateContainerAsync(ItemId channelId)
        {
            Debug.Assert(_blobServiceClient != null);
            var containerclient = _blobServiceClient.GetBlobContainerClient(channelId.ToString());
            await containerclient.CreateIfNotExistsAsync();
            await containerclient.SetAccessPolicyAsync(PublicAccessType.None);
            return containerclient;
        }

        public async Task<ItemId> UploadFile(ItemId domainId, FileUploadInfo file)
        {
            // Make file name
            ItemId fileId = _idGenService.Generate();
            string? remotefilename = FileHelper.MakeFileNameMime(fileId, file.MimeType);
            if (remotefilename == null)
            {
                return default;
            }

            BlobContainerClient containerClient = await GetOrCreateContainerAsync(domainId);
            BlobClient blobClient = containerClient.GetBlobClient(remotefilename);
            // Upload
            using MemoryStream stream = new MemoryStream(file.Data);
            var response = await blobClient.UploadAsync(stream, true);
            if (response != null && response.Value != null)
            {
                return fileId;
            }
            return default;
        }

        public async Task<bool> DeleteContainer(ItemId domainId)
        {
            Debug.Assert(_blobServiceClient != null);
            var response = await _blobServiceClient.DeleteBlobContainerAsync(domainId.ToString());
            return response.Status == 200;
        }

        public async Task<TemporaryURL?> GetTemporaryFileURL(ItemId domainId, ItemId fileId, string mimeType)
        {
            // Make file name
            string? remotefilename = FileHelper.MakeFileNameMime(fileId, mimeType);
            if (remotefilename == null)
            {
                return default;
            }

            // Get the blob and check it exists
            var containerClient = _blobServiceClient!.GetBlobContainerClient(domainId.ToString());
            if (!await containerClient.ExistsAsync())
            {
                return default;
            }
            BlobClient blobClient = containerClient.GetBlobClient(remotefilename);
            var existCheck = await blobClient.ExistsAsync();
            if (!existCheck.Value)
            {
                return default;
            }

            // Build sas url
            DateTimeOffset expires = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(10);
            BlobSasBuilder sasBuilder = new BlobSasBuilder(BlobSasPermissions.Read, expires);
            Uri uri = blobClient.GenerateSasUri(sasBuilder);
            return new TemporaryURL()
            {
                Url = uri.ToString(),
                Expires = expires
            };
        }

        public async Task ClearContainer(ItemId domainId)
        {
            Debug.Assert(_blobServiceClient != null);
            var client = _blobServiceClient.GetBlobContainerClient(domainId.ToString());
            if (!await client.ExistsAsync())
            {
                return;
            }
            // Clear all pages
            var page = client.GetBlobsAsync(BlobTraits.All, BlobStates.None);
            await foreach (var item in page)
            {
                await client.DeleteBlobAsync(item.Name);
            }
        }
    }
}
