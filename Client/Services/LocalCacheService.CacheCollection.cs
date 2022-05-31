using BlazorChat.Shared;
using System.Text.Json;

namespace BlazorChat.Client.Services
{

    public partial class LocalCacheService
    {
        private class CacheCollection<T> where T : ItemBase
        {
            private readonly LocalCacheService Cache;
            private readonly string Tag;
            private ItemId OldestValue;
            public long OldestTimestamp{ get; private set; }
            public int Count => Data.Count;

            private readonly Dictionary<ItemId, CachedObj<T>> Data = new Dictionary<ItemId, CachedObj<T>>();

            public CacheCollection(LocalCacheService cache, string tag)
            {
                this.Cache = cache;
                this.Tag = $"{tag}-";
            }

            public async Task Init()
            {
                Data.Clear();
                IEnumerable<string> keys = await Cache.Storage.KeysAsync() ?? Array.Empty<string>();
                List<Task<Tuple<string,CachedObj<T>>>> tasks = new();

                foreach (string key in keys)
                {
                    if (key.StartsWith(Tag))
                    {
                        tasks.Add(Task.Run(async () =>
                        {
                            var item = await Cache.Storage.GetItemAsync<CachedObj<T>>(key);
                            return new Tuple<string, CachedObj<T>>(key, item);
                        }));
                    }
                }
                await Task.WhenAll(tasks);

                foreach (var task in tasks)
                {
                    string key = task.Result.Item1;
                    CachedObj<T> item = task.Result.Item2;
                    if (item.Value != null)
                    {
                        Data.Add(item.Value.Id, item);
                    }
                    else
                    { // clean nonvalid entries
                        await Cache.Storage.RemoveItemAsync(key);
                    }
                }

                CachedObj<T> oldest = Data.Values.MinBy((CachedObj<T> val) => val.Timestamp);
                OldestValue = oldest.Value!.Id;
                OldestTimestamp = oldest.Timestamp;
            }

            public ValueTask<IReadOnlyCollection<T>> GetAll()
            {
                List<T> list = new List<T>(Data.Values.Select(cacheobj => cacheobj.Value!));
                return ValueTask.FromResult<IReadOnlyCollection<T>>(list);
            }

            public async ValueTask Set(ItemId id, T value)
            {
                var data = new CachedObj<T>()
                {
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Value = value
                };
                await Cache.Storage.SetItemAsync(MakeKey(id), data);
                Data[id] = data;
            }

            public ValueTask<Result<T>> Get(ItemId id)
            {
                if (Data.TryGetValue(id, out var data))
                {
                    return ValueTask.FromResult(new Result<T>() { Success = true, Value = data.Value });
                }
                return ValueTask.FromResult(new Result<T>() { Success = false, Value = default });
            }

            public async ValueTask DeleteOldest()
            {
                if (OldestValue != default)
                {
                    await Remove(OldestValue);
                }

                if (Data.Count > 0)
                {
                    CachedObj<T> oldest = Data.Values.MinBy((CachedObj<T> val) => val.Timestamp);
                    OldestValue = oldest.Value!.Id;
                    OldestTimestamp = oldest.Timestamp;
                }
            }

            public async ValueTask Remove(ItemId id)
            {
                Data.Remove(id);
                await Cache.Storage.RemoveItemAsync(MakeKey(id));
            }

            private string MakeKey(ItemId id)
            {
                return $"{Tag}{id}";
            }

            public void Clear()
            {
                Data.Clear();
            }
        }
    }
}
