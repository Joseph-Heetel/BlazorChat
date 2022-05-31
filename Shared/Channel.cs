using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CustomBlazorApp.Shared
{
    /// <summary>
    /// User facing Api type representing a chat channel
    /// </summary>
    public class Channel : ItemBase
    {
        public string Name { get; set; } = string.Empty;
        public long CreatedTS { get; set; }
        public long LastMessageTS { get; set; }
        [JsonIgnore]
        public DateTimeOffset Created { get => DateTimeOffset.FromUnixTimeMilliseconds(CreatedTS); set => CreatedTS = value.ToUnixTimeMilliseconds(); }
        [JsonIgnore]
        public DateTimeOffset LastMessage { get => DateTimeOffset.FromUnixTimeMilliseconds(LastMessageTS); set => LastMessageTS = value.ToUnixTimeMilliseconds(); }
        public HashSet<Participation> Participants { get; set; } = new HashSet<Participation>();
        public bool HasUnread { get; set; }

    }

    public class ChannelCreateRequest
    {
        public string Name { get; set; } = string.Empty;
        public ItemId[] UserIds { get; set; } = Array.Empty<ItemId>();
    }
}
