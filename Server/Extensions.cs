using CustomBlazorApp.Server.Services;
using CustomBlazorApp.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.Cosmos;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CustomBlazorApp
{
    public static class CosmosDBExtensions
    {
        /// <summary>
        /// Reads a single item from the <paramref name="container"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="container"></param>
        /// <param name="id"></param>
        /// <param name="key"></param>
        /// <returns>Value if the item existed and no JSON parse error occured. Null otherwise</returns>
        public static async Task<T?> ReadSystemJSONAsync<T>(this Container container, string id, PartitionKey key)
        {
            try
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                var streamResponseTask = container.ReadItemStreamAsync(id, key, null, cts.Token);
                //var timeoutTask = Task.Delay(TimeSpan.FromSeconds(1));
                //await Task.WhenAny(streamResponseTask, timeoutTask);
                //if (timeoutTask.IsCompleted)
                //{
                //    cts.Cancel();
                //    Console.Error.WriteLine("Request timed out!");
                //    Trace.Assert(false, "timeout");
                //    return default;
                //}
                using var streamResponse = await streamResponseTask;
                if (streamResponse == null || streamResponse.Content == null)
                {
                    Console.WriteLine($"Failed to retrieve a requested resource from {container.Id} with id \"{id}\" and key \"{key}\". Reason: {streamResponse?.StatusCode.ToString() ?? "null"}");
                    return default;
                }
                T? result = await JsonSerializer.DeserializeAsync<T>(streamResponse.Content, new JsonSerializerOptions() { });
                if (result == null)
                {
                    Console.WriteLine($"Failed to retrieve a requested resource from {container.Id} with id \"{id}\" and key \"{key}\". Reason: JSON parsed to NULL");
                    return default;
                }
                return result;
            }
            catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }
        }

        /// <summary>
        /// Creates a new item
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="container"></param>
        /// <param name="item"></param>
        /// <param name="key"></param>
        /// <returns>True if a success code is returned</returns>
        public static async Task<bool> CreateSystemJSONAsync<T>(this Container container, T item, PartitionKey key)
        {
            try
            {
                using MemoryStream stream = new MemoryStream(8192);
                using var jsonwriter = new Utf8JsonWriter(stream);
                JsonSerializer.Serialize(jsonwriter, item);
                stream.Position = 0;
                CancellationTokenSource cts = new CancellationTokenSource();
                var responseTask = container.CreateItemStreamAsync(stream, key, null, cts.Token);
                //var timeoutTask = Task.Delay(TimeSpan.FromSeconds(1));
                //await Task.WhenAny(responseTask, timeoutTask);
                //if (timeoutTask.IsCompleted)
                //{
                //    cts.Cancel();
                //    Console.Error.WriteLine("Request timed out!");
                //    Trace.Assert(false, "timeout");
                //    return false;
                //}
                using var response = await responseTask;
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to create a resource on {container.Id} in partition \"{key}\". Reason: {response.StatusCode}");
                }
                return response.IsSuccessStatusCode;
            }
            catch (CosmosException)
            {
                return false;
            }
        }

        /// <summary>
        /// Replaces an item
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="container"></param>
        /// <param name="id"></param>
        /// <param name="key"></param>
        /// <param name="item"></param>
        /// <returns>True if succeeds</returns>
        public static async Task<bool> ReplaceSystemJSONAsyc<T>(this Container container, string id, PartitionKey key, T item)
        {
            try
            {
                using MemoryStream stream = new MemoryStream(8192);
                using var jsonwriter = new Utf8JsonWriter(stream);
                JsonSerializer.Serialize(jsonwriter, item);
                stream.Position = 0;
                CancellationTokenSource cts = new CancellationTokenSource();
                var responseTask = container.ReplaceItemStreamAsync(stream, id, key, null, cts.Token);
                //var timeoutTask = Task.Delay(TimeSpan.FromSeconds(1));
                //await Task.WhenAny(responseTask, timeoutTask);
                //if (timeoutTask.IsCompleted)
                //{
                //    cts.Cancel();
                //    Console.Error.WriteLine("Request timed out!");
                //    Trace.Assert(false, "timeout");
                //    return false;
                //}
                using var response = await responseTask;
                return response.IsSuccessStatusCode;
            }
            catch (CosmosException)
            {
                return false;
            }
        }

        /// <summary>
        /// Represents a page of a query
        /// </summary>
        struct QueryPage<T>
        {
            public T[] Documents { get; set; }
            [JsonPropertyName("_count")]
            public int Count { get; set; }
        }

        /// <summary>
        /// Flattens
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="container"></param>
        /// <param name="query"></param>
        /// <param name="partitionKey"></param>
        /// <returns></returns>
        public static async Task<List<T>?> FlattenQuery<T>(this Container container, QueryDefinition query, PartitionKey partitionKey)
        {
            try
            {
                List<T> results = new List<T>();
                using (FeedIterator resultSetIterator = container.GetItemQueryStreamIterator(
                    query,
                    requestOptions: new QueryRequestOptions()
                    {
                        PartitionKey = partitionKey
                    }))
                {
                    while (resultSetIterator.HasMoreResults)
                    {
                        ResponseMessage response = await resultSetIterator.ReadNextAsync();
                        if (response.IsSuccessStatusCode)
                        {
                            QueryPage<T> item = await JsonSerializer.DeserializeAsync<QueryPage<T>>(response.Content);
                            if (item.Documents != null)
                            {
                                results.AddRange(item.Documents);
                            }
                        }
                    }
                }
                return results;
            }
            catch (CosmosException)
            {
                return null;
            }
        }

        public static async Task<List<T>?> FlattenQuery<T>(this Container container)
        {
            try
            {
                List<T> results = new List<T>();
                QueryDefinition query = new QueryDefinition("Select * from i");
                using (FeedIterator resultSetIterator = container.GetItemQueryStreamIterator(query))
                {
                    while (resultSetIterator.HasMoreResults)
                    {
                        ResponseMessage response = await resultSetIterator.ReadNextAsync();
                        QueryPage<T> item = await JsonSerializer.DeserializeAsync<QueryPage<T>>(response.Content);
                        if (item.Documents != null)
                        {
                            results.AddRange(item.Documents);
                        }
                    }
                }
                return results;
            }
            catch (CosmosException)
            {
                return null;
            }
        }

    }
    public static class OtherExtensions { 

        public static async Task<Session> SigninSession(this HttpContext context, TimeSpan? expireDelay, Shared.User user)
        {
            // Create claim (UserId)
            Claim claim = new Claim(ClaimTypes.NameIdentifier, user.Id.ToString());
            // Create claimsIdentity
            ClaimsIdentity claimsIdentity = new ClaimsIdentity(new[] { claim }, "serverAuth");
            // Create claimPrincipal
            ClaimsPrincipal claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
            DateTimeOffset expires = DateTimeOffset.UtcNow + TimeSpan.FromHours(8);
            if (expireDelay.HasValue)
            {
                TimeSpan max = TimeSpan.FromDays(2);
                TimeSpan min = TimeSpan.FromMinutes(5);
                expireDelay = TimeSpan.FromMinutes(Math.Clamp(expireDelay.Value.TotalMinutes, min.TotalMinutes, max.TotalMinutes));
                expires = DateTimeOffset.UtcNow + expireDelay.Value;
            }
            await AuthenticationHttpContextExtensions.SignInAsync(context, claimsPrincipal, new AuthenticationProperties() { ExpiresUtc = expires });
            return new Session(expires, user);
        }

        public class LockedEnumerable<T> : IEnumerable<T>, IDisposable
        {
            private IEnumerable<T> _Enumerable;
            private SemaphoreAccessDisposable _SemaphoreAccess;
            public LockedEnumerable(IEnumerable<T> enumerable, SemaphoreAccessDisposable semaphoreAccess)
            {
                _Enumerable = enumerable;
                _SemaphoreAccess = semaphoreAccess;
            }

            public void Dispose()
            {
                ((IDisposable)_SemaphoreAccess).Dispose();
                GC.SuppressFinalize(this);
            }

            public IEnumerator<T> GetEnumerator()
            {
                return _Enumerable.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable)_Enumerable).GetEnumerator();
            }
        }

        public static async Task<LockedEnumerable<T>> AsLockedEnumerable<T>(this IEnumerable<T> enumerable, SemaphoreSlim semaphore)
        {
            var semaphoreAccess = await semaphore.WaitAsyncDisposable();
            return new LockedEnumerable<T>(enumerable, semaphoreAccess);
        }
    }
}