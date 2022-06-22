
using BlazorChat.Server.Hubs;
using BlazorChat.Server.Services;
using BlazorChat.Shared;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BlazorChat.Server.Controllers
{
    [Route("api/forms")]
    [ApiController]
    public class FormsController : ControllerBase
    {
        private readonly IFormDataService _formService;

        public FormsController(IFormDataService forms)
        {
            this._formService = forms;
        }

        /// <summary>
        /// Endpoint which returns a form (Json Schema encoded data)
        /// </summary>
        /// <param name="formIdStr"></param>
        /// <returns></returns>
        [Route("{formIdStr}")]
        [HttpGet]
        public async Task<ActionResult<JsonNode>> GetForm(string formIdStr)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            if (!ItemId.TryParse(formIdStr, out ItemId formId))
            {
                return BadRequest("Could not parse form id!");
            }

            // TODO: Perhaps access to scopes should be restricted to active form requests?

            var form = await _formService.GetForm(formId);
            if (form == null)
            {
                return NotFound();
            }
            return form;
        }

        /// <summary>
        /// Gets a request given a request id
        /// </summary>
        /// <param name="requestidstr"></param>
        /// <returns></returns>
        [Route("request/{requestidstr}")]
        [HttpGet]
        public async Task<ActionResult<FormRequest>> GetRequest(string requestidstr)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            if (!ItemId.TryParse(requestidstr, out ItemId requestId))
            {
                return BadRequest("Could not parse form id!");
            }

            var formRequest = await _formService.GetFormRequest(requestId);
            if (formRequest == null || formRequest.RecipientId != userId)
            {
                return NotFound();
            }

            return formRequest;
        }

        /// <summary>
        /// Endpoint for providing a response
        /// </summary>
        /// <param name="requestidstr"></param>
        /// <returns></returns>
        [Route("response/{requestidstr}")]
        [HttpPost]
        public async Task<ActionResult> PostResponse(string requestidstr)
        {
            // check authorization, get user Id
            if (!User.GetUserLogin(out ItemId userId))
            {
                return Unauthorized();
            }

            if (!ItemId.TryParse(requestidstr, out ItemId requestId))
            {
                return BadRequest("Could not parse form id!");
            }

            var formRequest = await _formService.GetFormRequest(requestId);
            if (formRequest == null || formRequest.RecipientId != userId)
            {
                return NotFound();
            }

            if (formRequest.AnswerCount > 0 && !formRequest.AllowMultipleAnswers)
            {
                return BadRequest("No further answers allowed!");
            }

            ItemId result = default;
            try
            {
                using (StreamReader reader = new StreamReader(Request.Body))
                {
                    var json = await reader.ReadToEndAsync();
                    using (JsonDocument document = JsonDocument.Parse(json))
                    {
                        result = await _formService.RecordResponse(formRequest, document);
                    }
                }
            }
            catch (Exception)
            {
                return BadRequest();
            }

            return result.IsZero ? StatusCode(StatusCodes.Status500InternalServerError) : Ok();
        }
    }
}
