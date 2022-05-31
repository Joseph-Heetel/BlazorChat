using BlazorChat.Server.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Scripts;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace BlazorChat.Server.Services
{
    /// <summary>
    /// Struct describing properties of a database table
    /// </summary>
    public struct Table
    {
        /// <summary>
        /// Id of the table within the database
        /// </summary>
        public string Id { get; } = String.Empty;
        /// <summary>
        /// Path to the partition value
        /// </summary>
        public string PartitionKey { get; } = String.Empty;
        /// <summary>
        /// Type stored within the table
        /// </summary>
        public Type AssociatedType { get; } = typeof(int);
        public int PartitionKeyModulo { get; } = 0;
        public Table() { }
        public Table(string id, string partitionKey, Type associatedType, int partitionKeyModulo)
        {
            Id = id;
            PartitionKey = partitionKey;
            AssociatedType = associatedType;
            PartitionKeyModulo = partitionKeyModulo;
        }
        public static Table Create<T>(string id, string partitionKey, int partitionKeyModulo = 0)
        {
            return new Table(id, partitionKey, typeof(T), partitionKeyModulo);
        }
    }

    /// <summary>
    /// Service which exposes methods for interacting with a database
    /// </summary>
    public interface IDatabaseBackendService
    {
        /// <summary>
        /// Checks for table existence. If does not exist, will create it.
        /// </summary>
        public Task CreateTableIfNotExists(Table table);
        /// <summary>
        /// Returns item of type <typeparamref name="T"/> identified by <paramref name="key"/> and <paramref name="partitionkey"/> in <paramref name="table"/>. If no match is found, null is returned.
        /// </summary>
        /// <remarks>
        /// Asserts that <paramref name="table"/> is known and that <see cref="Table.AssociatedType"/> matches <typeparamref name="T"/>
        /// </remarks>
        public Task<T?> GetItem<T>(Table table, string key, object? partitionkey = default);
        /// <summary>
        /// Returns all items in <paramref name="table"/>. If <paramref name="query"/> (and <paramref name="partitionkey"/>) are set, items returned are filtered.
        /// </summary>
        public Task<List<T>> QueryItems<T>(Table table, string? query = default, object? partitionkey = default);
        /// <summary>
        /// Creates a new <paramref name="item"/> in <paramref name="table"/>. Returns success.
        /// </summary>
        public Task<bool> CreateItem<T>(Table table, T item, object? partitionkey = default);
        /// <summary>
        /// Replaces an existing item identified by <paramref name="key"/> and <paramref name="partitionkey"/> with <paramref name="item"/>. Returns success.
        /// </summary>
        public Task<bool> ReplaceItem<T>(Table table, T item, string key, object? partitionkey = default);
        /// <summary>
        /// Removes an item identified by <paramref name="key"/> (and <paramref name="partitionkey"/>). Returns success.
        /// </summary>
        public Task<bool> RemoveItem(Table table, string key, object? partitionkey = default);
        public Task<T> GetTableObject<T>(Table table) where T : class;
        public Task<bool> RunBulkDelete(Table table, string query, object? partitionkey = null);
    }

    public class CosmosDatabaseBackendService : IDatabaseBackendService
    {
        private ConcurrentDictionary<string, Container> _knownTables = new ConcurrentDictionary<string, Container>();
        private const string DATABASEID = "CustomBlazorChatDBv4";
        private const string BULKDELETESPROC = "bulkDeleteSproc";
        private static readonly FileInfo s_bulkDeleteJSSource = new FileInfo(Path.Combine(Environment.CurrentDirectory, "CosmosDBProcedures/bulkDeleteSproc.js"));

        private CosmosClient? _cosmosClient;
        private Database? _database;

        public CosmosDatabaseBackendService()
        {
            InitializeAsync().Wait();
        }
        public async Task InitializeAsync()
        {
            // The Azure Cosmos DB endpoint for running this sample.
            const string EndpointUri = "https://chat-data-serverless.documents.azure.com:443/";
            // The primary key for the Azure Cosmos account.
            string? PrimaryKey = Environment.GetEnvironmentVariable("CosmosDB_PrimaryKey");
            Debug.Assert(PrimaryKey != null);
            try
            {
                this._cosmosClient = new CosmosClient(EndpointUri, PrimaryKey);
                //this._DocumentClient = new DocumentClient(new Uri(EndpointUri), PrimaryKey);
                this._database = await this._cosmosClient.CreateDatabaseIfNotExistsAsync(DATABASEID);
            }
            catch (CosmosException de)
            {
                Exception baseException = de.GetBaseException();
                Console.WriteLine("{0} error occurred: {1}", de.StatusCode, de);
            }
        }

        public async Task AddBulkDeleteSProc(params Table[] tables)
        {
            if (!s_bulkDeleteJSSource.Exists)
            {
                return;
            }

            string source = string.Empty;
            {
                using var fileStream = s_bulkDeleteJSSource.OpenRead();
                using var reader = new StreamReader(fileStream);
                source = await reader.ReadToEndAsync();
            }
            List<Task> tasks = new List<Task>();
            foreach (Table table in tables)
            {
                Container container = GetContainer(table);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var response = await container.Scripts.ReadStoredProcedureAsync(BULKDELETESPROC);
                        if (response?.StatusCode == HttpStatusCode.OK)
                        {
                            return;
                        }
                    } catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                    {

                    }
                    var properties = new StoredProcedureProperties()
                    {
                        Body = source,
                        Id = BULKDELETESPROC,
                    };
                    await container.Scripts.CreateStoredProcedureAsync(properties);
                }));
            }
            await Task.WhenAll(tasks);
        }

        private Container GetContainer(Table table)
        {
            Trace.Assert(_knownTables.TryGetValue(table.Id, out Container? container));
            return container!;
        }

        private Container GetContainer<T>(Table table)
        {
            Trace.Assert(typeof(T) == table.AssociatedType);
            Trace.Assert(_knownTables.TryGetValue(table.Id, out Container? container));
            return container!;
        }

        public Task<T> GetTableObject<T>(Table table) where T : class
        {
            return Task.FromResult((GetContainer(table) as T)!);
        }

        public async Task CreateTableIfNotExists(Table table)
        {
            if (_knownTables.ContainsKey(table.Id))
            {
                return;
            }
            Trace.Assert(_database != null);
            Trace.Assert(!string.IsNullOrEmpty(table.Id) && !string.IsNullOrEmpty(table.PartitionKey));
            Container container = await _database!.CreateContainerIfNotExistsAsync(table.Id, table.PartitionKey);
            Trace.Assert(_knownTables.TryAdd(table.Id, container));
        }

        public async Task<T?> GetItem<T>(Table table, string key, object? partitionkey = null)
        {
            Container container = GetContainer<T>(table);
            PartitionKey? partKey = (PartitionKey?)partitionkey;
            Trace.Assert(partKey.HasValue);
            return await container.ReadSystemJSONAsync<T>(key, partKey!.Value);
        }

        public async Task<List<T>> QueryItems<T>(Table table, string? query = default, object? partitionkey = null)
        {
            Container container = GetContainer<T>(table);
            PartitionKey? partKey = (PartitionKey?)partitionkey;
            if (query != null)
            {
                Trace.Assert(partKey!.HasValue);
                return (await container.FlattenQuery<T>(new QueryDefinition(query), partKey.Value)) ?? new List<T>();
            }
            else
            {
                return (await container.FlattenQuery<T>()) ?? new List<T>();
            }
        }

        public async Task<bool> CreateItem<T>(Table table, T item, object? partitionkey = null)
        {
            Container container = GetContainer<T>(table);
            PartitionKey? partKey = (PartitionKey?)partitionkey;
            Trace.Assert(partKey!.HasValue);
            if (item is DBModelBase model)
            {
                Trace.Assert(model.CheckWellFormed());
            }
            return await container.CreateSystemJSONAsync<T>(item, partKey.Value);
        }

        public async Task<bool> ReplaceItem<T>(Table table, T item, string key, object? partitionkey = null)
        {
            Container container = GetContainer<T>(table);
            PartitionKey? partKey = (PartitionKey?)partitionkey;
            Trace.Assert(partKey.HasValue);
            if (item is DBModelBase model)
            {
                Trace.Assert(model.CheckWellFormed());
            }
            return await container.ReplaceSystemJSONAsyc<T>(key, partKey!.Value, item);
        }

        public async Task<bool> RemoveItem(Table table, string key, object? partitionkey = null)
        {
            Container container = GetContainer(table);
            PartitionKey? partKey = (PartitionKey?)partitionkey;
            Trace.Assert(partKey.HasValue);
            return (await container.DeleteItemStreamAsync(key, partKey!.Value)).IsSuccessStatusCode;
        }

        public class BulkDeleteResponse
        {
            public int deleted { get; set; }
            public bool continuation { get; set; }
        }

        public async Task<bool> RunBulkDelete(Table table, string query, object? partitionkey = null)
        {
            Container container = GetContainer(table);
            PartitionKey? partKey = (PartitionKey?)partitionkey;
            Trace.Assert(partKey.HasValue);
            var response = await container.Scripts.ExecuteStoredProcedureStreamAsync(BULKDELETESPROC, partKey!.Value, new dynamic[] { query });
            return response.IsSuccessStatusCode;
        }
    }
}
