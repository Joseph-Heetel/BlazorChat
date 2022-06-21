using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BlazorChat.Shared
{
    /// <summary>
    /// Encodes information required to begin a new session
    /// </summary>
    public class LoginRequest
    {
        public string Login { get; set; } = string.Empty;
        [JsonConverter(typeof(ByteArrayConverter))]
        public ByteArray PasswordHash { get; set; } = default;
        public TimeSpan? ExpireDelay { get; set; } = default;
    }

    public class RegisterRequest : LoginRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Encodes information on a user session
    /// </summary>
    public class Session
    {
        public long ExpiresTS { get; set; } = default;
        [JsonIgnore]
        public DateTimeOffset Expires
        {
            get => DateTimeOffset.FromUnixTimeMilliseconds(ExpiresTS);
            set => ExpiresTS = value.ToUnixTimeMilliseconds();
        }
        public User? User { get; set; }

        public Session() { }
        public Session(DateTimeOffset expires = default, User? user = default, string login = "")
        {
            Expires = expires;
            User = user;
        }
    }
}
