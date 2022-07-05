using BlazorChat.Server.Services;
using BlazorChat.Shared;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace BlazorChat.Server.AdminApi
{
    [Route("api/admin/users")]
    [ApiController]
    public class AdminUserController : ControllerBase
    {
        public AdminUserController(IUserDataService userdata, ILoginDataService login, IAdminAuthService auth, IServiceProvider serviceProvider, ITokenService tokenService)
        {
            _userService = userdata;
            _loginService = login;
            _adminAuthService = auth;
            _storageService = serviceProvider.GetService<IStorageService>();
            _tokenService = tokenService;
        }

        private readonly IUserDataService _userService;
        private readonly ILoginDataService _loginService;
        private readonly IAdminAuthService _adminAuthService;
        private readonly IStorageService? _storageService;
        private readonly ITokenService _tokenService;

        [Route("")]
        [HttpGet]
        public async Task<ActionResult<User[]>> GetAllUsers()
        {
            if (!await _adminAuthService.ValidateBearer(Request))
            {
                return Unauthorized();
            }
            return await _userService.GetUsers();
        }

        [Route("{userIdstr}")]
        [HttpGet]
        public async Task<ActionResult<User>> GetUser(string userIdstr)
        {
            if (!await _adminAuthService.ValidateBearer(Request))
            {
                return Unauthorized();
            }
            if (!ItemId.TryParse(userIdstr, out ItemId userId))
            {
                return BadRequest();
            }
            var user = await _userService.GetUser(userId);
            if (user == null)
            {
                return NotFound();
            }
            return user;
        }

        [Route("create")]
        [HttpPost]
        public async Task<ActionResult<User>> CreateUser([FromQuery] string name, [FromQuery] string login, [FromQuery] string password)
        {
            if (!await _adminAuthService.ValidateBearer(Request))
            {
                return Unauthorized();
            }
            if (string.IsNullOrEmpty(name))
            {
                return BadRequest();
            }
            if (string.IsNullOrEmpty(login))
            {
                return BadRequest();
            }
            if (string.IsNullOrEmpty(password))
            {
                return BadRequest();
            }
            using HashAlgorithm hash = HashAlgorithm.Create(HashAlgorithmName.SHA256.Name!)!;
            ByteArray hashedPassword = new ByteArray(hash.ComputeHash(Encoding.UTF8.GetBytes(password)));
            User? user = await _loginService.CreateUserAndLogin(name, login, hashedPassword);
            if (user == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to create user.");
            }
            return user;
        }

        [Route("{userIdstr}")]
        [HttpDelete]
        public async Task<ActionResult> DeleteUser(string userIdstr)
        {
            if (!await _adminAuthService.ValidateBearer(Request))
            {
                return Unauthorized();
            }
            if (!ItemId.TryParse(userIdstr, out ItemId userId))
            {
                return BadRequest();
            }

            // Replaces the user with a deleted item
            
            if (_storageService != null)
            {
                // Deleting the users storage container removes the avatar
                await _storageService.DeleteContainer(userId);
                // Unset avatar reference
                await _userService.UpdateAvatar(userId, null);
            }
            return await _userService.UpdateUserName(userId, "Deleted User") ? Ok() : NotFound();
        }

        [Route("{userIdstr}")]
        [HttpPut]
        public async Task<ActionResult> UpdateUser(string userIdstr, [FromQuery] string? newName)
        {
            if (!await _adminAuthService.ValidateBearer(Request))
            {
                return Unauthorized();
            }
            if (!ItemId.TryParse(userIdstr, out ItemId userId))
            {
                return BadRequest();
            }
            if (newName == null)
            {
                return BadRequest();
            }
            return await _userService.UpdateUserName(userId, newName) ? Ok() : NotFound();
        }

        [Route("makesession")]
        [HttpPost]
        public async Task<ActionResult<string>> MakeSession([FromQuery] string? login)
        {
            if (!await _adminAuthService.ValidateBearer(Request))
            {
                return Unauthorized();
            }
            if (login == null)
            {
                return BadRequest();
            }
            string token = _tokenService.MakeToken(login, DateTimeOffset.UtcNow + TimeSpan.FromHours(8));
            string host = HttpContext.Request.Host.Value;
            return $"https://{host}/chat?token={Uri.EscapeDataString(token)}";
        }
    }
}
