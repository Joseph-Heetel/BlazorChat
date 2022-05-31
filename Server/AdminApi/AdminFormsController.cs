
using BlazorChat.Server.Hubs;
using BlazorChat.Server.Models;
using BlazorChat.Server.Services;
using BlazorChat.Shared;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BlazorChat.Server.AdminApi
{
    [Route("api/admin/forms")]
    [ApiController]
    public class AdminFormsController : ControllerBase
    {
        private readonly IFormDataService _formService;
        private readonly IAdminAuthService _adminAuthService;
        private readonly IUserDataService _userService;
        private readonly IMessageDataService _messageService;

        public AdminFormsController(IFormDataService forms, IAdminAuthService adminAuth, IUserDataService users, IMessageDataService messages)
        {
            this._formService = forms;
            this._adminAuthService = adminAuth;
            this._userService = users;
            this._messageService = messages;
        }


        [Route("{formIdStr}")]
        [HttpGet]
        public async Task<ActionResult<JsonNode>> GetForm(string formIdStr)
        {
            // check authorization
            if (!await _adminAuthService.ValidateBearer(Request))
            {
                return Unauthorized();
            }

            if (!ItemId.TryParse(formIdStr, out ItemId formId))
            {
                return BadRequest("Could not parse form id!");
            }

            var form = await _formService.GetForm(formId);
            if (form == null)
            {
                return NotFound();
            }
            return form;
        }

        [Route("")]
        [HttpPost]
        public async Task<ActionResult<string>> CreateForm()
        {
            // check authorization
            if (!await _adminAuthService.ValidateBearer(Request))
            {
                return Unauthorized();
            }


            ItemId result = default;

            try
            {
                using (StreamReader reader = new StreamReader(Request.Body))
                {
                    var json = await reader.ReadToEndAsync();
                    using (JsonDocument document = JsonDocument.Parse(json))
                    {
                        result = await _formService.CreateForm(document);
                    }
                }
            }
            catch (Exception)
            {
                return BadRequest();
            }

            return result.ToString();
        }

        public class AdminCreateFormRequest
        {
            [JsonConverter(typeof(ItemIdConverter))]
            public ItemId FormId { get; set; }
            [JsonConverter(typeof(ItemIdConverter))]
            public ItemId RecipientId { get; set; }
            public long ExpiresTS { get; set; }
            [JsonIgnore]
            public DateTimeOffset Expires { get => DateTimeOffset.FromUnixTimeMilliseconds(ExpiresTS); set => ExpiresTS = value.ToUnixTimeMilliseconds(); }
            public bool AllowMultiple { get; set; }
            [JsonConverter(typeof(ItemIdConverter))]
            public ItemId ChannelId { get; set; }
            [JsonConverter(typeof(ItemIdConverter))]
            public ItemId MessageId { get; set; }
        }

        [Route("request")]
        [HttpPost]
        public async Task<ActionResult<string>> CreateRequest(AdminCreateFormRequest? request)
        {
            // check authorization
            if (!await _adminAuthService.ValidateBearer(Request))
            {
                return Unauthorized();
            }

            if (request == null)
            {
                return BadRequest("No request data provided!");
            }

            if (request.FormId.IsZero || request.RecipientId.IsZero || request.Expires < DateTimeOffset.UtcNow)
            {
                return BadRequest("Incomplete request data provided!");
            }

            if (!await _formService.FormExists(request.FormId))
            {
                return BadRequest("Form does not exist!");
            }

            if (await _userService.GetUser(request.RecipientId) == null)
            {
                return BadRequest("Recipient does not exist!");
            }

            var message = await _messageService.GetMessage(request.ChannelId, request.MessageId);

            if (message == null)
            {
                return BadRequest("Message does not exist");
            }

            var requestId = await _formService.CreateFormRequest(request.FormId, request.RecipientId, request.Expires, request.AllowMultiple);

            if (requestId.IsZero)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            var success = await _messageService.AttachFormRequest(request.ChannelId, request.MessageId, requestId);

            return success ? Ok(requestId.ToString()) : StatusCode(StatusCodes.Status500InternalServerError);
        }

        /// <summary>
        /// Queries form responses. Parameters are optional, but exclusive.
        /// </summary>
        /// <param name="requestIdStr">(Optional) Filter responses by Id of form request</param>
        /// <param name="formIdStr">(Optional) Filter responses by Id of form</param>
        /// <returns></returns>
        [Route("responses")]
        [HttpGet]
        public async Task<ActionResult<FormResponse[]>> ListResponses([FromQuery] string? requestIdStr = null, [FromQuery] string? formIdStr = null)
        {
            // check authorization
            if (!await _adminAuthService.ValidateBearer(Request))
            {
                return Unauthorized();
            }

            if (formIdStr != null && requestIdStr != null)
            {
                return BadRequest("Only one filter may be present!");
            }

            FormResponse[] responses = Array.Empty<FormResponse>();

            if (formIdStr != null)
            {
                if (!ItemId.TryParse(formIdStr, out ItemId formId))
                {
                    return BadRequest("Malformed formIdstr query parameter!");
                }

                responses = await _formService.GetFormResponses(formId);
            }
            else if (requestIdStr != null)
            {
                if (!ItemId.TryParse(requestIdStr, out ItemId requestId))
                {
                    return BadRequest("Malformed requestIdstr query parameter!");
                }

                responses = await _formService.GetRequestResponses(requestId);
            }
            else
            {
                responses = await _formService.GetResponses();
            }

            return responses;
        }
    }
}
