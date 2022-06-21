using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using BlazorChat.Shared;
using System.Diagnostics;

namespace BlazorChat.Server.Services
{
    public interface IStorageService
    {
        public Task<bool> DeleteContainer(ItemId domainId);
        public Task<ItemId> UploadFile(ItemId domainId, FileUploadInfo file);
        public Task<TemporaryURL?> GetTemporaryFileURL(ItemId channelId, ItemId fileId, string mimeType);
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
            ItemId fileId = _idGenService.Generate();
            string? remotefilename = FileHelper.MakeFileNameMime(fileId, file.MimeType);
            if (remotefilename == null)
            {
                return default;
            }
            BlobContainerClient containerClient = await GetOrCreateContainerAsync(domainId);
            BlobClient blobClient = containerClient.GetBlobClient(remotefilename);
            using MemoryStream stream = new MemoryStream(file.Data);
            var response = await blobClient.UploadAsync(stream, true);
            return fileId;
        }

        public async Task<bool> DeleteContainer(ItemId domainId)
        {
            Debug.Assert(_blobServiceClient != null);
            var response = await _blobServiceClient.DeleteBlobContainerAsync(domainId.ToString());
            return response.Status == 200;
        }

        public async Task<TemporaryURL?> GetTemporaryFileURL(ItemId channelId, ItemId fileId, string mimeType)
        {
            string? remotefilename = FileHelper.MakeFileNameMime(fileId, mimeType);
            if (remotefilename == null)
            {
                return default;
            }

            BlobContainerClient containerClient = await GetOrCreateContainerAsync(channelId);
            BlobClient blobClient = containerClient.GetBlobClient(remotefilename);
            var existCheck = await blobClient.ExistsAsync();
            if (!existCheck.Value)
            {
                return default;
            }

            DateTimeOffset expires = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(10);
            BlobSasBuilder sasBuilder = new BlobSasBuilder(BlobSasPermissions.Read, expires);
            Uri uri = blobClient.GenerateSasUri(sasBuilder);
            return new TemporaryURL()
            {
                Url = uri.ToString(),
                Expires = expires
            };
        }
    }
}
