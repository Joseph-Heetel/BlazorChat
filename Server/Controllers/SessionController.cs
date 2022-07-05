using BlazorChat.Server.Services;
using BlazorChat.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;

namespace BlazorChat.Server.Controllers
{
    [Route("api/session")]
    [ApiController]
    public class SessionController : ControllerBase
    {
        private readonly ILoginDataService _loginService;
        private readonly IUserDataService _userService;
        private readonly ITokenService _tokenService;

        public SessionController(ILoginDataService loginData, IUserDataService userData, ITokenService tokenService)
        {
            _loginService = loginData;
            _userService = userData;
            _tokenService = tokenService;
        }

        /// <summary>
        /// Endpoint which returns session currently active session, if user is authenticated
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<Session>> GetSession()
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId, out string login))
            {
                return Unauthorized();
            }

            // check sure a user matching exists (part of Session return)
            User? user = await _userService.GetUser(userId);
            if (user == null)
            {
                return Unauthorized();
            }

            // Get state to find expiring timestamp
            AuthenticateResult state = await HttpContext.AuthenticateAsync();
            DateTimeOffset expires = DateTimeOffset.MinValue;
            if (state.Properties?.ExpiresUtc.HasValue ?? false)
            {
                expires = state.Properties.ExpiresUtc.Value;
            }

            return new Session(expires, user, login);
        }

        /// <summary>
        /// Endpoint for refreshing expire time of an existing session
        /// </summary>
        /// <returns>A new session</returns>
        [Route("refresh")]
        [HttpGet]
        public async Task<ActionResult<Session>> RefreshSession()
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId, out string login))
            {
                return Unauthorized();
            }

            // check sure a user matching exists (part of Session return)
            User? user = await _userService.GetUser(userId);
            if (user == null)
            {
                return base.StatusCode(StatusCodes.Status500InternalServerError, "Failed to get user information.");
            }

            // Renew session
            return await HttpContext.SigninSession(null, user, login);
        }

        /// <summary>
        /// Endpoint for logging with login string and password hash
        /// </summary>
        /// <returns>Session information if login successful</returns>
        [Route("login")]
        [HttpPost]
        public async Task<ActionResult<Session>> Login(LoginRequest request)
        {
            // Check login information
            ItemId? userId = await _loginService.TestLoginPassword(request.Login, request.PasswordHash);
            if (userId == null)
            {
                return base.Unauthorized("Login or Password are incorrect.");
            }

            // Make sure user exists
            User? user = await _userService.GetUser(userId.Value);
            if (user == null)
            {
                return base.StatusCode(StatusCodes.Status500InternalServerError, "Failed to get user information.");
            }

            // Generate session
            return await HttpContext.SigninSession(request.ExpireDelay, user, request.Login);
        }

        [Route("token/{tokenbase64}")]
        [HttpGet]
        public async Task<ActionResult<Session>> ClaimTokenSession(string tokenbase64)
        {
            if (!_tokenService.ValidateToken(tokenbase64, out string login, out DateTimeOffset expires))
            {
                return Unauthorized();
            }
            
            var userId = await _loginService.GetUserForLogin(login);
            if (userId == null)
            {
                return Unauthorized();
            }

            var user = await _userService.GetUser(userId.Value);
            if (user == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return await HttpContext.SigninSession(default, user, login);
        }

        [Route("changepassword")]
        [HttpPost]
        public async Task<ActionResult> ChangePassword(PasswordChangeRequest request)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            var loginTest = await _loginService.TestLoginPassword(request.Login, request.PasswordHash);
            if (loginTest != userId)
            {
                return Unauthorized("Login or Password are incorrect.");
            }

            if (!await _loginService.UpdatePassword(request.Login, request.NewPasswordHash))
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Ok();
        }

        /// <summary>
        /// Endpoint for terminating a session
        /// </summary>
        /// <returns></returns>
        [Route("logout")]
        [HttpDelete]
        public async Task<ActionResult> Logout()
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            // Kill session
            await HttpContext.SignOutAsync();
            return Ok();
        }

#if ENABLE_ENDUSER_SELFREGISTER
        /// <summary>
        /// Endpoint for registering new users
        /// </summary>
        [Route("register")]
        [HttpPost]
        public async Task<ActionResult<Session>> Register(RegisterRequest request)
        {
            // Validate request
            if (request == null)
            {
                return base.BadRequest("Missing data.");
            }
            if (string.IsNullOrWhiteSpace(request.Login) || request.Login.Length < ChatConstants.LOGIN_MIN || request.Login.Length > ChatConstants.LOGIN_MAX)
            {
                return BadRequest($"Login must be a string of [{ChatConstants.LOGIN_MIN},{ChatConstants.LOGIN_MAX}] size!");
            }
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length < ChatConstants.USERNAME_MIN || request.Name.Length > ChatConstants.USERNAME_MAX)
            {
                return BadRequest($"Name must be a string of [{ChatConstants.USERNAME_MIN},{ChatConstants.USERNAME_MAX}] size!");
            }
            if (request.PasswordHash.Data.Length != ChatConstants.PASSWORD_HASH_SIZE)
            {
                return BadRequest($"Password hash must be passed as hex encoded string of 32 bytes decoded length!");
            }

            // Make sure login doesn't exist yet
            bool existing = await _loginService.TestLogin(request.Login);
            if (existing)
            {
                return base.BadRequest("Login already exists.");
            }

            // Create user
            var user = await _loginService.CreateUserAndLogin(request.Name, request.Login, request.PasswordHash);
            if (user == null)
            {
                return base.StatusCode(StatusCodes.Status500InternalServerError, "Failed to create user.");
            }
            return await HttpContext.SigninSession(request.ExpireDelay, user);
        }
#endif

    }
}
