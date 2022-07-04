using BlazorChat.Server.Services.DatabaseWrapper;
using BlazorChat.Shared;
using System.Text.Json.Serialization;

namespace BlazorChat.Server.Models
{
    public class MessageModel : DBModelBase
    {
        [JsonPropertyName("id")]
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId Id { get; set; }

        [JsonPropertyName("authorId")]
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId AuthorId { get; set; }

        [PartitionProperty(GenerateFromId = false)]
        [JsonPropertyName("channelId")]
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId ChannelId { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonPropertyName("media")]
        public MediaModel? Media { get; set; }

        [JsonPropertyName("formRequestid")]
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId FormRequestId { get; set; }

        public override bool CheckWellFormed()
        {
            bool hasContent = !string.IsNullOrEmpty(Body)
                || (Media != null && Media.CheckWellFormed());
            return !Id.IsZero
                && !ChannelId.IsZero
                && !AuthorId.IsZero
                && Created != default
                && hasContent;

        }

        public Message ToApiType()
        {
            FileAttachment? attachment = null;
            if (Media != null)
            {
                attachment = Media.ToApiType();
            }
            return new Message()
            {
                Id = Id,
                ChannelId = ChannelId,
                AuthorId = AuthorId,
                CreatedTS = Created,
                Body = Body ?? "",
                Attachment = attachment,
                FormRequestId = FormRequestId
            };
        }

        public static MessageModel FromApiType(Message message)
        {
            return new MessageModel()
            {
                Id = message.Id,
                AuthorId = message.AuthorId,
                ChannelId = message.ChannelId,
                Created = message.CreatedTS,
                Body = message.Body,
                Media = message.Attachment != null ? MediaModel.FromApiType(message.Attachment) : null,
                FormRequestId = message.FormRequestId
            };
        }
    }
}
