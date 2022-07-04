using BlazorChat.Server.Models;
using BlazorChat.Shared;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Scripts;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace BlazorChat.Server.Services.DatabaseWrapper
{
    /// <summary>
    /// Singleton service managing the connection to cosmos db
    /// </summary>
    public class CosmosDatabaseConnection : IDatabaseConnection
    {
        public const string BULKDELETESPROC = "bulkDeleteSproc";
        private const string DATABASEID = "BlazorChatDBv6";

        private CosmosClient? _cosmosClient;
        private Database? _database;
        private readonly IServiceProvider _serviceProvider;


        private readonly ConcurrentDictionary<string, object> _containers = new ConcurrentDictionary<string, object>();

        public CosmosDatabaseConnection(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task Init()
        {
            try
            {
                var connectionString = Environment.GetEnvironmentVariable(EnvironmentVarKeys.AZURECOSMOSCONNECTIONSTRING);
                if (connectionString != null)
                {
                    this._cosmosClient = new CosmosClient(connectionString);
                    this._database = await this._cosmosClient.CreateDatabaseIfNotExistsAsync(DATABASEID);
                }
                Task<bool>[] containerInitTasks = new Task<bool>[]
                        {
                    InitContainer<LoginModel>(DatabaseConstants.LOGINSTABLE),
                    InitContainer<UserModel>(DatabaseConstants.USERSTABLE),
                    InitContainer<ChannelModel>(DatabaseConstants.CHANNELSTABLE),
                    InitContainer<MembershipModel>(DatabaseConstants.MEMBERSHIPSTABLE),
                    InitContainer<MessageModel>(DatabaseConstants.MESSAGESTABLE),
                    InitContainer<FormModel>(DatabaseConstants.FORMSTABLE),
                    InitContainer<FormRequestModel>(DatabaseConstants.FORMREQUESTSTABLE),
                    InitContainer<FormResponseModel>(DatabaseConstants.FORMRESPONSESTABLE),
                };
                await Task.WhenAll(containerInitTasks);
                if (containerInitTasks.Any(task => task.Result))
                {
                    await InitializeWithPlaceholderData();
                }
            }
            catch (CosmosException e)
            {
                Exception baseException = e.GetBaseException();
                Console.WriteLine("{0} error occurred: {1}", e.StatusCode, e);
            }
        }

        private async Task<bool> InitContainer<T>(string containerName)
        {
            CosmosTableService<T> table = new CosmosTableService<T>(_serviceProvider, containerName);
            bool createdNewContainer = false;
            if (this._database != null)
            {
                // Create/Get container
                ContainerResponse containerResponse = await this._database.CreateContainerIfNotExistsAsync(containerName, table.PartitionPath);
                createdNewContainer = containerResponse.StatusCode == HttpStatusCode.Created;
                Container container = containerResponse.Container;

                // Detect and (if needed) upload bulkdeletescript
                bool hasBulkDeleteScript = false;
                try
                {
                    var response = await container.Scripts.ReadStoredProcedureAsync(BULKDELETESPROC);
                    if (response?.StatusCode == HttpStatusCode.OK)
                    {
                        hasBulkDeleteScript = true;
                    }
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                { }
                if (!hasBulkDeleteScript)
                {
                    var properties = new StoredProcedureProperties()
                    {
                        Body = BulkDeleteScript.SOURCE,
                        Id = BULKDELETESPROC,
                    };
                    await container.Scripts.CreateStoredProcedureAsync(properties);
                }
                table.Container = container;
            }
            _containers.TryAdd(containerName, table);

            return createdNewContainer;
        }

        /// <summary>
        /// Initializes with some basic data
        /// </summary>
        /// <remarks>User login "testuser" pw "password"; Channel testchannel, system welcome message</remarks>
        /// <returns></returns>
        private async Task InitializeWithPlaceholderData()
        {
            var loginService = _serviceProvider.GetRequiredService<ILoginDataService>();
            var channelService = _serviceProvider.GetRequiredService<IChannelDataService>();
            var messageService = _serviceProvider.GetRequiredService<IMessageDataService>();

            using HashAlgorithm hash = HashAlgorithm.Create(HashAlgorithmName.SHA256.Name!)!;
            ByteArray hashedPassword = new ByteArray(hash.ComputeHash(Encoding.UTF8.GetBytes("password")));

            var user = await loginService.CreateUserAndLogin("BlazorChat Test User", "testuser", hashedPassword);
            var channel = await channelService.CreateChannel("Test Channel", new HashSet<ItemId>() { user!.Id });
            await messageService.CreateMessage(channel!.Id, ItemId.SystemId, "Welcome to Blazor Chat!", null);
        }

        public ITableService<T>? TryGetTable<T>(string name)
        {
            if (_containers.TryGetValue(name, out var table) && table is CosmosTableService<T> typedTable)
            {
                return typedTable;
            }
            return null;
        }
    }
}
