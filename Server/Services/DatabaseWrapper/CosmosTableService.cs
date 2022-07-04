using BlazorChat.Server.Models;
using BlazorChat.Shared;
using Microsoft.Azure.Cosmos;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorChat.Server.Services.DatabaseWrapper
{
    public class CosmosTableService<T> : ITableService<T>
    {
        public string PartitionPath { get; private set; } = "";
        public Container? Container { get; set; }

        private readonly string _tableName;
        private readonly ILogger _logger;
        private PropertyInfo? _partitionKeyProperty;
        private PropertyInfo? _idProperty;
        private bool _generateKeyFromId = false;

        public CosmosTableService(IServiceProvider serviceProvider, string tableName)
        {
            _tableName = tableName;
            _logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger($"CosmosTableService<{nameof(T)}>");
            GetTypeInformation();
        }

        /// <summary>
        /// Finds partition key property (Marked with PartitionPropertyAttribute) and id key property (marked wiht JsonPropertyNameAttribute("id))
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void GetTypeInformation()
        {
            foreach (var property in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.CanRead)
                {
                    var propertyAttribute = property.GetCustomAttribute<PartitionPropertyAttribute>();
                    var jsonNameAttribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();
                    if (propertyAttribute != null)
                    {
                        if (jsonNameAttribute != null)
                        {
                            PartitionPath = $"/{jsonNameAttribute.Name}";
                        }
                        else
                        {
                            PartitionPath = $"/{property.Name}";
                        }
                        _partitionKeyProperty = property;
                        _generateKeyFromId = propertyAttribute.GenerateFromId;
                    }
                    if (jsonNameAttribute != null && jsonNameAttribute.Name == "id")
                    {
                        _idProperty = property;
                    }
                }
            }
            if (_partitionKeyProperty == null)
            {
                throw new Exception("Init failed - no partition key property detected");
            }
            if (_idProperty == null)
            {
                throw new Exception("Init failed - no id property detected");
            }
        }

        /// <summary>
        /// Use partitionkey property to get a partition key
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private PartitionKey GetPartitionKey(T item)
        {
            string key = (_partitionKeyProperty!.GetValue(item)!.ToString())!;
            return new PartitionKey(key);
        }

        /// <summary>
        /// Use id property to get item id
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private string GetId(T item)
        {
            return _idProperty!.GetValue(item)!.ToString()!;
        }

        [ThreadStatic]
        private static byte[]? _utf8Bytes;

        [ThreadStatic]
        private static HashAlgorithm? _hashAlgorithm;

        /// <summary>
        /// Generate the partitionkey value from id string
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Method is used but model type prohibits generating key from id</exception>
        private string GeneratePartitionKeyValue(string id)
        {
            if (_generateKeyFromId)
            {
                // Convert id to byte array
                const int utf8arrlength = ItemId.IDLENGTH * 2;
                _utf8Bytes ??= new byte[utf8arrlength + 1];
                Encoding.UTF8.GetBytes(id, 0, Math.Min(utf8arrlength, id.Length), _utf8Bytes, 0);

                // hash byte array
                _hashAlgorithm ??= HashAlgorithm.Create(HashAlgorithmName.SHA256.Name!)!;
                byte[] hashed = _hashAlgorithm.ComputeHash(_utf8Bytes);

                // compact hash array to length of 4
                int size = hashed.Length;
                for (; size > 4; size /= 2)
                {
                    int halfSize = size / 2;
                    for (int i = 0; i < halfSize; i++)
                    {
                        hashed[i] ^= hashed[i + halfSize];
                    }
                }
                return Convert.ToHexString(hashed, 0, 4);
            }
            else
            {
                throw new InvalidOperationException("PartitionProperty Attribute has GenerateFromKey disabled!");
            }
        }

        private PartitionKey GeneratePartitionKey(string id)
        {
            return new PartitionKey(GeneratePartitionKeyValue(id));
        }

        /// <summary>
        /// Run the bulk delete script
        /// </summary>
        /// <param name="query">If specified, only affects items matching the query</param>
        /// <param name="partitionKey">If specified, only affects items within one partition</param>
        /// <returns></returns>
        public async Task<TableActionResult> BulkDeleteItemsAsync(string? query = null, string? partitionKey = null)
        {
            PartitionKey partKey = new PartitionKey(partitionKey);
            query ??= "SELECT i.id FROM i";
            var response = await Container!.Scripts.ExecuteStoredProcedureStreamAsync(CosmosDatabaseConnection.BULKDELETESPROC, partKey, new dynamic[] { query });
            if (response.IsSuccessStatusCode)
            {
                return TableActionResult.Success;
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return TableActionResult.NotFound;
            }
            return TableActionResult.Error;

        }

        /// <summary>
        /// Creates a new item
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public async Task<TableActionResult> CreateItemAsync(T item)
        {
            autoSetPartitionKey(item);
            PartitionKey partKey = GetPartitionKey(item);
            try
            {
                using MemoryStream stream = new MemoryStream(8192);
                using var jsonwriter = new Utf8JsonWriter(stream);
                JsonSerializer.Serialize(jsonwriter, item);
                stream.Position = 0;
                CancellationTokenSource cts = new CancellationTokenSource();
                var responseTask = Container!.CreateItemStreamAsync(stream, partKey, null, cts.Token);
                using var response = await responseTask;
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to create a resource on {Container!.Id} in partition \"{partKey}\". Reason: {response.StatusCode}: {response.ErrorMessage}");
                    return TableActionResult.Error;
                }
                return TableActionResult.Success;
            }
            catch (CosmosException e)
            {
                _logger.LogError($"Failed to create a resource on {Container!.Id} in partition \"{partKey}\". Exception: {e}");
                return TableActionResult.Error;
            }

        }

        /// <summary>
        /// Sets the partition key property if autogenerate is enabled on the model type
        /// </summary>
        /// <param name="item"></param>
        private void autoSetPartitionKey(T item)
        {
            if (_generateKeyFromId)
            {
                string partition = GeneratePartitionKeyValue((_idProperty!.GetValue(item)!.ToString())!);
                _partitionKeyProperty!.SetValue(item, partition);
            }
        }

        /// <summary>
        /// Deletes an item
        /// </summary>
        /// <param name="id"></param>
        /// <param name="partitionKey"></param>
        /// <returns></returns>
        public async Task<TableActionResult> DeleteItemAsync(string id, string? partitionKey = null)
        {
            PartitionKey partKey = partitionKey == null ? GeneratePartitionKey(id) : new PartitionKey(partitionKey);
            try
            {
                var response = await Container!.DeleteItemStreamAsync(id, partKey);
                if (response.IsSuccessStatusCode)
                {
                    return TableActionResult.Success;
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return TableActionResult.NotFound;
                }
                return TableActionResult.Error;
            }
            catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                return TableActionResult.NotFound;
            }
            catch (CosmosException e)
            {
                _logger.LogError($"Failed to delete a resource from {Container!.Id} with id \"{id}\" and key \"{partKey}\". Exception: {e}");
                return TableActionResult.Error;
            }
        }

        /// <summary>
        /// Gets an item
        /// </summary>
        /// <param name="id"></param>
        /// <param name="partitionKey"></param>
        /// <returns></returns>
        public async Task<TableActionResult<T>> GetItemAsync(string id, string? partitionKey = null)
        {
            PartitionKey partKey = partitionKey == null ? GeneratePartitionKey(id) : new PartitionKey(partitionKey);
            try
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                var streamResponseTask = Container!.ReadItemStreamAsync(id, partKey, null, cts.Token);
                using var streamResponse = await streamResponseTask;
                if (streamResponse == null || streamResponse.Content == null)
                {
                    if (streamResponse?.StatusCode == HttpStatusCode.NotFound)
                    {
                        return TableActionResult<T>.NotFound;
                    }
                    _logger.LogError($"Failed to retrieve a requested resource from {Container!.Id} with id \"{id}\" and key \"{partKey}\". Reason: {streamResponse?.StatusCode.ToString() ?? "null"}");
                    return TableActionResult<T>.Error;
                }
                T? result = await JsonSerializer.DeserializeAsync<T>(streamResponse.Content, new JsonSerializerOptions() { });
                if (result == null)
                {
                    _logger.LogError($"Failed to retrieve a requested resource from {Container.Id} with id \"{id}\" and key \"{partKey}\". Reason: JSON parsed to NULL");
                    return TableActionResult<T>.Error;
                }
                if (result is DBModelBase model)
                {
                    if (!model.CheckWellFormed())
                    {
                        _logger.LogError($"Failed to retrieve a requested resource from {Container.Id} with id \"{id}\" and key \"{partKey}\". Reason: Response was Malformed");
                        return TableActionResult<T>.Error;
                    }
                }

                return TableActionResult<T>.Success(result);
            }
            catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                return TableActionResult<T>.NotFound;
            }
            catch (CosmosException e)
            {
                _logger.LogError($"Failed to retrieve a requested resource from {Container!.Id} with id \"{id}\" and key \"{partKey}\". Exception: {e}");
                return TableActionResult<T>.Error;
            }
        }


        /// <summary>
        /// Represents a page of a query
        /// </summary>
        private struct QueryPage
        {
            public T[] Documents { get; set; }
            [JsonPropertyName("_count")]
            public int Count { get; set; }
        }

        /// <summary>
        /// Performs and flattens a query
        /// </summary>
        /// <param name="query">If amended all items are returned</param>
        /// <param name="partition">If specified only one partition is returned</param>
        /// <returns></returns>
        public async Task<TableActionResult<List<T>>> QueryItemsAsync(string? query = null, string? partition = null)
        {
            var options = new QueryRequestOptions();
            if (!string.IsNullOrEmpty(partition))
            {
                options.PartitionKey = new PartitionKey(partition);
            }

            try
            {
                List<T> results = new List<T>();
                using (FeedIterator resultSetIterator = Container!.GetItemQueryStreamIterator(
                    query,
                    requestOptions: options))
                {
                    while (resultSetIterator.HasMoreResults)
                    {
                        ResponseMessage response = await resultSetIterator.ReadNextAsync();
                        if (response.IsSuccessStatusCode)
                        {
                            QueryPage item = await JsonSerializer.DeserializeAsync<QueryPage>(response.Content);
                            if (item.Documents != null)
                            {
                                foreach (T document in item.Documents)
                                {
                                    if (!(document is DBModelBase model) || model.CheckWellFormed())
                                    {
                                        results.Add(document);
                                    }
                                }
                            }
                        }
                    }
                }
                return TableActionResult<List<T>>.Success(results);
            }
            catch (CosmosException e)
            {
                _logger.LogError($"Exception while iterating container {Container!.Id} with query {query} in partition {partition ?? "null"}. Exception {e}");
                return TableActionResult<List<T>>.Error;
            }
        }

        /// <summary>
        /// Replaces an existing itme
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public async Task<TableActionResult> ReplaceItemAsync(T item)
        {
            autoSetPartitionKey(item);
            string id = GetId(item);
            PartitionKey partKey = GetPartitionKey(item);
            try
            {
                using MemoryStream stream = new MemoryStream(8192);
                using var jsonwriter = new Utf8JsonWriter(stream);
                JsonSerializer.Serialize(jsonwriter, item);
                stream.Position = 0;
                CancellationTokenSource cts = new CancellationTokenSource();
                var responseTask = Container!.ReplaceItemStreamAsync(stream, id, partKey, null, cts.Token);
                using var response = await responseTask;
                if (response.IsSuccessStatusCode)
                {
                    return TableActionResult.Success;
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return TableActionResult.NotFound;
                }
                else
                {
                    return TableActionResult.Error;
                }
            }
            catch (CosmosException e)
            {
                _logger.LogError($"Failed to replace a resource from {Container!.Id} with id \"{id}\" and key \"{partKey}\". Exception: {e}");
                return TableActionResult.Error;
            }
        }
    }
}
