﻿using BlazorChat.Shared;
using Blazored.LocalStorage;
using System.Text.Json.Serialization;

namespace BlazorChat.Client.Services
{
    public sealed partial class LocalCacheService : ILocalCacheService, IDisposable
    {
        private readonly IServiceScope Scope;
        private readonly ILocalStorageService Storage;
        private readonly CacheCollection<Channel> Channels;
        private readonly CacheCollection<User> Users;
        private readonly CacheCollection<Message> Messages;

        private const int MAX_CACHE = 1024;

        public LocalCacheService(IServiceScopeFactory scopeFactory)
        {
            this.Scope = scopeFactory.CreateScope();
            this.Storage = Scope.ServiceProvider.GetRequiredService<ILocalStorageService>();
            this.Channels = new CacheCollection<Channel>(this, "channel");
            this.Users = new CacheCollection<User>(this, "user");
            this.Messages = new CacheCollection<Message>(this, "message");
            _ = Task.Run(Init);
        }

        public async Task Init()
        {
            await Task.WhenAll(Users.Init(), Messages.Init(), Channels.Init());
            await EnforceMax();
        }

        public struct CachedObj<T> where T : ItemBase
        {
            [JsonPropertyName("ts")]
            public long Timestamp { get; set; }
            [JsonPropertyName("v")]
            public T? Value { get; set; }
        }

        public ValueTask<IReadOnlyCollection<Channel>> CachedChannels => this.Channels.GetAll();

        public ValueTask<IReadOnlyCollection<User>> CachedUsers => this.Users.GetAll();

        public async ValueTask<IReadOnlyCollection<Message>> CachedMessages(ItemId channelId)
        {
            var allMessages = await Messages.GetAll();
            List<Message> filtered = new List<Message>();
            foreach (var message in allMessages)
            {
                if (message.ChannelId == channelId)
                {
                    filtered.Add(message);
                }
            }
            return filtered;
        }

        public async ValueTask UpdateItem(Channel channel)
        {
            await Channels.Set(channel.Id, channel);
            await EnforceMax();
        }

        public async ValueTask UpdateItem(Message message)
        {
            await Messages.Set(message.Id, message);
            await EnforceMax();
        }

        public async ValueTask UpdateItem(User user)
        {
            user.Online = false;
            await Users.Set(user.Id, user);
            await EnforceMax();
        }

        private async ValueTask EnforceMax()
        {
            int sum = Messages.Count + Channels.Count + Users.Count;
            while (sum > MAX_CACHE)
            {
                if (Messages.OldestTimestamp > Channels.OldestTimestamp && Messages.OldestTimestamp > Users.OldestTimestamp)
                {
                    await Messages.DeleteOldest();
                }
                else if (Channels.OldestTimestamp > Users.OldestTimestamp)
                {
                    await Channels.DeleteOldest();
                }
                else
                {
                    await Users.DeleteOldest();
                }
            }
        }

        public async Task Maintain(Channel[] channels)
        {
            // Check if some channels in the cache are no longer available
            var knownChannels = new HashSet<ItemId>();
            foreach (var channel in channels)
            {
                knownChannels.Add(channel.Id);
            }
            var channelsToRemove = new List<Channel>();
            {
                var cachedChannels = await CachedChannels;
                foreach (var channel in cachedChannels)
                {
                    if (!knownChannels.Contains(channel.Id))
                    {
                        channelsToRemove.Add(channel);
                    }
                }
            }

            // Remove no longer valid channels from cache
            foreach (var channel in channelsToRemove)
            {
                var messages = await CachedMessages(channel.Id);
                foreach (var message in messages)
                {
                    await Messages.Remove(message.Id);
                }
                foreach (var participation in channel.Participants)
                {
                    bool stillSharesChannel = false;
                    foreach (var channel2 in channels)
                    {
                        if (channel2.Participants.Any(p => p.Id == participation.Id))
                        {
                            stillSharesChannel = true;
                            break;
                        }
                    }
                    if (!stillSharesChannel)
                    {
                        await Users.Remove(participation.Id);
                    }
                }
                await Channels.Remove(channel.Id);
            }
        }

        public void Dispose()
        {
            Scope.Dispose();
        }

        public async Task ClearCache()
        {
            Messages.Clear();
            Users.Clear();
            Channels.Clear();
            await Storage.ClearAsync();
        }

        public Task<Session?> GetOfflineSession()
        {
            return Storage.GetItemAsync<Session?>("offlinesession").AsTask();
        }

        public Task SetOfflineSession(Session session)
        {
            return Storage.SetItemAsync("offlinesession", session).AsTask();
        }

        public Task SetQueuedMessages(IReadOnlyCollection<MessageDispatchProcess> processes)
        {
            return Storage.SetItemAsync("messagequeue", processes).AsTask();
        }

        public async Task<MessageDispatchProcess[]> GetQueuedMessages()
        {
            if (await Storage.ContainKeyAsync("messagequeue"))
            {
                return await Storage.GetItemAsync<MessageDispatchProcess[]>("messagequeue");
            }
            return Array.Empty<MessageDispatchProcess>();
        }
    }
}
