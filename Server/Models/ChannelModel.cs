using BlazorChat.Server.Services.DatabaseWrapper;
using BlazorChat.Shared;
using System.Text.Json.Serialization;

namespace BlazorChat.Server.Models
{
    public class ChannelModel : DBModelBase
    {
        public const string PartitionPath = "/p";
        [JsonPropertyName("id")]
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId Id { get; set; }
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("created")]
        public long Created { get; set; }
        [JsonPropertyName("lastMessage")]
        public long LastMessage { get; set; }
        [PartitionProperty(GenerateFromId = true)]
        [JsonPropertyName("p")]
        public string? Partition { get; set; }

        public override bool CheckWellFormed()
        {
            return !Id.IsZero
                && !string.IsNullOrEmpty(Name)
                && !string.IsNullOrEmpty(Partition)
                && Created != default;
        }

        public static ChannelModel FromApiType(Channel channel)
        {
            return new ChannelModel()
            {
                Id = channel.Id,
                Name = channel.Name,
                Created = channel.CreatedTS,
                LastMessage = channel.LastMessageTS
            };
        }

        public Channel ToApiType()
        {
            return new Channel()
            {
                Id = Id,
                Name = Name ?? string.Empty,
                CreatedTS = Created,
                LastMessageTS = LastMessage
            };
        }
    }
}
