﻿using CustomBlazorApp.Shared;

namespace CustomBlazorApp.Client.Components
{
    public struct UserViewParams
    {
        public ItemId UserId { get; set; } = default;
        public ItemId SelfUserId { get; set; } = default;
        public User? User { get; set; } = default;
        public bool DisplayOnlineState { get; set; } = default;

        public UserViewParams() { }
    }
}
