using BlazorChat.Shared;
using System.Collections.Generic;

namespace BlazorChat.Client.Components
{
    /// <summary>
    /// Single channel item as passed to the channel list
    /// </summary>
    public struct ChannelListParamEntry
    {
        public string Name { get; set; } = string.Empty;
        public ItemId Id { get; set; } = default;
        public bool HasUnread { get; set; } = default;

        public ChannelListParamEntry() { }
    }

    /// <summary>
    /// Parameters for <see cref="ChannelList"/>
    /// </summary>
    public struct ChannelListParams
    {
        public IReadOnlyCollection<ChannelListParamEntry> Channels { get; set; } = new List<ChannelListParamEntry>();
        public ItemId CurrentChannelId { get; set; } = default;

        public ChannelListParams() { }
    }
}
