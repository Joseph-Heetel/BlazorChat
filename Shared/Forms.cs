using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BlazorChat.Shared
{
    /// <summary>
    /// Api-facing type describing a request for a specific user to submit an answer to a form.
    /// </summary>
    public class FormRequest : ItemBase
    {
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId FormId { get; set; }
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId RecipientId { get; set; }
        public long ExpiresTS { get; set; }
        [JsonIgnore]
        public DateTimeOffset Expires { get => DateTimeOffset.FromUnixTimeMilliseconds(ExpiresTS); set => ExpiresTS = value.ToUnixTimeMilliseconds(); }
        public bool AllowMultipleAnswers { get; set; }
        /// <summary>
        /// Count of already recorded answers
        /// </summary>
        public int AnswerCount { get; set; }
    }

    /// <summary>
    /// Api-facing type detailing form answers
    /// </summary>
    public class FormResponse
    {

        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId Id { get; set; } = default;


        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId FormId { get; set; } = default;

        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId RequestId { get; set; } = default;

        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId UserId { get; set; } = default;

        public JsonObject? Response { get; set; } = default;

        public long CreatedTS { get; set; }
        [JsonIgnore]
        public DateTimeOffset Created { get => DateTimeOffset.FromUnixTimeMilliseconds(CreatedTS); set => CreatedTS = value.ToUnixTimeMilliseconds(); }
    }
}
