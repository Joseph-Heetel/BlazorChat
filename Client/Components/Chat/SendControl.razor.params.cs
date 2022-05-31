using BlazorChat.Shared;

namespace BlazorChat.Client.Components
{
    public struct SendControlParams
    {
        public ItemId CurrentChannelId { get; set; } = default;

        public SendControlParams() { }
    }
}
