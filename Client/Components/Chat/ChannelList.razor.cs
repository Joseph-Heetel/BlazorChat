using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.JSInterop;
using CustomBlazorApp.Client;
using CustomBlazorApp.Client.Shared;
using CustomBlazorApp.Client.Services;
using System.Diagnostics;
using System.Collections.Specialized;
using CustomBlazorApp.Shared;

namespace CustomBlazorApp.Client.Components.Chat
{
    public partial class ChannelList
    {
        [Parameter]
        public ChannelListParams Params { get; set; } = new ChannelListParams();
        /// <summary>
        /// Invoked when the user clicks on a channel entry in the list
        /// </summary>
        [Parameter]
        public EventCallback<ItemId> OnChannelClicked { get; set; }

        private SortedList<string, ChannelListParamEntry> _channelsSorted = new SortedList<string, ChannelListParamEntry>();

        protected override void OnParametersSet()
        {
            _channelsSorted.Clear();
            foreach (var channel in Params.Channels)
            {
                _channelsSorted[channel.Name] = channel;
            }
        }

        /// <summary>
        /// Manage selected channel item
        /// </summary>
        public object Selected
        {
            get => (object)(Params.CurrentChannelId); 
            set
            {
                _ = Task.Run(async () =>
                {
                    ItemId channelId = (ItemId)value;
                    if (Params.CurrentChannelId != channelId)
                    {
                        await OnChannelClicked.InvokeAsync(channelId);
                    }
                });
            }
        }
    }
}
