using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BlazorChat.Shared
{
    public class Call : ItemBase
    {
        public enum EState
        {
            Pending,
            Negotiating,
            Ongoing,
            Terminated
        }

        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId Initiator { get; set; }
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId Recipient { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EState State { get; set; }
        public long ExpiresTS { get; set; }
        [JsonIgnore]
        public DateTimeOffset Expires
        {
            get => DateTimeOffset.FromUnixTimeMilliseconds(ExpiresTS);
            set => ExpiresTS = value.ToUnixTimeMilliseconds();
        }

        public bool HasExpired()
        {
            if (State == EState.Pending)
            {
                return DateTimeOffset.UtcNow > Expires;
            }
            else
            {
                return false;
            }
        }
    }

    public class PendingCall : ItemBase
    {
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId CallerId { get; set; }
    }


    public class NegotiationMessage
    {
        public string? type { get; set; }
        public string? sdp { get; set; } = null;
        public string? candidate { get; set; } = null;
        public string? sdpMid { get; set; } = null;
        public double sdpMLineIndex { get; set; } = 0;
    }
}
