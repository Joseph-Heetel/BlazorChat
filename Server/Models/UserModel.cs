using BlazorChat.Server.Services.DatabaseWrapper;
using BlazorChat.Shared;
using System.Text.Json.Serialization;

namespace BlazorChat.Server.Models
{
    public class UserModel : DBModelBase
    {
        public const string PartitionPath = "/p";

        [JsonPropertyName("id")]
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("created")]
        public long Created { get; set; }

        [PartitionProperty(GenerateFromId = true)]
        [JsonPropertyName("p")]
        public string? Partition { get; set; }

        [JsonPropertyName("avatar")]
        public MediaModel? Avatar { get; set; }

        public override bool CheckWellFormed()
        {
            bool avatarWellFormed = true;
            if (Avatar != null)
            {
                avatarWellFormed = Avatar.CheckWellFormed()
                    && FileHelper.IsImageMime(Avatar.Mimetype);
            }
            return !Id.IsZero
                && !string.IsNullOrEmpty(Name)
                && !string.IsNullOrEmpty(Partition)
                && Created != default
                && avatarWellFormed;
        }

        public static UserModel FromApiType(User user)
        {
            return new UserModel()
            {
                Id = user.Id,
                Name = user.Name,
                Created = user.CreatedTS,
                Avatar = (user.Avatar != null ? MediaModel.FromApiType(user.Avatar) : null)
            };
        }

        public User ToApiType()
        {

            return new User()
            {
                Id = Id,
                CreatedTS = Created,
                Name = Name ?? string.Empty,
                Avatar = Avatar?.ToApiType()
            };
        }
    }
}
