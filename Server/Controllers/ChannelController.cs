using CustomBlazorApp.Server.Services;
using CustomBlazorApp.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CustomBlazorApp.Server.Controllers
{
    [Route("api/channels")]
    [ApiController]
    public class ChannelController : ControllerBase
    {
        private readonly IChannelDataService _channelService;

        public ChannelController(IChannelDataService channelData)
        {
            _channelService = channelData;
        }


        /// <summary>
        /// Endpoint for accessing all channels the user making the request is member of.
        /// </summary>
        /// <returns>Full channel data for all matching channels.</returns>
        [Route("")]
        [HttpGet]
        public async Task<ActionResult<Channel[]>> Get()
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            // Get Ids of channels user is member of
            var channelIds = await _channelService.GetChannels(userId);
            if (channelIds == null)
            {
                return Array.Empty<Channel>();
            }

            // Fetch information on all channels
            List<Task<Channel?>> getChannels = new List<Task<Channel?>>();
            foreach (var channelId in channelIds)
            {
                getChannels.Add(_channelService.GetChannel(channelId, userId));
            }
            if (getChannels.Count == 0)
            {
                return Array.Empty<Channel>();
            }
            await Task.WhenAll(getChannels);

            // Map channel task results to output list
            List<Channel> channels = new List<Channel>(getChannels.Count);
            foreach (var getChannel in getChannels)
            {
                if (getChannel.Result != null)
                {
                    channels.Add(getChannel.Result);
                }
            }

            return channels.ToArray();
        }

        /// <summary>
        /// Endpoint for accessing Channel <paramref name="channelId"/>
        /// </summary>
        /// <returns>Full channel information</returns>
        [Route("{channelIdstr}")]
        [HttpGet]
        public async Task<ActionResult<Channel>> Get(string channelIdstr)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            if (!ItemId.TryParse(channelIdstr, out ItemId channelId))
            {
                return BadRequest();
            }

            // Make sure channel exists and requesting user is a member
            if (!await _channelService.IsMember(channelId, userId))
            {
                return NotFound();
            }

            // Retrieve full channel info
            var channel = await _channelService.GetChannel(channelId, userId);
            if (channel == null)
            {
                // at this point channel existence is guaranteed, so not getting information is an internal error.
                return base.StatusCode(StatusCodes.Status500InternalServerError, "Failed to load channel info.");
            }

            return channel;
        }

        /// <summary>
        /// Endpoint for setting user read horizon
        /// </summary>
        [Route("{channelIdstr}/readhorizon/{timestampstr}")]
        [HttpPatch]
        public async Task<ActionResult> UpdateReadHorizon(string channelIdstr, string timestampstr)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            if (!ItemId.TryParse(channelIdstr, out ItemId channelId))
            {
                return BadRequest();
            }

            if (!long.TryParse(timestampstr, out long timestamp))
            {
                return BadRequest();
            }

            // Make sure channel exists and requesting user is a member
            Participation? participation = await _channelService.GetParticipation(channelId, userId);
            if (participation == null)
            {
                return NotFound();
            }

            // Updating read horizons backwards is not necessary and a sign of inefficient implementation on the clientside
            if (participation.LastReadTS >= timestamp)
            {
                return BadRequest("Read Horizon should only be updated forward.");
            }

            // Update read horizon
            var success = await _channelService.UpdateReadHorizon(channelId, userId, timestamp);
            if (!success)
            {
                return base.StatusCode(StatusCodes.Status500InternalServerError, "Failed to update read horizon.");
            }

            return Ok();
        }
    }
}
