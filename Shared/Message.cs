using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BlazorChat.Shared
{
    /// <summary>
    /// Represents a chat message
    /// </summary>
    public class Message : ItemBase
    {
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId ChannelId { get; set; }
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId AuthorId { get; set; }
        public long CreatedTS { get; set; }
        [JsonIgnore]
        public DateTimeOffset Created { get => DateTimeOffset.FromUnixTimeMilliseconds(CreatedTS); set => CreatedTS = value.ToUnixTimeMilliseconds(); }
        public string Body { get; set; } = string.Empty;
        public FileAttachment? Attachment { get; set; } = null;
        public ItemId FormRequestId { get; set; } = default;

        public Message() { }

        public Message(ItemId id, ItemId channelId = default, ItemId authorId = default, long timeStamp = 0, string body = "", FileAttachment? attachment = null)
            : base(id)
        {
            ChannelId = channelId;
            AuthorId = authorId;
            CreatedTS = timeStamp;
            Body = body;
            Attachment = attachment;
        }

        public bool HasAttachment()
        {
            return Attachment != null;
        }

        public override string ToString()
        {
            return $"[{ChannelId:X}] {AuthorId:X}: \"{Body}\" ({DateTimeOffset.FromUnixTimeMilliseconds(CreatedTS)}, {Id:X})";
        }
    }

    public class MessageGetInfo
    {
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId ChannelId { get; set; }
        public long ReferenceTS { get; set; }
        [JsonIgnore]
        public DateTimeOffset Reference { get => DateTimeOffset.FromUnixTimeMilliseconds(ReferenceTS); set => ReferenceTS = value.ToUnixTimeMilliseconds(); }
        public bool Older { get; set; }
        public int Limit { get; set; }
    }

    public class MessageCreateInfo
    {
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId ChannelId { get; set; }
        public string Body { get; set; } = string.Empty;
        public FileAttachment? Attachment { get; set; } = default;
    }

    public class MessageSearchQuery
    {
        public string Search { get; set; } = "";
        public ItemId ChannelId { get; set; } = default;
        public ItemId AuthorId { get; set; } = default;
        public long BeforeTS { get; set; } = default;
        public long AfterTS { get; set; } = default;
        [JsonIgnore]
        public DateTimeOffset Before { get => DateTimeOffset.FromUnixTimeMilliseconds(BeforeTS); set => BeforeTS = value.ToUnixTimeMilliseconds(); }
        [JsonIgnore]
        public DateTimeOffset After { get => DateTimeOffset.FromUnixTimeMilliseconds(AfterTS); set => AfterTS = value.ToUnixTimeMilliseconds(); }
    }

    public class MessageBlock
    {
        [JsonPropertyName("id")]
        public ItemId Id { get; set; } = default;
        public long OldestTimestamp { get; set; } = 0;
        public long NewestTimestamp { get; set; } = 0;
        public ItemId OlderBlock { get; set; } = default;
        public ItemId NewerBlock { get; set; } = default;
        public ItemId ChannelId { get; set; } = default;
        public List<Message> Messages { get; set; } = new List<Message>();
    }
}
