using CustomBlazorApp.Shared;

namespace CustomBlazorApp.Client.Components
{
    public struct SendControlParams
    {
        public ItemId CurrentChannelId { get; set; } = default;

        public SendControlParams() { }
    }
}
