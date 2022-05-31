using System.Text.Json.Serialization;

namespace BlazorChat.Shared
{
    /// <summary>
    /// Participation is a channel context sensitive helper type for communicating membership data in the server and client API
    /// </summary>
    public class Participation : ItemBase
    {
        public long JoinedTS { get; set; }
        [JsonIgnore]
        public DateTimeOffset Joined
        {
            get => DateTimeOffset.FromUnixTimeMilliseconds(JoinedTS);
            set => JoinedTS = value.ToUnixTimeMilliseconds();
        }

        public long LastReadTS { get; set; }
        [JsonIgnore]
        public DateTimeOffset LastRead
        {
            get => DateTimeOffset.FromUnixTimeMilliseconds(LastReadTS);
            set => LastReadTS = value.ToUnixTimeMilliseconds();
        }

        public Participation() { }
        public Participation(ItemId userId, long joined, long lastread)
            : base(userId)
        {
            JoinedTS = joined;
            LastReadTS = lastread;
        }
    }
}
