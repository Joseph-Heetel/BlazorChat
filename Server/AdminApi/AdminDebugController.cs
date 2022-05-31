using BlazorChat.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace BlazorChat.Server.AdminApi
{
    [Route("api/admin/debug")]
    [ApiController]
    public class AdminDebugController : ControllerBase
    {
        private IAdminAuthService _adminAuthService { get; }
        public AdminDebugController(IAdminAuthService auth) { _adminAuthService = auth; }

        [HttpGet]
        [Route("ping")]
        public async Task<ActionResult<string>> Ping()
        {
            if (!await _adminAuthService.ValidateBearer(Request))
            {
                return Unauthorized();
            }

            return Ok("pong");
        }
    }
}
