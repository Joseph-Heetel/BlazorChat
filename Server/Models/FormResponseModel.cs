using CustomBlazorApp.Server.Services.DatabaseWrapper;
using CustomBlazorApp.Shared;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CustomBlazorApp.Server.Models
{
    public class FormResponseModel : DBModelBase
    {
        public const string PartitionPath = "/p";

        [JsonPropertyName("id")]
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId Id { get; set; } = default;

        [PartitionProperty(GenerateFromId = true)]
        [JsonPropertyName("p")]
        public string Partition { get; set; } = "";

        [JsonPropertyName("formId")]
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId FormId { get; set; } = default;

        [JsonPropertyName("requestId")]
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId RequestId { get; set; } = default;

        [JsonPropertyName("userId")]
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId UserId { get; set; } = default;

        [JsonPropertyName("response")]
        public JsonObject? Response { get; set; } = default;

        [JsonPropertyName("created")]
        public long Created { get; set; } = 0;

        public override bool CheckWellFormed()
        {
            return !Id.IsZero && !FormId.IsZero && !RequestId.IsZero && !UserId.IsZero && Created > 0 && Response != null;
        }

        public FormResponse ToApiType()
        {
            return new FormResponse()
            {
                Id = Id,
                FormId = FormId,
                RequestId = RequestId,
                UserId = UserId,
                CreatedTS = Created,
                Response = Response
            };
        }

        public static FormResponseModel FromApiType(FormResponse response)
        {
            return new FormResponseModel()
            {
                Id = response.Id,
                FormId = response.FormId,
                RequestId = response.RequestId,
                UserId = response.UserId,
                Created = response.CreatedTS,
                Response = response.Response
            };
        }
    }
}
