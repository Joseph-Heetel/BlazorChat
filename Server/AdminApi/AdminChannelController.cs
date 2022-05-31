using CustomBlazorApp.Server.Services;
using CustomBlazorApp.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace CustomBlazorApp.Server.AdminApi
{
    [Route("api/admin/channels")]
    [ApiController]
    public class AdminChannelController : ControllerBase
    {
        private readonly IChannelDataService _channelService;
        private readonly IMessageDataService _messageService;
        private readonly IAdminAuthService _adminAuthService;

        public AdminChannelController(IChannelDataService channels, IMessageDataService message, IAdminAuthService auth)
        {
            _channelService = channels;
            _messageService = message;
            _adminAuthService = auth;
            base.ModelState.Clear();
        }


        [Route("")]
        [HttpGet]
        public async Task<ActionResult<Channel[]>> GetAllChannels()
        {
            if (!await _adminAuthService.ValidateBearer(Request))
            {
                return Unauthorized();
            }
            return await _channelService.GetChannels();
        }

        [Route("{channelIdstr}")]
        [HttpGet]
        public async Task<ActionResult<Channel>> GetChannel(string channelIdstr)
        {
            if (!await _adminAuthService.ValidateBearer(Request))
            {
                return Unauthorized();
            }
            if (!ItemId.TryParse(channelIdstr, out ItemId channelId))
            {
                return BadRequest();
            }
            var channel = await _channelService.GetChannel(channelId, default);
            if (channel == null)
            {
                return NotFound();
            }
            return channel;
        }

        [Route("create")]
        [HttpPost]
        public async Task<ActionResult<Channel>> CreateChannel(ChannelCreateRequest request)
        {
            if (!await _adminAuthService.ValidateBearer(Request))
            {
                return Unauthorized();
            }
            if (request == null)
            {
                return BadRequest();
            }
            Channel? channel = await _channelService.CreateChannel(request.Name, new HashSet<ItemId>(request.UserIds));
            if (channel == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to create channel.");
            }
            return channel;
        }

        [Route("{channelIdstr}")]
        [HttpDelete]
        public async Task<ActionResult> DeleteChannel(string channelIdstr)
        {
            if (!await _adminAuthService.ValidateBearer(Request))
            {
                return Unauthorized();
            }
            if (!ItemId.TryParse(channelIdstr, out ItemId channelId))
            {
                return BadRequest();
            }
            return await _channelService.DeleteChannel(channelId) ? Ok() : NotFound();
        }

        [Route("{channelIdstr}/members/{userIdstr}")]
        [HttpPost]
        public async Task<ActionResult> AddMember(string channelIdstr, string userIdstr)
        {
            if (!await _adminAuthService.ValidateBearer(Request))
            {
                return Unauthorized();
            }
            if (!ItemId.TryParse(channelIdstr, out ItemId channelId) || !ItemId.TryParse(userIdstr, out ItemId userId))
            {
                return BadRequest();
            }
            if (!await _channelService.ChannelExists(channelId))
            {
                return NotFound();
            }
            return await _channelService.AddMember(channelId, userId) ? Ok() : NotFound();
        }

        [Route("{channelIdstr}/members/{userIdstr}")]
        [HttpDelete]
        public async Task<ActionResult> RemoveMember(string channelIdstr, string userIdstr)
        {
            if (!await _adminAuthService.ValidateBearer(Request))
            {
                return Unauthorized();
            }
            if (!ItemId.TryParse(channelIdstr, out ItemId channelId) || !ItemId.TryParse(userIdstr, out ItemId userId))
            {
                return BadRequest();
            }
            return await _channelService.RemoveMember(channelId, userId) ? Ok() : NotFound();
        }

        [Route("{channelIdstr}")]
        [HttpPut]
        public async Task<ActionResult> Update(string channelIdstr, [FromQuery] string newName)
        {
            if (!await _adminAuthService.ValidateBearer(Request))
            {
                return Unauthorized();
            }
            if (!ItemId.TryParse(channelIdstr, out ItemId channelId))
            {
                return BadRequest();
            }
            if (!await _channelService.ChannelExists(channelId))
            {
                return NotFound();
            }
            return await _channelService.UpdateChannelName(channelId,  newName) ? Ok() : NotFound();
        }

        [Route("{channelIdstr}/systemmessage")]
        [HttpPost]
        public async Task<ActionResult<ItemId>> PostSystemMessage(string? channelIdstr, [FromQuery] string? body)
        {
            if (!await _adminAuthService.ValidateBearer(Request))
            {
                return Unauthorized();
            }
            if (!ItemId.TryParse(channelIdstr, out ItemId channelId))
            {
                return BadRequest();
            }
            if (!await _channelService.ChannelExists(channelId))
            {
                return NotFound();
            }

            if (string.IsNullOrEmpty(body))
            {
                return BadRequest("No message body!");
            }

            var message = await _messageService.CreateMessage(channelId, ItemId.SystemId, body, null);
            return message != null ? Ok(message.Id) : StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
