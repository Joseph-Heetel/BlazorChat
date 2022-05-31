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
using System.Text.Json;
using BlazorChat.Shared;
using System.Security.Cryptography;

namespace BlazorChat.Client.Components
{
    public partial class UserView
    {
        [Parameter]
        public UserViewParams Params { get; set; }
        [Parameter]
        public string Style { get; set; } = "";
        [Parameter]
        public Size Size { get; set; } = Size.Small;

        private string _displayName = string.Empty;
        private string _textStyle = "";

        private AvatarParams _avatarParams = default;

        protected override void OnParametersSet()
        {
            _displayName = string.Empty;
            if (Params.UserId.IsZero)
            {
                _displayName = "[?]";
            }
            else
            {
                if (Params.User != null)
                {
                    _displayName = Params.User.Name;
                }
                if (string.IsNullOrEmpty(_displayName))
                {
                    _displayName = $"[{Params.UserId}]";
                }
                if (Params.UserId == Params.SelfUserId)
                {
                    _displayName = "You";
                }
            }
            _avatarParams = new AvatarParams()
            {
                DisplayOnlineState = Params.DisplayOnlineState,
                User = Params.User,
                UserId = Params.UserId
            };
            switch (Size)
            {
                case Size.Small:
                    _textStyle = "font-size: 1em";
                    break;
                case Size.Medium:
                    _textStyle = "font-size: 1.45em";
                    break;
                case Size.Large:
                    _textStyle = "font-size: 2em";
                    break;
            }
            this.StateHasChanged();

        }
    }
}