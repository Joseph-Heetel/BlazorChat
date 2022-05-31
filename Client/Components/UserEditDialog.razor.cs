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
using System.Collections;

namespace CustomBlazorApp.Client.Components
{
    public partial class UserEditDialog : IDisposable
    {
        [CascadingParameter]
        public MudDialogInstance MudDialog { get; set; } = new MudDialogInstance();

        private User? _selfUser = null;
        private UserViewParams _selfUserParams = default;
        private string _userName = "";
        private string _avatarUrl = "";
        private ItemId _avatarId = default;
        private string _process = "";

        protected override void OnInitialized()
        {
            ChatApiService.SelfUser.StateChanged += SelfUser_StateChanged;
            SelfUser_StateChanged(ChatApiService.SelfUser.State);
        }

        private void SelfUser_StateChanged(User? value)
        {
            _selfUser = value;
            _userName = value?.Name ?? "";
            _selfUserParams = new UserViewParams()
            {
                DisplayOnlineState = false,
                SelfUserId = default,
                User = value,
                UserId = value?.Id ?? default
            };
            if (_avatarId != _selfUser?.Avatar?.Id)
            {
                if (!_avatarId.IsZero)
                {
                    MediaService.Unsubscribe(_avatarId, onAvatarUrlChanged);
                }
                if (_selfUser?.Avatar != null)
                {
                    _avatarId = _selfUser.Avatar.Id;
                    _avatarUrl = MediaService.GetAndSubscribe(_selfUser.Id, _selfUser.Avatar, onAvatarUrlChanged);
                }
            }
            this.StateHasChanged();
        }

        private void onAvatarUrlChanged(string avatarUrl)
        {
            _avatarUrl = avatarUrl;
            this.StateHasChanged();
        }

        private async Task updateUserName()
        {
            if (_selfUser == null || _userName == _selfUser.Name)
            {
                return;
            }
            _process = "Updating Username ...";
            this.StateHasChanged();
            await ChatApiService.UpdateUsername(_userName);
            _process = "";
            this.StateHasChanged();
        }

        private async Task uploadAvatar(InputFileChangeEventArgs args)
        {
            if (_selfUser == null)
            {
                return;
            }
            IBrowserFile file = args.File;
            if (!FileHelper.IsValidMimeType(file.ContentType) || !FileHelper.IsImageMime(file.ContentType))
            {
                return;
            }
            if (file.Size > FileHelper.MAX_FILE_SIZE)
            {
                return;
            }
            _process = "Uploading Avatar ...";
            this.StateHasChanged();
            await ChatApiService.UploadAvatar(file);
            _process = "";
            this.StateHasChanged();
        }

        private void exit()
        {
            MudDialog.Close();
        }

        public void Dispose()
        {
            ChatApiService.SelfUser.StateChanged -= SelfUser_StateChanged;
            MediaService.Unsubscribe(_avatarId, onAvatarUrlChanged);
        }
    }
}