using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CustomBlazorApp.Shared
{
    /// <summary>
    /// Represents a user interacting with chat
    /// </summary>
    public class User : ItemBase
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("createdTS")]
        public long CreatedTS { get; set; }
        [JsonIgnore]
        public DateTimeOffset Created { get => DateTimeOffset.FromUnixTimeMilliseconds(CreatedTS); set => CreatedTS = value.ToUnixTimeMilliseconds(); }
        [JsonPropertyName("online")]
        public bool Online { get; set; }
        [JsonPropertyName("avatar")]
        public FileAttachment? Avatar { get; set; }

        public User Clone() { return (this.MemberwiseClone() as User)!; }

        public User() { }
        public User(ItemId id, string name = "", DateTimeOffset created = default)
            : base(id)
        {
            Name = name;
            Created = created;
        }
    }
}
