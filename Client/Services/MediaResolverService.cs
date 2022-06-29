using BlazorChat.Shared;

namespace BlazorChat.Client.Services
{
    /// <summary>
    /// Class helping to maintain a list of subscribers and url state for a remote media object
    /// </summary>
    public class ObservedMedia
    {
        public event Action<string>? UriChanged;
        public string Uri = "";
        public DateTimeOffset Expires { get; private set; }
        public FileAttachment Attachment { get; }
        public ItemId DomainId { get; }
        public bool UseAvatarEndpoint { get; }

        public ObservedMedia(ItemId domainId, FileAttachment attachment, bool avatar)
        {
            DomainId = domainId;
            Attachment = attachment;
            Expires = DateTimeOffset.MinValue;
            UseAvatarEndpoint = avatar;
        }

        public async Task<bool> Resolve(IChatApiService apiservice)
        {
            TemporaryURL? tempUrl;
            if (UseAvatarEndpoint)
            {
                tempUrl = await apiservice.GetTemporaryAvatarURL(DomainId, Attachment);
            }
            else
            {
                tempUrl = await apiservice.GetTemporaryURL(DomainId, Attachment);
            }
            if (tempUrl == null)
            {
                // No ok server response for temporary url request: retry after 30 seconds
                Expires = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
                return false;
            }
            Uri = tempUrl.Url;
            UriChanged?.Invoke(Uri);
            Expires = tempUrl.Expires;
            return true;
        }

        public int Subscribers { get => UriChanged?.GetInvocationList().Length ?? 0; }
    }

    /// <summary>
    /// Maintains a list of media objects for which temporary fileattachment objects are maintained (and subscribers updated when these get refreshed)
    /// </summary>
    public interface IMediaResolverService
    {
        /// <summary>
        /// Subscribes <paramref name="callback"/> to updates described by <paramref name="domainId"/> and <paramref name="attachment"/>. 
        /// Automatically initiates fetching of a new temporary url, calling <paramref name="callback"/> as soon as it's available
        /// </summary>
        /// <returns>An empty string or a temporary url, if the media object was already resolved previously</returns>
        public string GetAndSubscribe(ItemId domainId, FileAttachment attachment, Action<string> callback, bool isAvatar = false);
        /// <summary>
        /// Removes <paramref name="callback"/> from listening to updates to the attachment identified by <paramref name="attachmentId"/>
        /// </summary>
        public void Unsubscribe(ItemId attachmentId, Action<string> callback);
    }

    public class MediaResolverService : IMediaResolverService, IDisposable
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly IChatApiService _chatApiService;
        private readonly IDictionary<ItemId, ObservedMedia> _media = new Dictionary<ItemId, ObservedMedia>();

        public MediaResolverService(IChatApiService chatApiService)
        {
            _chatApiService = chatApiService;
            _ = Task.Run(async () =>
            {
                // Loops until service is disposed:
                // Checks any media for expired temporary links. If there are any, resolves these (which in turn triggers UI)
                while (!_cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), _cts.Token);


                    List<Task> resolveTasks = new List<Task>();
                    DateTimeOffset now = DateTimeOffset.UtcNow;
                    foreach (var media in _media.Values)
                    {
                        if (now > media.Expires)
                        {
                            resolveTasks.Add(media.Resolve(_chatApiService));
                        }
                    }
                    await Task.WhenAll(resolveTasks);
                }
            }, _cts.Token);
        }

        public void Dispose()
        {
            _cts.Dispose();
        }

        public string GetAndSubscribe(ItemId domainId, FileAttachment attachment, Action<string> callback, bool avatar = false)
        {
            if (_media.TryGetValue(attachment.Id, out ObservedMedia? value))
            {
                value.UriChanged += callback;
                return value.Uri;
            }
            else
            {
                ObservedMedia media = new ObservedMedia(domainId, attachment, avatar);
                media.UriChanged += callback;
                _media.Add(attachment.Id, media);
                return "";
            }
        }

        public void Unsubscribe(ItemId attachmentId, Action<string> callback)
        {
            if (_media.TryGetValue(attachmentId, out ObservedMedia? value))
            {
                value.UriChanged -= callback;
                if (value.Subscribers == 0)
                {
                    _media.Remove(attachmentId);
                }
            }
        }
    }
}
