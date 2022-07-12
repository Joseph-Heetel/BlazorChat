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
using System.Web;

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
            _navManager.LocationChanged += _navManager_LocationChanged;
            onLocationChanged(_navManager.Uri);
        }

        private void _navManager_LocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
        {
            onLocationChanged(e.Location);
        }

        private async void onLocationChanged(string location)
        {
            int indexOfQuery = location.IndexOf('?');
            if (indexOfQuery >= 0)
            {
                var query = HttpUtility.ParseQueryString(location.Substring(indexOfQuery));
                string? tokenParam = query.Get("token");
                if (tokenParam != null)
                {
                    await ClaimTokenSession(tokenParam);
                }
            }
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
        private async Task<ApiResult> apiPost<TPayload>(string path, TPayload payload, CancellationToken ct = default)
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
            return await apiHttpSend(message, method, ct);
        }

        /// <summary>
        /// Sends Http Request
        /// </summary>
        private Task<ApiResult> apiHttpSend(HttpMethod method, string path, CancellationToken ct = default)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, path);
            return apiHttpSend(request, null, ct);
        }
        /// <summary>
        /// Sends Http Request
        /// </summary>
        private async Task<ApiResult> apiHttpSend(HttpRequestMessage message, string? customMethodInfo = null, CancellationToken ct = default)
        {
            HttpClient client = getHttpClient();
            string path = message.RequestUri?.OriginalString ?? "";
            string method = customMethodInfo ?? message.Method.Method;
            try
            {
                HttpResponseMessage response = await client.SendAsync(message, ct);
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
        private Task<ApiResult<T>> apiGet<T>(string path, CancellationToken ct = default)
        {
            return apiHttpSend<T>(HttpMethod.Get, path, ct);
        }
        /// <summary>
        /// Sends Http post request, json-serializes <paramref name="payload"/> as request content, json-deserializes response content as <typeparamref name="T"/>
        /// </summary>
        private async Task<ApiResult<T>> apiPost<T, TPayload>(string path, TPayload payload, CancellationToken ct = default)
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
            return await apiHttpSend<T>(message, method, ct);
        }
        /// <summary>
        /// Send Http request, json-deserializes response content as <typeparamref name="T"/>
        /// </summary>
        public Task<ApiResult<T>> apiHttpSend<T>(HttpMethod method, string path, CancellationToken ct = default)
        {
            HttpRequestMessage message = new HttpRequestMessage(method, path);
            return apiHttpSend<T>(message, null, ct);
        }
        /// <summary>
        /// Send Http request, json-deserializes response content as <typeparamref name="T"/>
        /// </summary>
        public async Task<ApiResult<T>> apiHttpSend<T>(HttpRequestMessage message, string? customMethodInfo = null, CancellationToken ct = default)
        {
            HttpClient client = getHttpClient();
            string path = message.RequestUri?.OriginalString ?? "";
            string method = customMethodInfo ?? message.Method.Method;
            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(message, ct);
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
                    var result = new ApiResult<T>(EApiStatusCode.Success, response.StatusCode, await response.Content.ReadFromJsonAsync<T>((JsonSerializerOptions?)null, ct));
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

        public async Task<ApiResult> Login(string login, string password, CancellationToken ct = default)
        {
            this._loginState.State = Services.LoginState.Connecting;
            LoginRequest request = new LoginRequest() { Login = login, PasswordHash = GeneratePasswordHash(password) };
            ApiResult<Session> response = await apiPost<Session, LoginRequest>("/api/session/login", request, ct);
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

        public async Task<ApiResult> Register(string login, string password, string username, CancellationToken ct = default)
        {
            this._loginState.State = Services.LoginState.Connecting;
            var request = new RegisterRequest() { Login = login, PasswordHash = GeneratePasswordHash(password), Name = username };
            ApiResult<Session> response = await apiPost<Session, RegisterRequest>("/api/session/register", request, ct);
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

        public async Task<ApiResult<Session>> RecoverExistingSession(CancellationToken ct = default)
        {
            this._loginState.State = Services.LoginState.Connecting;
            ApiResult<Session> response = await apiGet<Session>("/api/session", ct);
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

        public async Task<ApiResult> ClaimTokenSession(string tokenbase64, CancellationToken ct = default)
        {
            if (_loginState.State == Services.LoginState.Connected)
            {
                await Logout();
            }
            this._loginState.State = Services.LoginState.Connecting;
            ApiResult<Session> response = await apiGet<Session>($"/api/session/token/{Uri.EscapeDataString(tokenbase64)}", ct);
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

        public async Task Logout(CancellationToken ct = default)
        {
            await apiHttpSend(HttpMethod.Delete, "/api/session/logout", ct);
            {
                this._loginState.State = Services.LoginState.NoInit;
                this._session.State = null;
                this._selfUser.State = null;
                await _cacheService.ClearCache();
                _navManager.NavigateTo("chat", true); // In order for auth state to update, the page needs to reload
            }
        }

        public Task<ApiResult<Message>> CreateMessage(ItemId channelId, string body = "", FileAttachment? attachment = null, CancellationToken ct = default)
        {
            MessageCreateInfo messageCreateInfo = new MessageCreateInfo() { ChannelId = channelId, Body = body, Attachment = attachment };
            return apiPost<Message, MessageCreateInfo>("api/messages/create", messageCreateInfo, ct);
        }

        public Task<ApiResult<Message[]>> SearchMessages(MessageSearchQuery query, CancellationToken ct = default)
        {
            return apiPost<Message[], MessageSearchQuery>("api/messages/find", query, ct);
        }

        public async Task<ApiResult<Message[]>> GetMessages(ItemId channelId, DateTimeOffset reference, bool older, int limit, CancellationToken ct = default)
        {
            MessageGetInfo messageGetInfo = new MessageGetInfo() { ChannelId = channelId, Limit = limit, Older = older, Reference = reference };

            var messages = await apiPost<Message[], MessageGetInfo>("api/messages/get", messageGetInfo, ct);

            if (messages.IsSuccess)
            {
                foreach (var message in messages.ResultAsserted)
                {
                    await _cacheService.UpdateItem(message);
                }
            }

            return messages;
        }

        public async Task<ApiResult<FileAttachment>> UploadFile(ItemId channelId, IBrowserFile file, CancellationToken ct = default)
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
            return await apiHttpSend<FileAttachment>(message, null, ct);
        }

        public Task<ApiResult<TemporaryURL>> GetTemporaryURL(ItemId channelId, FileAttachment attachment, CancellationToken ct = default)
        {
            return apiGet<TemporaryURL>(attachment.TemporaryFileRequestURL(channelId), ct);
        }

        public Task<ApiResult<TemporaryURL>> GetTemporaryAvatarURL(ItemId userId, FileAttachment attachment, CancellationToken ct = default)
        {
            return apiGet<TemporaryURL>(attachment.TemporaryAvatarRequestURL(userId), ct);
        }

        public async Task<ApiResult<Channel>> GetChannel(ItemId id, CancellationToken ct = default)
        {
            var response = await apiGet<Channel>($"api/channels/{id}", ct);
            if (response.TryGet(out Channel channel))
            {
                await _cacheService.UpdateItem(channel);
            }
            return response;
        }

        public async Task<ApiResult<Channel[]>> GetChannels(CancellationToken ct = default)
        {
            var response = await apiGet<Channel[]>("api/channels", ct);

            if (response.TryGet(out Channel[] channels))
            {
                foreach (var channel in channels)
                {
                    await _cacheService.UpdateItem(channel);
                }
            }

            return response;
        }

        public async Task<ApiResult<User>> GetUser(ItemId id, CancellationToken ct = default)
        {
            var response = await apiGet<User>($"api/users/{id}", ct);
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

        public async Task<ApiResult<User[]>> GetUsers(IReadOnlyCollection<ItemId> ids, CancellationToken ct = default)
        {
            var response = await apiPost<User[], IReadOnlyCollection<ItemId>>("api/users/multi", ids, ct);
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

        public Task<ApiResult<JsonDocument>> GetForm(ItemId formId, CancellationToken ct = default)
        {
            return apiGet<JsonDocument>($"api/forms/{formId}", ct);
        }

        public Task<ApiResult> UpdateReadHorizon(ItemId channelId, DateTimeOffset timestamp, CancellationToken ct = default)
        {
            return apiHttpSend(HttpMethod.Patch, $"api/channels/{channelId}/readhorizon/{timestamp.ToUnixTimeMilliseconds()}", ct);
        }

        public async Task<ApiResult<Message>> GetMessage(ItemId channelId, ItemId messageId, CancellationToken ct = default)
        {
            var response = await apiGet<Message>($"api/messages/{channelId}/{messageId}", ct);
            if (response.TryGet(out Message message))
            {
                await _cacheService.UpdateItem(message);
            }
            return response;
        }

        public Task<ApiResult<ItemId>> InitCall(ItemId calleeId, CancellationToken ct = default)
        {
            return apiHttpSend<ItemId>(HttpMethod.Post, $"api/calls/init/{calleeId}", ct);
        }

        public Task<ApiResult<PendingCall[]>> GetCalls(CancellationToken ct = default)
        {
            return apiGet<PendingCall[]>($"api/calls", ct);
        }

        public Task<ApiResult> ElevateCall(ItemId callId, CancellationToken ct = default)
        {
            return apiHttpSend(HttpMethod.Post, $"api/calls/{callId}", ct);
        }

        public Task<ApiResult> TerminateCall(ItemId callId, CancellationToken ct = default)
        {
            return apiHttpSend(HttpMethod.Delete, $"api/calls/{callId}", ct);
        }

        public async Task<ApiResult> UploadAvatar(IBrowserFile? file, CancellationToken ct = default)
        {
            if (file != null)
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
                return await apiHttpSend(message, null, ct);
            }
            else
            {
                return await apiHttpSend(HttpMethod.Delete, $"api/storage/avatar", ct);
            }
        }

        public Task<ApiResult> UpdateUsername(string username, CancellationToken ct = default)
        {
            return apiHttpSend(HttpMethod.Patch, $"api/users/username/{Uri.EscapeDataString(username)}", ct);
        }

        public Task<ApiResult<FormRequest>> GetFormRequest(ItemId requestId, CancellationToken ct = default)
        {
            return apiGet<FormRequest>($"api/forms/request/{requestId}", ct);
        }

        public Task<ApiResult> PostFormResponse(ItemId requestId, JsonNode response, CancellationToken ct = default)
        {
            JsonContent content = JsonContent.Create(response);
            return apiPost($" api/forms/response/{requestId}", content, ct);
        }

        public Task<ApiResult<IceConfiguration[]>> GetIceConfigurations(ItemId callId, CancellationToken ct = default)
        {
            return apiGet<IceConfiguration[]>($"api/calls/ice/{callId}", ct);
        }

        public async Task<ApiResult<Message>> GetMessageTranslated(ItemId channelId, ItemId messageId, string? languageCode = null, CancellationToken ct = default)
        {
            languageCode ??= CultureInfo.CurrentCulture.Name;
            var response = await apiGet<Message>($"api/messages/translate/{channelId}/{messageId}/{languageCode}", ct);
            if (response.TryGet(out Message message))
            {
                await _cacheService.UpdateItem(message);
            }
            return response;
        }

        public async Task<ApiResult> UpdatePassword(string oldpassword, string newpassword, CancellationToken ct = default)
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
            return await apiPost("api/session/changepassword", changeRequest, ct);
        }
    }
}
