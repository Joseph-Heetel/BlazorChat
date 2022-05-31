using BlazorChat.Shared;
using Microsoft.AspNetCore.Components;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace BlazorChat.Client.Services
{

    public enum LoginState
    {
        /// <summary>
        /// The application hasn't attempted login or has completed logout
        /// </summary>
        NoInit,
        /// <summary>
        /// The application is attempting to login
        /// </summary>
        Connecting,
        /// <summary>
        /// The application is logged in
        /// </summary>
        Connected,
    }

    /// <summary>
    /// Service exposing chat api access. Manages state of login and session
    /// </summary>
    public interface IChatApiService
    {
        /// <summary>
        /// Represents the login state of the service
        /// </summary>
        public IReadOnlyObservable<LoginState> LoginState { get; }
        /// <summary>
        /// Information about the current session
        /// </summary>
        public IReadOnlyObservable<Session?> Session { get; }
        /// <summary>
        /// Currently logged in user
        /// </summary>
        public IReadOnlyObservable<User?> SelfUser { get; }
        /// <summary>
        /// Login given existing <paramref name="login"/>. Causes page reload on success (Auth state cannot update without).
        /// </summary>
        public Task<bool> Login(string login, string password);
        /// <summary>
        /// Register new user for <paramref name="login"/> with display name <paramref name="username"/>. Causes page reload on success (Auth state cannot update without).
        /// </summary>
        public Task<bool> Register(string login, string password, string username);
        /// <summary>
        /// Recovers an existing session
        /// </summary>
        public Task<bool> RecoverExistingSession();
        /// <summary>
        /// Logs out currently signed in user
        /// </summary>
        /// <returns></returns>
        public Task Logout();
        /// <summary>
        /// Fetches a single channel from the API
        /// </summary>
        /// <param name="id">Channel Id</param>
        /// <returns>Channel object if is found and no errors occured. Null otherwise</returns>
        public Task<Channel?> GetChannel(ItemId id);
        /// <summary>
        /// Fetches all channels available to the logged in user
        /// </summary>
        /// <returns>Array of channels. May return empty array if an error occurs.</returns>
        public Task<Channel[]> GetChannels();
        /// <summary>
        /// Fetches a single user from the API
        /// </summary>
        /// <param name="id">User Id</param>
        /// <returns>User object if is found and no errors occured. Null otherwise</returns>
        public Task<User?> GetUser(ItemId id);
        /// <summary>
        /// Fetches multiple users from the API
        /// </summary>
        /// <param name="ids">Collection of user Ids to fetch</param>
        /// <returns>Array of user objects. May return empty array if an error occurs.</returns>
        public Task<User[]> GetUsers(IReadOnlyCollection<ItemId> ids);
        Task<Message?> CreateMessage(ItemId channelId, string body = "", FileAttachment? attachment = null);
        /// <summary>
        /// Uploades a new file to channel <paramref name="channelId"/> with file body <paramref name="file"/>
        /// </summary>
        /// <returns>The chatmessage if successful</returns>
        public Task<FileAttachment?> UploadFile(ItemId channelId, IBrowserFile file);
        public Task<bool> UploadAvatar(IBrowserFile file);
        public Task<bool> UpdateUsername(string username);
        public Task<TemporaryURL?> GetTemporaryURL(ItemId channelId, FileAttachment attachment);
        /// <summary>
        /// Get a list of chatmessages
        /// </summary>
        /// <param name="channelId">Channel to pull from</param>
        /// <param name="reference">Which inclusive timestamp to post the query relative to</param>
        /// <param name="older">if true, older messages than <paramref name="reference"/> are pulled, newer otherwise</param>
        /// <param name="limit">How many messages to return at most</param>
        /// <returns>Result of query</returns>
        public Task<Message[]> GetMessages(ItemId channelId, DateTimeOffset reference, bool older, int limit);
        /// <summary>
        /// Conducts a message search according to <paramref name="query"/>s parameters
        /// </summary>
        /// <returns>An array of matching messages (may be empty)</returns>
        public Task<Message[]> SearchMessages(MessageSearchQuery query);
        /// <summary>
        /// Sends a read horizon update to the server
        /// </summary>
        /// <param name="channelId">Channel Id to update read horizon in</param>
        /// <param name="timestamp">Timestamp to advance read horizon to</param>
        /// <returns>True if successful</returns>
        public Task<bool> UpdateReadHorizon(ItemId channelId, DateTimeOffset timestamp);
        /// <summary>
        /// Retrieves a single message from the server
        /// </summary>
        /// <returns>Hit or null on failure</returns>
        public Task<Message?> GetMessage(ItemId channelId, ItemId messageId);
        /// <summary>
        /// Inits a new call
        /// </summary>
        /// <param name="calleeId">User to be called</param>
        /// <returns>Call Id. Check ItemId.IsZero to determine operation success!</returns>
        public Task<ItemId> InitCall(ItemId calleeId);
        /// <summary>
        /// Get a list of pending calls 
        /// </summary>
        public Task<PendingCall[]> GetCalls();
        /// <summary>
        /// Elevates a call to active status
        /// </summary>
        /// <param name="callId"></param>
        /// <returns></returns>
        public Task<bool> ElevateCall(ItemId callId);
        /// <summary>
        /// Terminates an active or pending call
        /// </summary>
        /// <param name="callID"></param>
        /// <returns></returns>
        public Task<bool> TerminateCall(ItemId callID);
        /// <summary>
        /// Get a form
        /// </summary>
        /// <param name="formId"></param>
        /// <returns></returns>
        public Task<JsonDocument?> GetForm(ItemId formId);
        /// <summary>
        /// Get a form request
        /// </summary>
        /// <param name="requestId"></param>
        /// <returns></returns>
        public Task<FormRequest?> GetFormRequest(ItemId requestId);
        /// <summary>
        /// Post a form response to the server
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        public Task<bool> PostFormResponse(ItemId requestId, JsonNode response);
    }

    public class ChatApiService : IChatApiService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        private HttpClient getHttpClient()
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_navManager.BaseUri);
            return client;
        }
        private readonly HashAlgorithm _hashAlgorithm;
        private readonly NavigationManager _navManager;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly ILocalCacheService _cacheService;

        private Observable<LoginState> _loginState { get; } = new Observable<LoginState>(Services.LoginState.NoInit);
        private Observable<Session?> _session { get; } = new Observable<Session?>(null);
        private Observable<User?> _selfUser { get; } = new Observable<User?>(null);
        public IReadOnlyObservable<LoginState> LoginState => _loginState;
        public IReadOnlyObservable<Session?> Session => _session;
        public IReadOnlyObservable<User?> SelfUser => _selfUser;

        public ChatApiService(IHttpClientFactory httpClientFactory, NavigationManager navManager, AuthenticationStateProvider auth, ILocalCacheService cache)
        {
            this._httpClientFactory = httpClientFactory;
            HashAlgorithm? hash = HashAlgorithm.Create(HashAlgorithmName.SHA256.Name!);
            Trace.Assert(hash != null);
            this._hashAlgorithm = hash!;
            this._navManager = navManager;
            this._authStateProvider = auth;
            this._cacheService = cache;
            _ = Task.Run(async () =>
            {
                var state = await this._authStateProvider.GetAuthenticationStateAsync();
                if (state != null && !string.IsNullOrEmpty(state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value))
                {
                    await RecoverExistingSession();
                }
            });
        }

        private ByteArray GeneratePasswordHash(string password)
        {
            // This is bad practice, as a hash like this could easily be cross referenced
            // with other hashes of passwords the user used in the past, recovered from previous data leaks.
            // A cryptographic hash function with a unique init vector should be used instead. Doing so on the server
            // side is probably sufficiently secure (This still leaves client <-> server communication as a potential weakness).
            return new ByteArray(_hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(password)));
        }

        public async Task<bool> Login(string login, string password)
        {
            this._loginState.State = Services.LoginState.Connecting;
            LoginRequest request = new LoginRequest() { Login = login, PasswordHash = GeneratePasswordHash(password) };
            var response = await getHttpClient().PostGetFromJSONAsync<LoginRequest, Session>("/api/session/login", request);
            if (response != null)
            {
                _navManager.NavigateTo("chat", true); // In order for auth state to update, the page needs to reload
                return true;
            }
            this._loginState.State = Services.LoginState.NoInit;
            return false;
        }

        public async Task<bool> Register(string login, string password, string username)
        {
            this._loginState.State = Services.LoginState.Connecting;
            var request = new RegisterRequest() { Login = login, PasswordHash = GeneratePasswordHash(password), Name = username };
            var response = await getHttpClient().PostGetFromJSONAsync<RegisterRequest, Session>("/api/session/register", request);
            if (response != null)
            {
                _navManager.NavigateTo("chat", true); // In order for auth state to update, the page needs to reload
                return true;
            }
            this._loginState.State = Services.LoginState.NoInit;
            return false;
        }

        public async Task<bool> RecoverExistingSession()
        {
            this._loginState.State = Services.LoginState.Connecting;
            var response = await getHttpClient().GetFromJSONAsyncNoExcept<Session>("/api/session");
            if (response != null)
            {
                this._session.State = response;
                this._selfUser.State = response.User;
                this._loginState.State = Services.LoginState.Connected;
                Console.WriteLine($"Logged in as \"{response.User?.Name ?? "null"}\" [{response.User?.Id ?? default(ItemId)}]");
                return true;
            }
            this._loginState.State = Services.LoginState.NoInit;
            return false;
        }

        public async Task Logout()
        {
            await getHttpClient().DeleteAsync("/api/session/logout");
            {
                this._loginState.State = Services.LoginState.NoInit;
                this._session.State = null;
                this._selfUser.State = null;
                await _cacheService.ClearCache();
                _navManager.NavigateTo("chat", true); // In order for auth state to update, the page needs to reload
            }
        }

        public async Task<Message?> CreateMessage(ItemId channelId, string body = "", FileAttachment? attachment = null)
        {
            MessageCreateInfo messageCreateInfo = new MessageCreateInfo() { ChannelId = channelId, Body = body, Attachment = attachment };
#if APIDEBUGLOGGING
            Console.WriteLine($"[Post+] api/messages/create << {JsonSerializer.Serialize(messageCreateInfo)}");
#endif
            var message = await getHttpClient().PostGetFromJSONAsync<MessageCreateInfo, Message>("api/messages/create", messageCreateInfo);
            return message;
        }

        public async Task<Message[]> SearchMessages(MessageSearchQuery query)
        {
#if APIDEBUGLOGGING
            Console.WriteLine($"[Post+] api/messages/find << {JsonSerializer.Serialize(query)}");
#endif
            var messages = await getHttpClient().PostGetFromJSONAsync<MessageSearchQuery, Message[]>("api/messages/find", query);
            return messages ?? Array.Empty<Message>();
        }

        public async Task<Message[]> GetMessages(ItemId channelId, DateTimeOffset reference, bool older, int limit)
        {
            MessageGetInfo messageGetInfo = new MessageGetInfo() { ChannelId = channelId, Limit = limit, Older = older, Reference = reference };

#if APIDEBUGLOGGING
            Console.WriteLine($"[Post+] api/messages/get << {JsonSerializer.Serialize(messageGetInfo)}");
#endif
            var messages = await getHttpClient().PostGetFromJSONAsync<MessageGetInfo, Message[]>("api/messages/get", messageGetInfo);

            if (messages != null)
            {
                foreach (var message in messages)
                {
                    await _cacheService.UpdateItem(message);
                }
            }

            return messages ?? Array.Empty<Message>();
        }

        public async Task<FileAttachment?> UploadFile(ItemId channelId, IBrowserFile file)
        {
            string? mimeType = file.ContentType.ToLowerInvariant();
            if (!FileHelper.IsValidMimeType(mimeType) || !FileHelper.IsImageMime(mimeType))
            {
                return null;
            }
            if (file.Size > FileHelper.MAX_FILE_SIZE)
            {
                return null;
            }
            var fileContent = new StreamContent(file.OpenReadStream(FileHelper.MAX_FILE_SIZE));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
#if APIDEBUGLOGGING
            Console.WriteLine($"[Post+] api/storage/{channelId} << Uploadfile {file.Name}");
#endif
            var response = await getHttpClient().PostAsync($"api/storage/{channelId}", fileContent);

            //Console.WriteLine($"Upload file response {response.Content.ToString()}");

            FileAttachment? result = await response.Content.ReadFromJsonAsync<FileAttachment>();


            if (result == null)
            {
                return null;
            }

            return result;
        }

        public async Task<TemporaryURL?> GetTemporaryURL(ItemId channelId, FileAttachment attachment)
        {
#if APIDEBUGLOGGING
            Console.WriteLine($"[Get] {attachment.TemporaryFileRequestURL(channelId)}");
#endif
            return await getHttpClient().GetFromJSONAsyncNoExcept<TemporaryURL>(attachment.TemporaryFileRequestURL(channelId));
        }

        public async Task<Channel?> GetChannel(ItemId id)
        {
#if APIDEBUGLOGGING
            Console.WriteLine($"[Get] api/channels/{id}");
#endif
            var channel = await getHttpClient().GetFromJSONAsyncNoExcept<Channel>($"api/channels/{id}");
            if (channel != null)
            {
                await _cacheService.UpdateItem(channel);
            }
            return channel;
        }

        public async Task<Channel[]> GetChannels()
        {
#if APIDEBUGLOGGING
            Console.WriteLine($"[Get] api/channels");
#endif
            var channels = (await getHttpClient().GetFromJSONAsyncNoExcept<Channel[]>("api/channels")) ?? Array.Empty<Channel>();

            foreach (var channel in channels)
            {
                await _cacheService.UpdateItem(channel);
            }

            return channels;
        }

        public async Task<User?> GetUser(ItemId id)
        {
#if APIDEBUGLOGGING
            Console.WriteLine($"[Get] api/users/{id}");
#endif
            var user = await getHttpClient().GetFromJSONAsyncNoExcept<User>($"api/users/{id}");
            if (user != null)
            {
                await _cacheService.UpdateItem(user.Clone());
                if (user.Id == SelfUser.State?.Id)
                {
                    _selfUser.TriggerChange(user);
                }
            }
            return user;

        }

        public async Task<User[]> GetUsers(IReadOnlyCollection<ItemId> ids)
        {
#if APIDEBUGLOGGING
            Console.WriteLine($"[Post+] api/users/multi << {JsonSerializer.Serialize(ids)}");
#endif
            var users = (await getHttpClient().PostGetFromJSONAsync<IReadOnlyCollection<ItemId>, User[]>("api/users/multi", ids)) ?? Array.Empty<User>();
            foreach (var user in users)
            {
                await _cacheService.UpdateItem(user.Clone());
                if (user.Id == SelfUser.State?.Id)
                {
                    _selfUser.TriggerChange(user);
                }
            }

            return users;
        }

        public async Task<JsonDocument?> GetForm(ItemId formId)
        {
#if APIDEBUGLOGGING
            Console.WriteLine($"[Get] api/forms/{formId}");
#endif
            var response = await getHttpClient().GetFromJSONAsyncNoExcept<JsonDocument>($"api/forms/{formId}");
            return response;
        }

        public async Task<bool> UpdateReadHorizon(ItemId channelId, DateTimeOffset timestamp)
        {
#if APIDEBUGLOGGING
            Console.WriteLine($"[Patch] api/channels/{channelId}/readhorizon/{timestamp.ToUnixTimeMilliseconds()}");
#endif
            var response = await getHttpClient().PatchAsync($"api/channels/{channelId}/readhorizon/{timestamp.ToUnixTimeMilliseconds()}", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<Message?> GetMessage(ItemId channelId, ItemId messageId)
        {
#if APIDEBUGLOGGING
            Console.WriteLine($"[Get] api/messages/{channelId}/{messageId}");
#endif
            var message = await getHttpClient().GetFromJSONAsyncNoExcept<Message>($"api/messages/{channelId}/{messageId}");
            if (message != null)
            {
                await _cacheService.UpdateItem(message);
            }
            return message;
        }

        public async Task<ItemId> InitCall(ItemId calleeId)
        {
#if APIDEBUGLOGGING
            Console.WriteLine($"[Post+] api/calls/init/{calleeId}");
#endif
            return await getHttpClient().PostGetFromJSONAsync<ItemId>($"api/calls/init/{calleeId}");
        }

        public async Task<PendingCall[]> GetCalls()
        {
#if APIDEBUGLOGGING
            Console.WriteLine($"[Get] api/calls/");
#endif
            return await getHttpClient().GetFromJSONAsyncNoExcept<PendingCall[]>($"api/calls") ?? Array.Empty<PendingCall>();
        }

        public async Task<bool> ElevateCall(ItemId callId)
        {
#if APIDEBUGLOGGING
            Console.WriteLine($"[Post] api/calls/{callId}");
#endif
            var response = await getHttpClient().PostAsync($"api/calls/{callId}", null);
            return response != null && response.IsSuccessStatusCode;
        }

        public async Task<bool> TerminateCall(ItemId callId)
        {
#if APIDEBUGLOGGING
            Console.WriteLine($"[Delete] api/calls/{callId}");
#endif
            var response = await getHttpClient().DeleteAsync($"api/calls/{callId}");
            return response != null && response.IsSuccessStatusCode;
        }

        public async Task<bool> UploadAvatar(IBrowserFile file)
        {

            string? mimeType = file.ContentType.ToLowerInvariant();
            if (!FileHelper.IsValidMimeType(mimeType) || !FileHelper.IsImageMime(mimeType))
            {
                return false;
            }
            if (file.Size > FileHelper.MAX_FILE_SIZE)
            {
                return false;
            }
            var fileContent = new StreamContent(file.OpenReadStream(FileHelper.MAX_FILE_SIZE));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
#if APIDEBUGLOGGING
            Console.WriteLine($"[Post+] api/storage/avatar << Uploadfile {file.Name}");
#endif
            var response = await getHttpClient().PostAsync($"api/storage/avatar", fileContent);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UpdateUsername(string username)
        {
#if APIDEBUGLOGGING
            Console.WriteLine($"[Get] api/users/username/{username}");
#endif
            var response = await getHttpClient().PatchAsync($"api/users/username/{Uri.EscapeDataString(username)}", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<FormRequest?> GetFormRequest(ItemId requestId)
        {
#if APIDEBUGLOGGING
            Console.WriteLine($"[Get] api/forms/request/{requestId}");
#endif
            return await getHttpClient().GetFromJSONAsyncNoExcept<FormRequest>($"api/forms/request/{requestId}");
        }

        public async Task<bool> PostFormResponse(ItemId requestId, JsonNode response)
        {
#if APIDEBUGLOGGING
            Console.WriteLine($"[Get] api/forms/response/{requestId}");
#endif
            JsonContent content = JsonContent.Create(response);
            var result = await getHttpClient().PostAsync($" api/forms/response/{requestId}", content);
            return result != null && result.IsSuccessStatusCode;
        }
    }
}
