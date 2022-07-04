using BlazorChat.Server.Services.DatabaseWrapper;
using BlazorChat.Shared;
using System.Text.Json.Serialization;

namespace BlazorChat.Server.Models
{
    public class FormRequestModel : DBModelBase
    {
        [JsonPropertyName("id")]
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId Id { get; set; }

        [PartitionProperty(GenerateFromId = true)]
        [JsonPropertyName("p")]
        public string? Partition { get; set; } = default;

        [JsonPropertyName("formId")]
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId FormId { get; set; }

        [JsonPropertyName("recipientId")]
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId RecipientId { get; set; }

        [JsonPropertyName("expires")]
        public long Expires { get; set; }

        [JsonPropertyName("allowMultipleAnswers")]
        public bool AllowMultipleAnswers { get; set; }

        [JsonPropertyName("answerCount")]
        public int AnswerCount { get; set; }


        public override bool CheckWellFormed()
        {
            return !Id.IsZero && !FormId.IsZero && !RecipientId.IsZero && Expires > 0 && AnswerCount >= 0;
        }

        public FormRequest ToApiType()
        {
            return new FormRequest()
            {
                Id = Id,
                FormId = FormId,
                RecipientId = RecipientId,
                ExpiresTS = Expires,
                AllowMultipleAnswers = AllowMultipleAnswers,
                AnswerCount = AnswerCount
            };
        }

        public static FormRequestModel FromApiType(FormRequest request)
        {
            return new FormRequestModel()
            {
                Id = request.Id,
                FormId = request.FormId,
                RecipientId = request.RecipientId,
                Expires = request.ExpiresTS,
                AllowMultipleAnswers = request.AllowMultipleAnswers,
                AnswerCount = request.AnswerCount
            };
        }
    }
}
