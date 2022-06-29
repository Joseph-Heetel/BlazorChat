using BlazorChat.Server.Services;
using BlazorChat.Shared;
using Microsoft.AspNetCore.Mvc;

namespace BlazorChat.Server.Controllers
{
    [Route("api/storage")]
    [ApiController]
    public class StorageController : ControllerBase
    {

        private readonly IStorageService? _storageService;
        private readonly IChannelDataService _channelService;
        private readonly IUserDataService _userService;
        public StorageController(IServiceProvider serviceProvider, IChannelDataService channelData, IUserDataService userData)
        {
            _storageService = serviceProvider.GetService<IStorageService>();
            _channelService = channelData;
            _userService = userData;
        }

        /// <summary>
        /// Endpoint for file upload. Expects hmtl request body containing data marked with proper mime type
        /// </summary>
        /// <param name="channelIdstr"></param>
        /// <returns></returns>
        [Route("{channelIdstr}")]
        [HttpPost]
        public async Task<ActionResult<FileAttachment>> Upload(string channelIdstr)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            if (_storageService == null)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            if (!ItemId.TryParse(channelIdstr, out ItemId channelId))
            {
                return BadRequest("Malformed channel Id.");
            }
            if (!await _channelService.IsMember(channelId, userId))
            {
                return BadRequest("Malformed channel Id.");
            }


            var stream = Request.Body;
            var mimetype = Request.Headers.ContentType.FirstOrDefault();

            if (stream == null)
            {
                return BadRequest("Malformed upload");
            }
            if (string.IsNullOrEmpty(mimetype))
            {
                return BadRequest("Malformed upload (missing MimeType).");
            }
            if (!FileHelper.IsValidMimeType(mimetype))
            {
                return new UnsupportedMediaTypeResult();
            }

            // In order to do useful work with the data and verify the size, we need to copy it into a continous memory buffer

            using MemoryStream mem = new MemoryStream((int)ChatConstants.MAX_FILE_SIZE);
            await stream.CopyToAsync(mem);
            await stream.DisposeAsync();

            if (mem.Length > ChatConstants.MAX_FILE_SIZE)
            {
                return BadRequest("File too large.");
            }

            var fileId = await _storageService.UploadFile(channelId, new FileUploadInfo() { Data = mem.ToArray(), MimeType = mimetype });
            if (fileId.IsZero)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
            return new FileAttachment()
            {
                Id = fileId,
                MimeType = mimetype,
                Size = mem.Length
            };
        }
        [Route("avatar")]
        [HttpPost]
        public async Task<ActionResult> UploadAvatar()
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            if (_storageService == null)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            var stream = Request.Body;
            var mimetype = Request.Headers.ContentType.FirstOrDefault();

            if (stream == null)
            {
                return BadRequest("Malformed upload");
            }
            if (string.IsNullOrEmpty(mimetype))
            {
                return BadRequest("Malformed upload (missing MimeType).");
            }
            if (!FileHelper.IsValidMimeType(mimetype) || !FileHelper.IsImageMime(mimetype))
            {
                return new UnsupportedMediaTypeResult();
            }
            using MemoryStream mem = new MemoryStream((int)ChatConstants.MAX_AVATAR_SIZE);
            await stream.CopyToAsync(mem);
            await stream.DisposeAsync();
            if (mem.Length > ChatConstants.MAX_AVATAR_SIZE)
            {
                return BadRequest("File too large.");
            }

            // Only one avatar is supposed to be uploaded at one time, so we just delete all files
            // associated with the user.
            await _storageService.ClearContainer(userId);

            var fileId = await _storageService.UploadFile(userId, new FileUploadInfo() { Data = mem.ToArray(), MimeType = mimetype });
            if (fileId.IsZero)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
            var attachment = new FileAttachment()
            {
                Id = fileId,
                MimeType = mimetype,
                Size = mem.Length
            };
            if (!await _userService.UpdateAvatar(userId, attachment))
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
            return Ok();
        }

        /// <summary>
        /// Access to uploaded media is never granted permanently, only temporary. This endpoint generates temporary access Urls
        /// </summary>
        /// <param name="channelIdstr"></param>
        /// <param name="fileNamestr"></param>
        /// <returns></returns>
        [Route("{channelIdstr}/{fileNamestr}")]
        [HttpGet]
        public async Task<ActionResult<TemporaryURL>> GetTemporaryMediaURL(string channelIdstr, string fileNamestr)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            if (_storageService == null)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            // Make sure channelId and fileId parse correctly
            if (!ItemId.TryParse(channelIdstr, out ItemId channelId))
            {
                return BadRequest("Malformed URI section channelId \"api/storage/channelId/fileId.ext\"");
            }
            if (string.IsNullOrEmpty(fileNamestr))
            {
                return BadRequest("Malformed URI section fileId \"api/storage/channelId/fileId.ext\"");
            }
            string[] sections = fileNamestr.Split('.');
            if (sections.Length != 2)
            {
                return BadRequest("Malformed URI section fileId \"api/storage/channelId/fileId.ext\"");
            }
            string fileIdstr = sections[0];
            string ext = sections[1];
            if (!ItemId.TryParse(fileIdstr, out ItemId fileId) || !FileHelper.IsValidExt(ext))
            {
                return BadRequest("Malformed URI section fileId \"api/storage/channelId/fileId.ext\"");
            }

            // Make sure channel exists and requesting user is a member.
            // This is required as it prevents access to files of channels the user has been removed from.
            if (!await _channelService.IsMember(channelId, userId))
            {
                return NotFound();
            }

            var url = await _storageService.GetTemporaryFileURL(channelId, fileId, FileHelper.ExtensionToMimeType(ext)!);
            if (url == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
            return url;
        }

        [Route("avatar/{avatarUserIdstr}/{fileNamestr}")]
        [HttpGet]
        public async Task<ActionResult<TemporaryURL>> GetTemporaryAvatarURL(string avatarUserIdstr, string fileNamestr)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            if (_storageService == null)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            // Make sure channelId and fileId parse correctly
            if (!ItemId.TryParse(avatarUserIdstr, out ItemId avatarUserId))
            {
                return BadRequest("Malformed URI section channelId \"api/storage/channelId/fileId.ext\"");
            }
            if (string.IsNullOrEmpty(fileNamestr))
            {
                return BadRequest("Malformed URI section fileId \"api/storage/channelId/fileId.ext\"");
            }
            string[] sections = fileNamestr.Split('.');
            if (sections.Length != 2)
            {
                return BadRequest("Malformed URI section fileId \"api/storage/channelId/fileId.ext\"");
            }
            string fileIdstr = sections[0];
            string ext = sections[1];
            if (!ItemId.TryParse(fileIdstr, out ItemId fileId) || !FileHelper.IsValidExt(ext))
            {
                return BadRequest("Malformed URI section fileId \"api/storage/channelId/fileId.ext\"");
            }

            // Make sure channel exists and requesting user is a member.
            // Make sure they share a channel
            if (avatarUserId != userId)
            {
                // Get all channels the requesting user is member of
                var test = await _channelService.GetChannels(userId);
                if (test == null)
                {
                    return NotFound();
                }
                bool found = false;
                foreach (var channelId in test)
                {
                    // Test membership of the requested other user for all channels, break if membership exists
                    if (await _channelService.IsMember(channelId, avatarUserId))
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

            var url = await _storageService.GetTemporaryFileURL(avatarUserId, fileId, FileHelper.ExtensionToMimeType(ext)!);
            if (url == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
            return url;
        }
    }
}
