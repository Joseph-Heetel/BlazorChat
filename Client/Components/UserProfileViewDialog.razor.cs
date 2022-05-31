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
using System.Text.Json;
using BlazorChat.Client.Components.Calls;

namespace BlazorChat.Client.Components
{
    public struct UserProfileViewerParams
    {
        public UserProfileViewerParams() { }

        public User? User { get; set; } = null;
        public ItemId SelfUserId { get; set; } = default;
        public Participation? Participation { get; set; } = null;
        public Channel? Channel { get; set; } = null;
    }

    public partial class UserProfileViewDialog
    {
        [CascadingParameter]
        MudDialogInstance MudDialog { get; set; } = new MudDialogInstance();

        [Parameter]
        public UserProfileViewerParams Params { get; set; } = default;

        private string _id = "";
        private string _created = "";
        private bool _hasParticipation = false;
        private string _channelname = "";
        private string _joined = "";
        private string _lastread = "";
        private bool _online = false;
        private bool _isSelf = false;
        private UserViewParams _userviewparams = default;

        protected override void OnParametersSet()
        {
            _id = "";
            _created = "";
            _hasParticipation = false;
            _channelname = "";
            _joined = "";
            _lastread = "";
            _online = false;
            _isSelf = false;
            if (Params.User != null)
            {
                _id = $"[{Params.User.Id}]";
                _created = Params.User.Created.LocalDateTime.ToString("d");
                _userviewparams = new UserViewParams()
                {
                    DisplayOnlineState = true,
                    User = Params.User,
                    UserId = Params.User.Id
                };
                _online = Params.User.Online;
                _isSelf = Params.User.Id == Params.SelfUserId;
            }
            if (Params.Channel != null && Params.Participation != null)
            {
                _hasParticipation = true;
                _channelname = Params.Channel.Name;
                _joined = Params.Participation.Joined.LocalDateTime.ToString("d");
                _lastread = Params.Participation.LastRead.LocalDateTime.ToString("d");
            }
        }

        void CallUser()
        {
            MudDialog.Close();
            User? user = Params.User;
            ItemId self = Params.SelfUserId;
            if (user != null && !self.IsZero && user.Online && user.Id != self)
            {
                DialogParameters parameters = new DialogParameters()
                {
                    [nameof(CallInitDialog.PendingCall)] = null,
                    [nameof(CallInitDialog.RemoteUser)] = user
                };
                var dialog = _dialogService.Show<CallInitDialog>("", parameters);
            }
        }
    }
}