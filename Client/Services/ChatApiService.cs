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
using System.Globalization;
using Blazored.LocalStorage;

namespace BlazorChat.Client.Services
{

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
        private readonly ILocalCacheService _cacheService;
        private readonly ILogger _logger;

        private Observable<LoginState> _loginState { get; } = new Observable<LoginState>(Services.LoginState.NoInit);
        private Observable<Session?> _session { get; } = new Observable<Session?>(null);
        private Observable<User?> _selfUser { get; } = new Observable<User?>(null);
        public IReadOnlyObservable<LoginState> LoginState => _loginState;
        public IReadOnlyObservable<Session?> Session => _session;
        public IReadOnlyObservable<User?> SelfUser => _selfUser;

        public ChatApiService(IHttpClientFactory httpClientFactory, NavigationManager navManager, ILocalCacheService cache, ILoggerFactory loggerFactory)
        {
            this._httpClientFactory = httpClientFactory;
            HashAlgorithm? hash = HashAlgorithm.Create(HashAlgorithmName.SHA256.Name!);
            Trace.Assert(hash != null);
            this._hashAlgorithm = hash!;
            this._navManager = navManager;
            this._cacheService = cache;
            _logger = loggerFactory.CreateLogger<ChatApiService>();
        }

        private ByteArray GeneratePasswordHash(string password)
        {
            // This is bad practice, as a hash like this could easily be cross referenced
            // with other hashes of passwords the user used in the past, recovered from previous data leaks.
            // A cryptographic hash function with a unique init vector should be used instead. Doing so on the server
            // side is probably sufficiently secure (This still leaves client <-> server communication as a potential weakness).
            return new ByteArray(_hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(password)));
        }

        #region Helper Methods
        /// <summary>
        /// Http Post request with <paramref name="payload"/> json content
        /// </summary>
        /// <typeparam name="TPayload"></typeparam>
        /// <param name="path"></param>
        /// <param name="payload"></param>
        /// <returns></returns>
        private async Task<ApiResult> apiPost<TPayload>(string path, TPayload payload)
        {
            string method = $"POST<{typeof(TPayload)}>";
            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, path);
            try
            {
                message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            }
            catch (Exception e)
            {
                _logger.LogCritical($"Http {method} \"{path}\" request json serialization failed: {e.Message}");
                return ApiResult.JsonException;
            }
            return await ApiHttpSend(message, method);
        }

        /// <summary>
        /// Sends Http Request
        /// </summary>
        private Task<ApiResult> apiHttpSend(HttpMethod method, string path)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, path);
            return ApiHttpSend(request);
        }
        /// <summary>
        /// Sends Http Request
        /// </summary>
        public async Task<ApiResult> ApiHttpSend(HttpRequestMessage message, string? customMethodInfo = null)
        {
            HttpClient client = getHttpClient();
            string path = message.RequestUri?.OriginalString ?? "";
            string method = customMethodInfo ?? message.Method.Method;
            try
            {
                HttpResponseMessage response = await client.SendAsync(message);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Http {method} \"{path}\" non-success status: {(int)response.StatusCode} {response.StatusCode}");
                    return new ApiResult(EApiStatusCode.NetErrorCode, response.StatusCode);
                }
                else
                {
                    _logger.LogDebug($"Http {method} \"{path}\" good: {(int)response.StatusCode} {response.StatusCode}");
                    return new ApiResult(EApiStatusCode.Success, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"HTTP {method} \"{path}\" send failed: \"{ex.Message}\"");
                return ApiResult.NetException;
            }
        }
        /// <summary>
        /// Send Http Get request, json-deserializes response content as <typeparamref name="T"/>
        /// </summary>
        public Task<ApiResult<T>> ApiGet<T>(string path)
        {
            return ApiHttpSend<T>(HttpMethod.Get, path);
        }
        /// <summary>
        /// Sends Http post request, json-serializes <paramref name="payload"/> as request content, json-deserializes response content as <typeparamref name="T"/>
        /// </summary>
        public async Task<ApiResult<T>> ApiPost<T, TPayload>(string path, TPayload payload)
        {
            string method = $"POST<{typeof(TPayload)}>";
            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, path);
            try
            {
                message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            }
            catch (Exception e)
            {
                _logger.LogCritical($"Http {method} \"{path}\" request json serialization failed: {e.Message}");
                return ApiResult<T>.JsonException;
            }
            return await ApiHttpSend<T>(message, method);
        }
        /// <summary>
        /// Send Http request, json-deserializes response content as <typeparamref name="T"/>
        /// </summary>
        public Task<ApiResult<T>> ApiHttpSend<T>(HttpMethod method, string path)
        {
            HttpRequestMessage message = new HttpRequestMessage(method, path);
            return ApiHttpSend<T>(message);
        }
        /// <summary>
        /// Send Http request, json-deserializes response content as <typeparamref name="T"/>
        /// </summary>
        public async Task<ApiResult<T>> ApiHttpSend<T>(HttpRequestMessage message, string? customMethodInfo = null)
        {
            HttpClient client = getHttpClient();
            string path = message.RequestUri?.OriginalString ?? "";
            string method = customMethodInfo ?? message.Method.Method;
            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"HTTP {method} \"{path}\" send failed: \"{ex.Message}\"");
                return ApiResult<T>.NetException;
            }
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Http {method} \"{path}\" non-success status: {(int)response.StatusCode} {response.StatusCode}");
                return new ApiResult<T>(EApiStatusCode.NetErrorCode, response.StatusCode, default);
            }
            else
            {
                try
                {
                    var result = new ApiResult<T>(EApiStatusCode.Success, response.StatusCode, await response.Content.ReadFromJsonAsync<T>());
                    _logger.LogDebug($"Http {method} \"{path}\" good: {(int)response.StatusCode} {response.StatusCode}");
                    return result;
                }
                catch (Exception e)
                {
                    _logger.LogCritical($"Http {method} \"{path}\" response json deserialization failed: {e.Message}");
                    return ApiResult<T>.JsonException;
                }
            }

        }

        #endregion

        public async Task<ApiResult> Login(string login, string password)
        {
            this._loginState.State = Services.LoginState.Connecting;
            LoginRequest request = new LoginRequest() { Login = login, PasswordHash = GeneratePasswordHash(password) };
            ApiResult<Session> response = await ApiPost<Session, LoginRequest>("/api/session/login", request);
            if (response.IsSuccess)
            {
                _navManager.NavigateTo("chat", true); // In order for auth state to update, the page needs to reload
            }
            else
            {
                this._loginState.State = Services.LoginState.NoInit;
            }
            return response;
        }

        public async Task<ApiResult> Register(string login, string password, string username)
        {
            this._loginState.State = Services.LoginState.Connecting;
            var request = new RegisterRequest() { Login = login, PasswordHash = GeneratePasswordHash(password), Name = username };
            ApiResult<Session> response = await ApiPost<Session, RegisterRequest>("/api/session/register", request);
            if (response.IsSuccess)
            {
                _navManager.NavigateTo("chat", true); // In order for auth state to update, the page needs to reload
            }
            else
            {
                this._loginState.State = Services.LoginState.NoInit;
            }
            return response;
        }

        public async Task<ApiResult<Session>> RecoverExistingSession()
        {
            this._loginState.State = Services.LoginState.Connecting;
            ApiResult<Session> response = await ApiGet<Session>("/api/session");
            Session? session = response.Result;
            if (response.StatusCode == EApiStatusCode.NetException)
            {
                // The session fetch failed due to network failure, fallback to offline mode
                session = await _cacheService.GetOfflineSession();
                response.Result = session;
            }
            if (session != null)
            {
                _ = Task.Run(() => _cacheService.SetOfflineSession(session));
                this._session.State = session;
                this._selfUser.State = session.User;
                this._loginState.State = Services.LoginState.Connected;
                _logger.LogInformation($"Logged in as \"{session.User?.Name ?? "null"}\" [{session.User?.Id ?? default(ItemId)}]");
                return response;
            }
            else
            {
                this._loginState.State = Services.LoginState.NoInit;
            }
            return response;
        }

        public async Task Logout()
        {
            await apiHttpSend(HttpMethod.Delete, "/api/session/logout");
            {
                this._loginState.State = Services.LoginState.NoInit;
                this._session.State = null;
                this._selfUser.State = null;
                await _cacheService.ClearCache();
                _navManager.NavigateTo("chat", true); // In order for auth state to update, the page needs to reload
            }
        }

        public Task<ApiResult<Message>> CreateMessage(ItemId channelId, string body = "", FileAttachment? attachment = null)
        {
            MessageCreateInfo messageCreateInfo = new MessageCreateInfo() { ChannelId = channelId, Body = body, Attachment = attachment };
            return ApiPost<Message, MessageCreateInfo>("api/messages/create", messageCreateInfo);
        }

        public Task<ApiResult<Message[]>> SearchMessages(MessageSearchQuery query)
        {
            return ApiPost<Message[], MessageSearchQuery>("api/messages/find", query);
        }

        public async Task<ApiResult<Message[]>> GetMessages(ItemId channelId, DateTimeOffset reference, bool older, int limit)
        {
            MessageGetInfo messageGetInfo = new MessageGetInfo() { ChannelId = channelId, Limit = limit, Older = older, Reference = reference };

            var messages = await ApiPost<Message[], MessageGetInfo>("api/messages/get", messageGetInfo);

            if (messages.IsSuccess)
            {
                foreach (var message in messages.ResultAsserted)
                {
                    await _cacheService.UpdateItem(message);
                }
            }

            return messages;
        }

        public async Task<ApiResult<FileAttachment>> UploadFile(ItemId channelId, IBrowserFile file)
        {
            string? mimeType = file.ContentType.ToLowerInvariant();
            if (!FileHelper.IsValidMimeType(mimeType) || !FileHelper.IsImageMime(mimeType))
            {
                return ApiResult<FileAttachment>.PreconditionFail;
            }
            if (file.Size > ChatConstants.MAX_FILE_SIZE)
            {
                return ApiResult<FileAttachment>.PreconditionFail;
            }
            using var fileContent = new StreamContent(file.OpenReadStream(ChatConstants.MAX_FILE_SIZE));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, $"api/storage/{channelId}");
            message.Content = fileContent;
            return await ApiHttpSend<FileAttachment>(message);
        }

        public Task<ApiResult<TemporaryURL>> GetTemporaryURL(ItemId channelId, FileAttachment attachment)
        {
            return ApiGet<TemporaryURL>(attachment.TemporaryFileRequestURL(channelId));
        }

        public Task<ApiResult<TemporaryURL>> GetTemporaryAvatarURL(ItemId userId, FileAttachment attachment)
        {
            return ApiGet<TemporaryURL>(attachment.TemporaryAvatarRequestURL(userId));
        }

        public async Task<ApiResult<Channel>> GetChannel(ItemId id)
        {
            var response = await ApiGet<Channel>($"api/channels/{id}");
            if (response.TryGet(out Channel channel))
            {
                await _cacheService.UpdateItem(channel);
            }
            return response;
        }

        public async Task<ApiResult<Channel[]>> GetChannels()
        {
            var response = await ApiGet<Channel[]>("api/channels");

            if (response.TryGet(out Channel[] channels))
            {
                foreach (var channel in channels)
                {
                    await _cacheService.UpdateItem(channel);
                }
            }

            return response;
        }

        public async Task<ApiResult<User>> GetUser(ItemId id)
        {
            var response = await ApiGet<User>($"api/users/{id}");
            if (response.TryGet(out User user))
            {
                await _cacheService.UpdateItem(user.Clone());
                if (user.Id == SelfUser.State?.Id)
                {
                    _selfUser.TriggerChange(user);
                }
            }
            return response;

        }

        public async Task<ApiResult<User[]>> GetUsers(IReadOnlyCollection<ItemId> ids)
        {
            var response = await ApiPost<User[], IReadOnlyCollection<ItemId>>("api/users/multi", ids);
            if (response.TryGet(out User[] users))
                foreach (var user in users)
                {
                    await _cacheService.UpdateItem(user.Clone());
                    if (user.Id == SelfUser.State?.Id)
                    {
                        _selfUser.TriggerChange(user);
                    }
                }

            return response;
        }

        public Task<ApiResult<JsonDocument>> GetForm(ItemId formId)
        {
            return ApiGet<JsonDocument>($"api/forms/{formId}");
        }

        public Task<ApiResult> UpdateReadHorizon(ItemId channelId, DateTimeOffset timestamp)
        {
            return apiHttpSend(HttpMethod.Patch, $"api/channels/{channelId}/readhorizon/{timestamp.ToUnixTimeMilliseconds()}");
        }

        public async Task<ApiResult<Message>> GetMessage(ItemId channelId, ItemId messageId)
        {
            var response = await ApiGet<Message>($"api/messages/{channelId}/{messageId}");
            if (response.TryGet(out Message message))
            {
                await _cacheService.UpdateItem(message);
            }
            return response;
        }

        public Task<ApiResult<ItemId>> InitCall(ItemId calleeId)
        {
            return ApiHttpSend<ItemId>(HttpMethod.Post, $"api/calls/init/{calleeId}");
        }

        public Task<ApiResult<PendingCall[]>> GetCalls()
        {
            return ApiGet<PendingCall[]>($"api/calls");
        }

        public Task<ApiResult> ElevateCall(ItemId callId)
        {
            return apiHttpSend(HttpMethod.Post, $"api/calls/{callId}");
        }

        public Task<ApiResult> TerminateCall(ItemId callId)
        {
            return apiHttpSend(HttpMethod.Delete, $"api/calls/{callId}");
        }

        public async Task<ApiResult> UploadAvatar(IBrowserFile file)
        {

            string? mimeType = file.ContentType.ToLowerInvariant();
            if (!FileHelper.IsValidMimeType(mimeType) || !FileHelper.IsImageMime(mimeType))
            {
                return ApiResult.PreconditionFail;
            }
            if (file.Size > ChatConstants.MAX_FILE_SIZE)
            {
                return ApiResult.PreconditionFail;
            }
            var fileContent = new StreamContent(file.OpenReadStream(ChatConstants.MAX_FILE_SIZE));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, $"api/storage/avatar");
            message.Content = fileContent;
            return await ApiHttpSend(message);
        }

        public Task<ApiResult> UpdateUsername(string username)
        {
            return apiHttpSend(HttpMethod.Patch, $"api/users/username/{Uri.EscapeDataString(username)}");
        }

        public Task<ApiResult<FormRequest>> GetFormRequest(ItemId requestId)
        {
            return ApiGet<FormRequest>($"api/forms/request/{requestId}");
        }

        public Task<ApiResult> PostFormResponse(ItemId requestId, JsonNode response)
        {
            JsonContent content = JsonContent.Create(response);
            return apiPost($" api/forms/response/{requestId}", content);
        }

        public Task<ApiResult<IceConfiguration[]>> GetIceConfigurations(ItemId callId)
        {
            return ApiGet<IceConfiguration[]>($"api/calls/ice/{callId}");
        }

        public async Task<ApiResult<Message>> GetMessageTranslated(ItemId channelId, ItemId messageId, string? languageCode = null)
        {
            languageCode ??= CultureInfo.CurrentCulture.Name;
            var response = await ApiGet<Message>($"api/messages/translate/{channelId}/{messageId}/{languageCode}");
            if (response.TryGet(out Message message))
            {
                await _cacheService.UpdateItem(message);
            }
            return response;
        }

        public async Task<ApiResult> UpdatePassword(string oldpassword, string newpassword)
        {
            if (_session.State == null)
            {
                return ApiResult.PreconditionFail;
            }
            PasswordChangeRequest changeRequest = new PasswordChangeRequest()
            {
                Login = _session.State.Login,
                PasswordHash = new ByteArray(_hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(oldpassword))),
                NewPasswordHash = new ByteArray(_hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(newpassword))),
            };
            return await apiPost("api/session/changepassword", changeRequest);
        }
    }
}
