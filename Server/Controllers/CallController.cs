
using BlazorChat.Server.Hubs;
using BlazorChat.Server.Services;
using BlazorChat.Shared;
using Microsoft.AspNetCore.Mvc;

namespace BlazorChat.Server.Controllers
{
    [Route("api/calls")]
    [ApiController]
    public class CallController : ControllerBase
    {
        private readonly ICallSupportService _callService;
        private readonly IChannelDataService _channelService;


        public CallController(ICallSupportService callsupport, IChannelDataService channelService)
        {
            _callService = callsupport;
            _channelService = channelService;
        }

        [Route("")]
        [HttpGet]
        public async Task<ActionResult<IList<PendingCall>>> Get()
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            return Ok(await _callService.GetPendingCalls(userId));
        }

        [Route("init/{calleeIdStr}")]
        [HttpPost]
        public async Task<ActionResult<ItemId>> Initiate(string calleeIdStr)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            if (!ItemId.TryParse(calleeIdStr, out ItemId calleeId))
            {
                return BadRequest("Could not parse callee id!");
            }

            if (userId == calleeId)
            {
                return BadRequest("Cannot call yourself!");
            }

            // Get all channels the requesting user is member of
            var channels = await _channelService.GetChannels(userId);
            if (channels == null)
            {
                return NotFound();
            }
            bool found = false;
            foreach (var channelId in channels)
            {
                // Test membership of the requested other user for all channels, break if membership exists
                if (await _channelService.IsMember(channelId, calleeId))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return NotFound();
            }

            var online = await ConnectionMap.Users.IsConnected(calleeId);
            if (!online)
            {
                return NotFound("User offline!");
            }

            return Ok(await _callService.InitiateCall(userId, calleeId));
        }

        [Route("{callIdStr}")]
        [HttpPost]
        public async Task<ActionResult> Elevate(string callIdStr)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            if (!ItemId.TryParse(callIdStr, out ItemId callId))
            {
                return BadRequest("Could not parse call id!");
            }

            var isincall = await _callService.IsInCall(callId, userId, checkongoing: false);
            if (!isincall)
            {
                return BadRequest("Invalid call id!");
            }

            await _callService.ElevateToOngoing(callId);

            return Ok();
        }

        [Route("{callIdStr}")]
        [HttpDelete]
        public async Task<ActionResult> Terminate(string callIdStr)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            if (!ItemId.TryParse(callIdStr, out ItemId callId))
            {
                return BadRequest("Could not parse call id!");
            }

            var isincall = await _callService.IsInCall(callId, userId);
            if (!isincall)
            {
                return BadRequest("Invalid call id!");
            }

            await _callService.TerminateCall(callId);

            return Ok();
        }

        [Route("ice/{callIdStr}")]
        [HttpGet]
        public async Task<ActionResult<IceConfiguration[]>> GetIceConfigurations(string? callIdStr)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            if (!ItemId.TryParse(callIdStr, out ItemId callId))
            {
                return BadRequest("Could not parse call id!");
            }

            var isincall = await _callService.IsInCall(callId, userId);
            if (!isincall)
            {
                return BadRequest("Invalid call id!");
            }

            return await _callService.GetIceConfigurations();
        }
    }
}
