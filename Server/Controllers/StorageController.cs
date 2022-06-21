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
        public StorageController(IStorageService storage, IChannelDataService channelData, IUserDataService userData)
        {
            _storageService = storage;
            _channelService = channelData;
            _userService = userData;
        }


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
                return StatusCode(StatusCodes.Status410Gone);
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
                return StatusCode(StatusCodes.Status410Gone);
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
                return StatusCode(StatusCodes.Status410Gone);
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

            //// Make sure channel exists and requesting user is a member
            //if (!await _Channels.IsMember(channelId, userId))
            //{
            //    return NotFound("Could not find channel specified.");
            //}

            var url = await _storageService.GetTemporaryFileURL(channelId, fileId, FileHelper.ExtensionToMimeType(ext)!);
            if (url == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
            return url;
        }
    }
}
