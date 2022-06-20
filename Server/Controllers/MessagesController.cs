using BlazorChat.Server.Services;
using BlazorChat.Shared;
using Microsoft.AspNetCore.Mvc;

namespace BlazorChat.Server.Controllers
{
    [Route("api/messages")]
    [ApiController]
    public class MessagesController : ControllerBase
    {
        private readonly IChannelDataService _channelService;
        private readonly IMessageDataService _messageService;
        private readonly ITranslationService? _translationService;

        public MessagesController(IChannelDataService channelData, IMessageDataService messageData, IServiceProvider provider)
        {
            _channelService = channelData;
            _messageService = messageData;
            _translationService = provider.GetService<ITranslationService>();
        }

        /// <summary>
        /// Endpoint for creating a new message
        /// </summary>
        /// <returns>The created <see cref="Message"/></returns>
        [Route("create")]
        [HttpPost]
        public async Task<ActionResult<Message>> CreateMessage(MessageCreateInfo? request)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            // Make sure request is set and was parsed properly
            if (request == null)
            {
                return BadRequest("No information provided.");
            }

            // Make sure request is well-formed
            if (request.Body != null && request.Body.Length > ChatConstants.MESSAGE_BODY_MAX)
            {
                return BadRequest($"Message body too long! Max {ChatConstants.MESSAGE_BODY_MAX} characters!");
            }
            if (request.Attachment == null && string.IsNullOrEmpty(request.Body))
            {
                return BadRequest($"Need to provide at minimum attachment or non-empty body!");
            }
            if (request.Attachment != null)
            {
                if (!FileHelper.IsValidMimeType(request.Attachment.MimeType) || request.Attachment.Id.IsZero)
                {
                    return BadRequest("Attachment is in invalid format!");
                }
            }

            // Make sure channel exists and requesting user is a member
            if (!(await _channelService.IsMember(request.ChannelId, userId)))
            {
                return Unauthorized("Can only send messages to a channel your are member off.");
            }

            // Create the message
            var message = await _messageService.CreateMessage(request.ChannelId, userId, request.Body!, request.Attachment);
            if (message == null)
            {
                // Only an internal failure could prevent this process at this point.
                return StatusCode(StatusCodes.Status500InternalServerError, "Unable to create message.");
            }

            return message;
        }

        /// <summary>
        /// Endpoint for getting a filtered list of messages
        /// </summary>
        /// <returns>Array of matching messages</returns>
        [Route("get")]
        [HttpPost]
        public async Task<ActionResult<Message[]>> GetMessages(MessageGetInfo? request)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            // Make sure request is set and was parsed properly
            if (request == null)
            {
                return BadRequest("No filter information provided.");
            }

            // Make sure channel exists and requesting user is a member
            if (!await _channelService.IsMember(request.ChannelId, userId))
            {
                return NotFound("Could not find channel specified.");
            }

            // Get messages. Make sure request.Limit is well formed.
            return await _messageService.GetMessages(request.ChannelId, request.Reference, request.Older, Math.Clamp(request.Limit, 1, ChatConstants.MESSAGE_FETCH_MAX));
        }

        [Route("find")]
        [HttpPost]
        public async Task<ActionResult<Message[]>> FindMessages(MessageSearchQuery request)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            // Make sure request is set and was parsed properly
            if (request == null)
            {
                return BadRequest("No filter information provided.");
            }

            if (request.ChannelId.IsZero)
            {
                return BadRequest("Channel needs to be specified.");
            }

            if (string.IsNullOrWhiteSpace(request.Search))
            {
                return BadRequest("Search string needs to be specified.");
            }

            // Make sure channel exists and requesting user is a member
            if (!await _channelService.IsMember(request.ChannelId, userId))
            {
                return NotFound("Could not find channel specified.");
            }

            // Perform the query
            return await _messageService.FindMessages(request.Search, request.ChannelId, request.AuthorId, request.BeforeTS, request.AfterTS);
        }

        [Route("{channelIdstr}/{messageIdstr}")]
        [HttpGet]
        public async Task<ActionResult<Message>> Get(string channelIdstr, string messageIdstr)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            if (!ItemId.TryParse(channelIdstr, out ItemId channelId))
            {
                return BadRequest("Unable to parse channel Id.");
            }

            if (!ItemId.TryParse(messageIdstr, out ItemId messageId))
            {
                return BadRequest("Unable to parse message Id.");
            }

            // Make sure channel exists and requesting user is a member
            if (!await _channelService.IsMember(channelId, userId))
            {
                return NotFound("Could not find message.");
            }

            Message? message = await _messageService.GetMessage(channelId, messageId);
            return message != null ? Ok(message) : NotFound("Could not find message.");
        }

        [Route("translate/{channelIdstr}/{messageIdstr}/{languageCode}")]
        [HttpGet]
        public async Task<ActionResult<Message>> GetTranslated(string channelIdstr, string messageIdstr, string languageCode)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            if (!ItemId.TryParse(channelIdstr, out ItemId channelId))
            {
                return BadRequest("Unable to parse channel Id.");
            }

            if (!ItemId.TryParse(messageIdstr, out ItemId messageId))
            {
                return BadRequest("Unable to parse message Id.");
            }

            if (string.IsNullOrEmpty(languageCode))
            {
                return BadRequest("Provide language code!");
            }

            if (_translationService == null)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Translation service not configured!");
            }

            // Make sure channel exists and requesting user is a member
            if (!await _channelService.IsMember(channelId, userId))
            {
                return NotFound("Could not find message.");
            }

            Message? message = await _messageService.GetMessage(channelId, messageId);

            if (message == null)
            {
                return NotFound("Could not find message.");
            }

            string? translated = await _translationService.Translate(message.Body, languageCode);

            if (translated == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            message.Body = translated;

            return Ok(message);
        }
    }
}
