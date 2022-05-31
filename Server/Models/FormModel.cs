using BlazorChat.Server.Services.DatabaseWrapper;
using BlazorChat.Shared;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BlazorChat.Server.Models
{
    public class FormModel : DBModelBase
    {
        public const string PartitionPath = "/p";
        [PartitionProperty(GenerateFromId = true)]
        [JsonPropertyName("p")]
        public string Partition { get; set; } = "";
        [JsonPropertyName("form")]
        public JsonNode? Form { get; set; } = default;
        [JsonPropertyName("id")]
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId Id { get; set; } = default;

        public override bool CheckWellFormed()
        {
            return !Id.IsZero && !string.IsNullOrEmpty(Partition) && Form is JsonObject obj && obj.Count > 0;
        }
    }
}
