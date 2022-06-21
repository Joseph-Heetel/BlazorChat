using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazorChat.Shared
{
    public static class ChatConstants
    {
        /// <summary>
        /// Maximum character length of the message body
        /// </summary>
        public const int MESSAGE_BODY_MAX = 2048;
        /// <summary>
        /// Minimum character length of the message body
        /// </summary>
        public const int MESSAGE_BODY_MIN = 1;
        /// <summary>
        /// Maximum character length of login strings
        /// </summary>
        public const int LOGIN_MAX = 256;
        /// <summary>
        /// Minimum character length of login strings
        /// </summary>
        public const int LOGIN_MIN = 4;
        /// <summary>
        /// Maximum character length of usernames
        /// </summary>
        public const int USERNAME_MAX = 128;
        /// <summary>
        /// Minimum character length of usernames
        /// </summary>
        public const int USERNAME_MIN = 4;
        /// <summary>
        /// Size of password hash in bytes
        /// </summary>
        public const int PASSWORD_HASH_SIZE = 256 / 8;
        /// <summary>
        /// Minimum character length of password. No maximum since it is handled in hashed form everywhere except the user interface.
        /// </summary>
        public const int PASSWORD_MIN = 8;
        /// <summary>
        /// Maximum count of messages transferred in one fetch request
        /// </summary>
        public const int MESSAGE_FETCH_MAX = 100;
        /// <summary>
        /// Default count of messages transferred in one fetch request
        /// </summary>
        public const int MESSAGE_FETCH_DEFAULT = 32;
        /// <summary>
        /// Maximum amount of messages retained in the clients memory at one time
        /// </summary>
        public const int MESSAGE_STORE_MAX = 128;
        /// <summary>
        /// Maximum count of users fetched at once
        /// </summary>
        public const int USER_FETCH_MAX = 50;
        /// <summary>
        /// Timeout for the receiving party to answer a pending call (the server terminates the call on expiration)
        /// </summary>
        public static readonly TimeSpan PENDINGCALLTIMEOUT = TimeSpan.FromMinutes(2);
        /// <summary>
        /// Maximum size of file accepted as upload
        /// </summary>
        public const long MAX_FILE_SIZE = 16 * 1024 * 1024;
        /// <summary>
        /// Maximum size of avatar file accepted as upload
        /// </summary>
        public const long MAX_AVATAR_SIZE = 256 * 1024;
    }
}
