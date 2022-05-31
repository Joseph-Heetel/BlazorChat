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
using BlazorChat.Client.Components.Calls;

namespace BlazorChat.Client.Components.Chat
{
    public sealed partial class ParticipantList : IDisposable
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
            ChatApiService.SelfUser.StateChanged += SelfUser_StateChanged;
            SelfUser_StateChanged(ChatApiService.SelfUser.State);
            ChatStateService.UserCache.StateChanged += UserCache_StateChanged;
        }

        private void SelfUser_StateChanged(User? value)
        {
            _selfUserId = value?.Id ?? default;
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
                          Channel = Params.Channel,
                          SelfUserId = _selfUserId
                        },
                        OnlineState = onlinestate ?? participant.LastRead.ToString("d")
                    };
                    _participantsSorted.Add(sortkey, current);
                }
            }
        }

        private void openProfileViewer(UserProfileViewerParams param)
        {
            DialogParameters parameters = new DialogParameters()
            {
                [nameof(UserProfileViewDialog.Params)] = param
            };
            DialogOptions options = new DialogOptions()
            {
                NoHeader = true
            };
            _dialogService.Show<UserProfileViewDialog>("", parameters, options);
        }

        public void Dispose()
        {
            ChatApiService.SelfUser.StateChanged -= SelfUser_StateChanged;
            ChatStateService.UserCache.StateChanged -= UserCache_StateChanged;
        }
    }
}