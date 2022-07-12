using BlazorChat.Shared;
using Microsoft.AspNetCore.Components.Forms;
using System.Text.Json;
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
        #region Login State
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
        public Task<ApiResult> Login(string login, string password, CancellationToken ct = default);
        /// <summary>
        /// Register new user for <paramref name="login"/> with display name <paramref name="username"/>. Causes page reload on success (Auth state cannot update without).
        /// </summary>
        public Task<ApiResult> Register(string login, string password, string username, CancellationToken ct = default);
        /// <summary>
        /// Recovers an existing session
        /// </summary>
        public Task<ApiResult<Session>> RecoverExistingSession(CancellationToken ct = default);
        /// <summary>
        /// Logs out currently signed in user
        /// </summary>
        /// <returns></returns>
        public Task Logout(CancellationToken ct = default);
        #endregion
        /// <summary>
        /// Fetches a single channel from the API
        /// </summary>
        /// <param name="id">Channel Id</param>
        /// <returns>Channel object if is found and no errors occured. Null otherwise</returns>
        public Task<ApiResult<Channel>> GetChannel(ItemId id, CancellationToken ct = default);
        /// <summary>
        /// Fetches all channels available to the logged in user
        /// </summary>
        /// <returns>Array of channels. May return empty array if an error occurs.</returns>
        public Task<ApiResult<Channel[]>> GetChannels(CancellationToken ct = default);
        /// <summary>
        /// Fetches a single user from the API
        /// </summary>
        /// <param name="id">User Id</param>
        /// <returns>User object if is found and no errors occured. Null otherwise</returns>
        public Task<ApiResult<User>> GetUser(ItemId id, CancellationToken ct = default);
        /// <summary>
        /// Fetches multiple users from the API
        /// </summary>
        /// <param name="ids">Collection of user Ids to fetch</param>
        /// <returns>Array of user objects. May return empty array if an error occurs.</returns>
        public Task<ApiResult<User[]>> GetUsers(IReadOnlyCollection<ItemId> ids, CancellationToken ct = default);
        /// <summary>
        /// Creates a message
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="body"></param>
        /// <param name="attachment"></param>
        Task<ApiResult<Message>> CreateMessage(ItemId channelId, string body = "", FileAttachment? attachment = null, CancellationToken ct = default);
        /// <summary>
        /// Uploades a new file to channel <paramref name="channelId"/> with file body <paramref name="file"/>
        /// </summary>
        /// <returns>The chatmessage if successful</returns>
        public Task<ApiResult<FileAttachment>> UploadFile(ItemId channelId, IBrowserFile file, CancellationToken ct = default);
        /// <summary>
        /// Upload a new avatar
        /// </summary>
        public Task<ApiResult> UploadAvatar(IBrowserFile? file, CancellationToken ct = default);
        /// <summary>
        /// Update own username
        /// </summary>
        public Task<ApiResult> UpdateUsername(string username, CancellationToken ct = default);
        /// <summary>
        /// Update own password
        /// </summary>
        public Task<ApiResult> UpdatePassword(string oldpassword, string newpassword, CancellationToken ct = default);
        /// <summary>
        /// Get temporary url for file access
        /// </summary>
        public Task<ApiResult<TemporaryURL>> GetTemporaryURL(ItemId channelId, FileAttachment attachment, CancellationToken ct = default);
        /// <summary>
        /// Get a list of chatmessages
        /// </summary>
        /// <param name="channelId">Channel to pull from</param>
        /// <param name="reference">Which inclusive timestamp to post the query relative to</param>
        /// <param name="older">if true, older messages than <paramref name="reference"/> are pulled, newer otherwise</param>
        /// <param name="limit">How many messages to return at most</param>
        /// <returns>Result of query</returns>
        public Task<ApiResult<Message[]>> GetMessages(ItemId channelId, DateTimeOffset reference, bool older, int limit, CancellationToken ct = default);
        /// <summary>
        /// Conducts a message search according to <paramref name="query"/>s parameters
        /// </summary>
        /// <returns>An array of matching messages (may be empty)</returns>
        public Task<ApiResult<Message[]>> SearchMessages(MessageSearchQuery query, CancellationToken ct = default);
        /// <summary>
        /// Sends a read horizon update to the server
        /// </summary>
        /// <param name="channelId">Channel Id to update read horizon in</param>
        /// <param name="timestamp">Timestamp to advance read horizon to</param>
        /// <returns>True if successful</returns>
        public Task<ApiResult> UpdateReadHorizon(ItemId channelId, DateTimeOffset timestamp, CancellationToken ct = default);
        /// <summary>
        /// Retrieves a single message from the server
        /// </summary>
        /// <returns>Hit or null on failure</returns>
        public Task<ApiResult<Message>> GetMessage(ItemId channelId, ItemId messageId, CancellationToken ct = default);
        /// <summary>
        /// Inits a new call
        /// </summary>
        /// <param name="calleeId">User to be called</param>
        /// <returns>Call Id. Check ItemId.IsZero to determine operation success!</returns>
        public Task<ApiResult<ItemId>> InitCall(ItemId calleeId, CancellationToken ct = default);
        /// <summary>
        /// Get a list of pending calls 
        /// </summary>
        public Task<ApiResult<PendingCall[]>> GetCalls(CancellationToken ct = default);
        /// <summary>
        /// Elevates a call to active status
        /// </summary>
        /// <param name="callId"></param>
        /// <returns></returns>
        public Task<ApiResult> ElevateCall(ItemId callId, CancellationToken ct = default);
        /// <summary>
        /// Terminates an active or pending call
        /// </summary>
        /// <param name="callID"></param>
        /// <returns></returns>
        public Task<ApiResult> TerminateCall(ItemId callId, CancellationToken ct = default);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="callId"></param>
        /// <returns></returns>
        public Task<ApiResult<IceConfiguration[]>> GetIceConfigurations(ItemId callId, CancellationToken ct = default);
        /// <summary>
        /// Get a form
        /// </summary>
        /// <param name="formId"></param>
        /// <returns></returns>
        public Task<ApiResult<JsonDocument>> GetForm(ItemId formId, CancellationToken ct = default);
        /// <summary>
        /// Get a form request
        /// </summary>
        /// <param name="requestId"></param>
        /// <returns></returns>
        public Task<ApiResult<FormRequest>> GetFormRequest(ItemId requestId, CancellationToken ct = default);
        /// <summary>
        /// Post a form response to the server
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        public Task<ApiResult> PostFormResponse(ItemId requestId, JsonNode response, CancellationToken ct = default);
        /// <summary>
        /// Gets a message copy from the server with the body replaced by a translation
        /// </summary>
        /// <param name="channelId">Messages channel</param>
        /// <param name="messageId">Message Id</param>
        /// <param name="languageCode">Language to translate to. Reverts to current culture if amended</param>
        public Task<ApiResult<Message>> GetMessageTranslated(ItemId channelId, ItemId messageId, string? languageCode = null, CancellationToken ct = default);
        /// <summary>
        /// Gets a temporary url for a user avatar
        /// </summary>
        Task<ApiResult<TemporaryURL>> GetTemporaryAvatarURL(ItemId userId, FileAttachment attachment, CancellationToken ct = default);
        /// <summary>
        /// Start a session by consuming a JWT in base64 string format
        /// </summary>
        /// <param name="tokenbase64"></param>
        Task<ApiResult> ClaimTokenSession(string tokenbase64, CancellationToken ct = default);
    }
}
