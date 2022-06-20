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
using System.Collections;

namespace BlazorChat.Client.Components.Chat
{
    public partial class SearchDialog
    {
        [CascadingParameter]
        MudDialogInstance MudDialog { get; set; } = new MudDialogInstance();
        private Channel _currentChannel { get; set; } = new Channel();
        private IDictionary<ItemId, User> _userCache { get; set; } = new Dictionary<ItemId, User>();
        private List<UserViewParams> _availableUsers = new List<UserViewParams>();
        private string _searchStr { get; set; } = "";
        private User? _author { get; set; } = null;
        private DateRange _range { get; set; } = new DateRange((DateTime.Now - TimeSpan.FromDays(14)).Date, DateTime.Now.Date);
        private List<MessageViewParams> _results { get; set; } = new List<MessageViewParams>();
        private Message? _selectedResult { get; set; } = null;

        private enum EState
        {
            Config,
            Fetch,
            Result
        }
        private EState _state { get; set; } = EState.Config;

        protected override void OnParametersSet()
        {
            _results.Clear();
            _selectedResult = null;
            _currentChannel = ChatStateService.CurrentChannel.State ?? new Channel();
            _userCache = ChatStateService.UserCache.State;
            
            _availableUsers.Clear();
            foreach (var participant in _currentChannel.Participants)
            {
                _userCache.TryGetValue(participant.Id, out User? user);
                _availableUsers.Add(new UserViewParams()
                { UserId = participant.Id, User = user });
            }
        }

        /// <summary>
        /// Launches the search query
        /// </summary>
        /// <returns></returns>
        async Task Search()
        {
            MessageSearchQuery query = new MessageSearchQuery()
            {
                ChannelId = _currentChannel.Id,
                AuthorId = _author?.Id ?? default,
                Search = _searchStr,
            };
            if (DisplayRangePicker && _range.Start.HasValue && _range.End.HasValue)
            {
                query.After = (DateTimeOffset)_range.Start;
                query.Before = (DateTimeOffset)_range.End;
            }
            _state = EState.Fetch;
            this.StateHasChanged();
            var results = await ChatApiService.SearchMessages(query);
            _results.Clear();
            _selectedResult = null;
            foreach (var result in results)
            {
                _results.Add(new MessageViewParams()
                {
                    Message = result,
                    ShowAuthor = true
                });
            }
            _state = EState.Result;
            this.StateHasChanged();
        }
        private async void JumpToMessage()
        {
            if (_selectedResult != null)
                await ChatStateService.SetHighlightedMessage(_selectedResult.ChannelId, _selectedResult.Id);
            MudDialog.Close(DialogResult.Ok(_selectedResult?.Id ?? default));
        }
        private void Reconfigure()
        {
            _state = EState.Config;
            _results.Clear();
            _selectedResult = null;
            this.StateHasChanged();
        }
        void Cancel() => MudDialog.Cancel();
    }
}