using BlazorChat.Server.Models;
using BlazorChat.Server.Services.DatabaseWrapper;
using BlazorChat.Shared;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BlazorChat.Server.Services
{
    public interface IFormDataService
    {
        /// <summary>
        /// Get a form with form Id
        /// </summary>
        public Task<JsonNode?> GetForm(ItemId formId);
        /// <summary>
        /// Create a form
        /// </summary>
        /// <returns>Valid ItemId of the form on success, zero otherwise</returns>
        public Task<ItemId> CreateForm(JsonDocument form);
        /// <summary>
        /// Return true if a formid is valid
        /// </summary>
        public Task<bool> FormExists(ItemId formId);
        /// <summary>
        /// Create a form request
        /// </summary>
        /// <param name="formId">Form id</param>
        /// <param name="recipientId">user id of recipient</param>
        /// <param name="expires">Expiration timestamp</param>
        /// <param name="allowMultiple">Allow multiple responses</param>
        /// <returns>Valid ItemId of the form request on success, zero otherwise</returns>
        public Task<ItemId> CreateFormRequest(ItemId formId, ItemId recipientId, DateTimeOffset? expires = null, bool allowMultiple = false);
        /// <summary>
        /// Get a form request
        /// </summary>
        /// <param name="requestId"></param>
        /// <returns></returns>
        public Task<FormRequest?> GetFormRequest(ItemId requestId);
        /// <summary>
        /// Record a reponse to a form request
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        public Task<ItemId> RecordResponse(FormRequest request, JsonDocument response);
        /// <summary>
        /// Get all responses
        /// </summary>
        public Task<FormResponse[]> GetResponses();
        /// <summary>
        /// Get all responses for a specific request
        /// </summary>
        public Task<FormResponse[]> GetRequestResponses(ItemId requestId);
        /// <summary>
        /// Get all responses for a specific form
        /// </summary>
        public Task<FormResponse[]> GetFormResponses(ItemId formId);
        /// <summary>
        /// Get all forms recorded. Has ["id"] field on lowest hierarchy.
        /// </summary>
        public Task<JsonNode[]> GetForms();
    }

    public class FormDataService : IFormDataService
    {
        private readonly ITableService<FormModel> _formsTable;
        private readonly ITableService<FormRequestModel> _requestsTable;
        private readonly ITableService<FormResponseModel> _responsesTable;
        private readonly IIdGeneratorService _idGenService;

        public FormDataService(IDatabaseConnection db, IIdGeneratorService idgen)
        {
            _formsTable = db.GetTable<FormModel>(DatabaseConstants.FORMSTABLE);
            _requestsTable = db.GetTable<FormRequestModel>(DatabaseConstants.FORMREQUESTSTABLE);
            _responsesTable = db.GetTable<FormResponseModel>(DatabaseConstants.FORMRESPONSESTABLE);
            _idGenService = idgen;
        }

        public async Task<JsonNode?> GetForm(ItemId formId)
        {
            var response = await _formsTable.GetItemAsync(formId.ToString());
            if (response.IsSuccess)
            {
                return response.ResultAsserted.Form;
            }
            return null;
        }

        public async Task<ItemId> CreateForm(JsonDocument form)
        {
            ItemId modelId = _idGenService.Generate();
            FormModel model = new FormModel()
            {
                Form = form.Deserialize<JsonNode>(),
                Id = modelId
            };

            var response = await _formsTable.CreateItemAsync(model);
            if (response.IsSuccess)
            {
                return model.Id;
            }
            return default;
        }

        public async Task<ItemId> CreateFormRequest(ItemId formId, ItemId recipientId, DateTimeOffset? expires = null, bool allowMultiple = false)
        {
            long expiresTs = expires?.ToUnixTimeMilliseconds() ?? long.MaxValue;
            ItemId requestId = _idGenService.Generate();
            FormRequestModel model = new FormRequestModel()
            {
                Id = requestId,
                FormId = formId,
                RecipientId = recipientId,
                Expires = expiresTs,
                AllowMultipleAnswers = allowMultiple,
                AnswerCount = 0,
            };
            if (!model.CheckWellFormed())
            {
                return default(ItemId);
            }
            var response = await _requestsTable.CreateItemAsync(model);
            return response.IsSuccess ? model.Id : default(ItemId);
        }

        public async Task<FormRequest?> GetFormRequest(ItemId requestId)
        {
            var response = await _requestsTable.GetItemAsync(requestId.ToString());
            if (response.IsSuccess)
            {
                return response.ResultAsserted.ToApiType();
            }
            return null;
        }

        public async Task<bool> FormExists(ItemId formId)
        {
            var response = await _formsTable.GetItemAsync(formId.ToString());
            return response.IsSuccess;
        }

        public async Task<ItemId> RecordResponse(FormRequest request, JsonDocument response)
        {
            ItemId responseId = _idGenService.Generate();
            FormResponseModel model = new FormResponseModel()
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                FormId = request.FormId,
                Id = responseId,
                RequestId = request.Id,
                UserId = request.RecipientId,
                Response = JsonObject.Create(response.RootElement)
            };

            if (!model.CheckWellFormed())
            {
                return default(ItemId);
            }

            var createResponse = await _responsesTable.CreateItemAsync(model);
            if (!createResponse.IsSuccess)
            {
                return default(ItemId);
            }

            var getResponse = await _requestsTable.GetItemAsync(request.Id.ToString());
            if (!getResponse.IsSuccess)
            {
                return default;
            }
            var requestModel = getResponse.ResultAsserted;
            requestModel.AnswerCount = requestModel.AnswerCount + 1;
            await _requestsTable.ReplaceItemAsync(requestModel);

            return responseId;
        }

        public async Task<FormResponse[]> GetResponses()
        {
            var queryResponse = await _responsesTable.QueryItemsAsync();
            if (!queryResponse.IsSuccess)
            {
                return Array.Empty<FormResponse>();
            }
            return queryResponse.ResultAsserted.Select(model => model.ToApiType()).ToArray();
        }

        public async Task<FormResponse[]> GetRequestResponses(ItemId requestId)
        {
            var query = $"SELECT * FROM i WHERE i.requestId = \"{requestId}\"";
            var queryResponse = await _responsesTable.QueryItemsAsync(query);
            if (!queryResponse.IsSuccess)
            {
                return Array.Empty<FormResponse>();
            }
            return queryResponse.ResultAsserted.Select(model => model.ToApiType()).ToArray();
        }

        public async Task<FormResponse[]> GetFormResponses(ItemId formId)
        {
            var query = $"SELECT * FROM i WHERE i.formId = \"{formId}\"";
            var queryResponse = await _responsesTable.QueryItemsAsync(query);
            if (!queryResponse.IsSuccess)
            {
                return Array.Empty<FormResponse>();
            }
            return queryResponse.ResultAsserted.Select(model => model.ToApiType()).ToArray();
        }

        public async Task<JsonNode[]> GetForms()
        {
            var queryResponse = await _formsTable.QueryItemsAsync();
            List<JsonNode> forms = new List<JsonNode>();
            if (queryResponse.IsSuccess)
            {
                foreach (var formResponseEntry in queryResponse.ResultAsserted)
                {
                    if (formResponseEntry.Form == null)
                    {
                        continue;
                    }
                    JsonNode form = formResponseEntry.Form;
                    form["id"] = formResponseEntry.Id.ToString();
                    forms.Add(form);
                }
            }
            return forms.ToArray();
        }
    }
}
