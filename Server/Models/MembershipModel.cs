using BlazorChat.Server.Services.DatabaseWrapper;
using BlazorChat.Shared;
using System.Text.Json.Serialization;

namespace BlazorChat.Server.Models
{
    public class MembershipModel : DBModelBase
    {
        [JsonPropertyName("id")]
        public string Key
        {
            get => MakeId(ChannelId, UserId);
            set { }
        }

        [JsonPropertyName("channelId")]
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId ChannelId { get; set; }

        [JsonPropertyName("userId")]
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId UserId { get; set; }

        [PartitionProperty(GenerateFromId = false)]
        [JsonPropertyName("p")]
        public string Partition { get; } = "0";

        [JsonPropertyName("lastRead")]
        public long LastRead { get; set; }

        [JsonPropertyName("joined")]
        public long Joined { get; set; }


        public static string MakeId(ItemId channelId, ItemId userId)
        {
            return $"{channelId}{userId}";
        }

        public override bool CheckWellFormed()
        {
            return !ChannelId.IsZero
                && !UserId.IsZero
                && Joined != default;
        }

        public Participation ToApiType()
        {
            return new Participation()
            {
                Id = UserId,
                JoinedTS = Joined,
                LastReadTS = LastRead
            };
        }

        public static MembershipModel FromApiType(Participation participation, ItemId channelId = default)
        {
            return new MembershipModel()
            {
                UserId = participation.Id,
                ChannelId = channelId,
                Joined = participation.JoinedTS,
                LastRead = participation.LastReadTS
            };
        }
    }
}
