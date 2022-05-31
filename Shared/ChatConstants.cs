using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazorChat.Shared
{
    public class ChatConstants
    {
        public const int MESSAGE_BODY_MAX = 2048;
        public const int MESSAGE_BODY_MIN = 1;
        public const int LOGIN_MAX = 256;
        public const int LOGIN_MIN = 4;
        public const int USERNAME_MAX = 128;
        public const int USERNAME_MIN = 4;
        public const int PASSWORD_HASH_SIZE = 256 / 8;
        public const int PASSWORD_MIN = 8;
        public const int MESSAGE_FETCH_MAX = 100;
        public const int MESSAGE_FETCH_DEFAULT = 32;
        public const int MESSAGE_STORE_MAX = 128;
        public const int USER_FETCH_MAX = 50;
    }
}
