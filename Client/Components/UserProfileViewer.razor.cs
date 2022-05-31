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
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using CustomBlazorApp.Shared;
using System.Text.Json;

namespace CustomBlazorApp.Client.Components
{
    public struct UserProfileViewerParams
    {
        public UserProfileViewerParams() { }

        public User? User { get; set; } = null;
        public Participation? Participation { get; set; } = null;
        public Channel? Channel { get; set; } = null;
    }

    public partial class UserProfileViewer
    {
        [Parameter]
        public UserProfileViewerParams Params { get; set; } = default;

        private string _id = "";
        private string _created = "";
        private bool _hasParticipation = false;
        private string _channelname = "";
        private string _joined = "";
        private string _lastread = "";
        private UserViewParams _userviewparams = default;

        protected override void OnParametersSet()
        {
            _id = "";
            _created = "";
            _hasParticipation = false;
            _channelname = "";
            _joined = "";
            _lastread = "";
            if (Params.User != null)
            {
                _id = $"[{Params.User.Id}]";
                _created = Params.User.Created.ToString("d");
                _userviewparams = new UserViewParams()
                {
                    DisplayOnlineState = true,
                    User = Params.User,
                    UserId = Params.User.Id
                };
            }
            if (Params.Channel != null && Params.Participation != null)
            {
                _hasParticipation = true;
                _channelname = Params.Channel.Name;
                _joined = Params.Participation.Joined.ToString("d");
                _lastread = Params.Participation.LastRead.ToString("d");
            }
        }
    }
}