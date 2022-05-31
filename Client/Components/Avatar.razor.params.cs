using BlazorChat.Shared;

namespace BlazorChat.Client.Components
{
    public struct AvatarParams
    {
        public ItemId UserId { get; set; } = default;
        public User? User { get; set; } = default;
        public bool DisplayOnlineState { get; set; } = default;

        public AvatarParams() { }
    }
}