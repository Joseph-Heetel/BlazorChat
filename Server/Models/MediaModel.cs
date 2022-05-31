using CustomBlazorApp.Shared;
using System.Text.Json.Serialization;

namespace CustomBlazorApp.Server.Models
{
    public class MediaModel : DBModelBase
    {
        [JsonPropertyName("id")]
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId Id { get; set; }

        [JsonPropertyName("mimetype")]
        public string? Mimetype { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        public static MediaModel FromApiType(FileAttachment attachment)
        {
            return new MediaModel()
            {
                Id = attachment.Id,
                Mimetype = attachment.MimeType,
                Size = attachment.Size
            };
        }

        public override bool CheckWellFormed()
        {
            return !Id.IsZero
                && FileHelper.IsValidMimeType(Mimetype)
                && Size != default;
        }

        public FileAttachment ToApiType()
        {
            return new FileAttachment()
            {
                Id = Id,
                MimeType = Mimetype ?? String.Empty,
                Size = Size
            };
        }
    }
}
