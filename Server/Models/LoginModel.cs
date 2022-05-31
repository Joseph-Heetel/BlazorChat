using CustomBlazorApp.Server.Services.DatabaseWrapper;
using CustomBlazorApp.Shared;
using Microsoft.Azure.Cosmos;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CustomBlazorApp.Server.Models
{
    /// <summary>
    /// Database entry storing user login name, password hash and associated user Id
    /// </summary>
    class LoginModel : DBModelBase
    {
        public const string PartitionPath = "/p";

        [JsonPropertyName("id")]
        public string? Login { get; set; }

        [PartitionProperty(GenerateFromId = true)]
        [JsonPropertyName("p")]
        public string? Partition { get; set; }

        [JsonPropertyName("userId")]
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId UserId { get; set; }

        [JsonPropertyName("passwordHash")]
        [JsonConverter(typeof(ByteArrayConverter))]
        public ByteArray Passwordhash { get; set; }

        public override bool CheckWellFormed()
        {
            return !string.IsNullOrEmpty(Login)
                && !string.IsNullOrEmpty(Partition)
                && !UserId.IsZero
                && Passwordhash.Data?.Length > 0;
        }
    }
}
