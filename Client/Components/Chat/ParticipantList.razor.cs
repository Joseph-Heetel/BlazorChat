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
using CustomBlazorApp.Client.Components.Calls;

namespace CustomBlazorApp.Client.Components.Chat
{
    public sealed partial class ParticipantList
    {
        [Parameter]
        public ParticipantListParams Params { get; set; }

        private struct ParticipantView
        {
            public Participation Participation;
            public string Tooltip;
            public string OnlineState;
            public UserViewParams ViewParams;
            public UserProfileViewerParams ProfileParams;
        }

        private readonly SortedList<string, ParticipantView> _participantsSorted = new SortedList<string, ParticipantView>();
        private bool _allUsersResolved = true;
        private ItemId _selfUserId;

        protected override void OnInitialized()
        {
            ChatStateService.UserCache.StateChanged += UserCache_StateChanged;
        }

        private void UserCache_StateChanged(IDictionary<ItemId, User> value)
        {
            if (!_allUsersResolved)
            {
                buildSortedParticipantList(value);
            }
        }

        protected override void OnParametersSet()
        {
            _selfUserId = ChatApiService.SelfUser.State?.Id ?? default;
            buildSortedParticipantList(ChatStateService.UserCache.State);
        }

        private void buildSortedParticipantList(IDictionary<ItemId, User>? userCache)
        {
            _participantsSorted.Clear();
            _allUsersResolved = true;
            if (Params.Participants != null)
            {
                foreach (var participant in Params.Participants)
                {

                    User? user = null;
                    string sortkey;
                    string tooltip;
                    string? onlinestate = null;
                    if (userCache != null && userCache.TryGetValue(participant.Id, out user))
                    {
                        tooltip = $"{user.Name} [{participant.Id}]";
                        sortkey = user.Name;
                        if (user.Online)
                        {
                            onlinestate = "now";
                        }
                    }
                    else
                    {
                        tooltip = $"[{participant.Id}]";
                        sortkey = $"[{participant.Id}]";
                        _allUsersResolved = false;
                    }
                    ParticipantView current = new ParticipantView()
                    {
                        Participation = participant,
                        Tooltip = tooltip,
                        ViewParams = new UserViewParams()
                        {
                            SelfUserId = default,
                            User = user,
                            UserId = participant.Id,
                            DisplayOnlineState = true
                        },
                        ProfileParams = new UserProfileViewerParams()
                        {
                          User = user,
                          Participation = participant,
                          Channel = Params.Channel
                        },
                        OnlineState = onlinestate ?? participant.LastRead.ToString("d")
                    };
                    _participantsSorted.Add(sortkey, current);
                }
            }
        }

        private void callParticipant(User user)
        {
            DialogParameters parameters = new DialogParameters();
            parameters.Add(nameof(CallInitDialog.PendingCall), null);
            parameters.Add(nameof(CallInitDialog.RemoteUser), user);
            var dialog = DialogService.Show<CallInitDialog>("", parameters);
        }
    }
}