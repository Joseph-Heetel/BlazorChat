using CustomBlazorApp.Shared;

namespace CustomBlazorApp.Client.Components
{
    public struct MessageViewParams
    {
        public Message Message { get; set; }
        public int ReadCount { get; set; }
        public int TotalCount { get; set; }
        public ItemId SelfUserId { get; set; }
        public bool ShowAuthor { get; set; }
    }
}
