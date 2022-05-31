using CustomBlazorApp.Server.Services;
using CustomBlazorApp.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CustomBlazorApp.Server.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserDataService _userService;
        private readonly IChannelDataService _channelService;

        public UserController(IUserDataService userData, IChannelDataService channelData)
        {
            _userService = userData;
            _channelService = channelData;
        }

        /// <summary>
        /// Endpoint for getting a single user
        /// </summary>
        /// <param name="requestedUserId"></param>
        /// <returns>User Information</returns>
        [Route("{requestedUserIdstr}")]
        [HttpGet]
        public async Task<ActionResult<User>> GetSingle(string requestedUserIdstr)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId requesterUserId))
            {
                return Unauthorized();
            }

            if (!ItemId.TryParse(requestedUserIdstr, out ItemId requestedUserId))
            {
                return BadRequest();
            }

            // Make sure they share a channel
            if (requestedUserId != requesterUserId)
            {
                // Get all channels the requesting user is member of
                var test = await _channelService.GetChannels(requesterUserId);
                if (test == null)
                {
                    return NotFound();
                }
                bool found = false;
                foreach (var channelId in test)
                {
                    // Test membership of the requested other user for all channels, break if membership exists
                    if (await _channelService.IsMember(channelId, requestedUserId))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return NotFound();
                }
            }

            // Get user information
            var user = await _userService.GetUser(requestedUserId);
            if (user == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to acquire user data.");
            }
            return user;
        }

        [Route("username/{name}")]
        [HttpPatch]
        public async Task<ActionResult> ChangeUsername(string name)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

           bool success = await _userService.UpdateUserName(userId, name);
            if (!success)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
            return Ok();
        }


        /// <summary>
        /// Endpoint for receiving multiple users at once
        /// </summary>
        [Route("multi")]
        [HttpPost]
        public async Task<ActionResult<User[]>> GetMulti(ItemId[]? requestedUserIds)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId requesterUserId))
            {
                return Unauthorized();
            }


            // Make sure request is well formed
            if (requestedUserIds == null || requestedUserIds.Length == 0)
            {
                return BadRequest("Provide collection of user Ids.");
            }
            if (requestedUserIds.Length > ChatConstants.USER_FETCH_MAX)
            {
                return BadRequest($"May only request at most {ChatConstants.USER_FETCH_MAX} users at once.");
            }

            // Make sure they share a channel
            var channels = await _channelService.GetChannels(requesterUserId);
            if (channels == null || channels.Length == 0)
            {
                // The user is not in any channels, so cannot see any users
                return NotFound();
            }

            // this set contains all users which we know share a channel with the requester
            HashSet<ItemId> confirmedUserIds = new HashSet<ItemId>();

            {
                // Set contains all users which have not been confirmed to share a channel yet
                HashSet<ItemId> unconfirmedUserIds = new HashSet<ItemId>(requestedUserIds);

                // Maps user Ids to membership lookup tasks
                Dictionary<ItemId, Task<bool>> lookupTasks = new Dictionary<ItemId, Task<bool>>();

                foreach (var channelId in channels)
                {
                    lookupTasks.Clear();

                    // Create the lookup task for all unconfirmed users
                    foreach (var requestedUserId in unconfirmedUserIds)
                    {
                        lookupTasks.Add(requestedUserId, _channelService.IsMember(channelId, requestedUserId));
                    }

                    // We have already confirmed all users to be present, so no further loop runs are required
                    if (lookupTasks.Count == 0)
                    {
                        break;
                    }

                    // Perform lookups
                    await Task.WhenAll(lookupTasks.Values);

                    // Mark all ids as confirmed for which a lookup task returned a membership exists
                    foreach (var pair in lookupTasks)
                    {
                        if (pair.Value.Result)
                        {
                            unconfirmedUserIds.Remove(pair.Key);
                            confirmedUserIds.Add(pair.Key);
                        }
                    }
                }

                // No ids could be confirmed
                if (confirmedUserIds.Count == 0)
                {
                    return NotFound();
                }
            }

            // Get detailed user information on all confirmed users
            List<Task<User?>> tasks = new List<Task<User?>>();
            foreach (var confirmedUserId in confirmedUserIds)
            {
                tasks.Add(_userService.GetUser(confirmedUserId));
            }
            await Task.WhenAll(tasks);

            // Map user info fetch tasks to output
            List<User> result = new List<User>(tasks.Count);
            foreach (var task in tasks)
            {
                if (task.Result != null)
                {
                    result.Add(task.Result);
                }
            }

            if (result.Count == 0)
            {
                // Failing here indicates that confirmed users were found, but no valid data could be loaded
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to acquire user data.");
            }

            return result.ToArray();
        }
    }
}
