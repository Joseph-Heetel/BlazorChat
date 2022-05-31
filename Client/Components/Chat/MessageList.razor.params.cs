using BlazorChat.Shared;

namespace BlazorChat.Client.Components
{
    public struct MessageListParams
    {
        public IReadOnlyCollection<Message> Messages { get; set; } = Array.Empty<Message>();
        public IReadOnlyCollection<Participation> Participants { get; set; } = Array.Empty<Participation>();
        public ItemId SelfUserId { get; set; } = default;
        public bool ScrollToEnd { get; set; } = false;

        public MessageListParams() { }
    }
}
