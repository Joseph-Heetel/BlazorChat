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
using BlazorChat.Client;
using BlazorChat.Client.Shared;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using BlazorChat.Shared;

namespace BlazorChat.Client.Components.Chat
{
    public struct ChannelInfoParams {
        public ItemId ChannelId { get; set; } = default;
        public Channel? Channel { get; set; } = null;

        public ChannelInfoParams() { }
    }

    public partial class ChannelInfo
    {
        [Parameter]
        public ChannelInfoParams Params { get; set; } = default;

        private string _id = "";
        private string _created = "";
        private string _users = "";
        private bool _hasChannel = false;

        protected override void OnParametersSet()
        {
            _id = $"[{Params.ChannelId}]";
            _hasChannel = !Params.ChannelId.IsZero;
            if (Params.Channel != null)
            {
                _created = Params.Channel.Created.LocalDateTime.ToString("d");
                _users = Params.Channel.Participants.Count.ToString();
            }
            else
            {
                _created = "";
                _users = "0";
            }
        }
    }
}