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

namespace BlazorChat.Client.Components
{
    public partial class UserEditDialog : IDisposable
    {
        [CascadingParameter]
        public MudDialogInstance MudDialog { get; set; } = new MudDialogInstance();

        private User? _selfUser = null;
        private UserViewParams _selfUserParams = default;
        private string _userName = "";
        private string _oldPassword = "";
        private string _newPassword = "";
        private string _newPasswordRepeat = "";
        private string _passwordError = "";
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
            _process = Loc["pedit_nameprocess"];
            this.StateHasChanged();
            await ChatApiService.UpdateUsername(_userName);
            _process = "";
            this.StateHasChanged();
        }

        private void processPasswordFeedback()
        {
            string oldError = _passwordError;
            _passwordError = "";
            if (!string.IsNullOrEmpty(_oldPassword) && !string.IsNullOrEmpty(_newPassword) && !string.IsNullOrEmpty(_newPasswordRepeat))
            {
                if (_newPassword.Length < ChatConstants.PASSWORD_MIN)
                {
                    _passwordError = Loc["pedit_tooshort"];
                }
                else if (_newPassword != _newPasswordRepeat)
                {
                    _passwordError = Loc["pedit_nomatch"];
                }
            }
            if (oldError != _passwordError)
            {
                this.StateHasChanged();
            }
        }

        private async Task updatePassword()
        {
            if (string.IsNullOrEmpty(_oldPassword) || string.IsNullOrEmpty(_newPassword) || string.IsNullOrEmpty(_newPasswordRepeat))
            {
                return;
            }
            if (!string.IsNullOrEmpty(_passwordError))
            {
                return;
            }
            _process = Loc["pedit_pwprocess"];
            this.StateHasChanged();
            bool success = await ChatApiService.UpdatePassword(_oldPassword, _newPassword);
            _process = "";
            if (!success)
            {
                Snackbar.Add(Loc["pedit_pwfail"], Severity.Error);
            }
            else
            {
                _oldPassword = "";
                _newPassword = "";
                _newPasswordRepeat = "";
            }
            this.StateHasChanged();
        }

        private async Task uploadAvatar(InputFileChangeEventArgs args)
        {
            if (_selfUser == null)
            {
                return;
            }
            IBrowserFile file = args.File;
            string? errormsg = null;
            if (file.Size > ChatConstants.MAX_AVATAR_SIZE)
            {
                errormsg = string.Format(Loc["send_file_toobig", FileHelper.MakeHumanReadableFileSize(ChatConstants.MAX_AVATAR_SIZE)]);
            }
            if (!FileHelper.IsValidMimeType(file.ContentType) || !FileHelper.IsImageMime(file.ContentType))
            {
                errormsg = string.Format(Loc["send_file_invalidtype"], file.ContentType);
            }
            if (errormsg != null)
            {
                Snackbar.Add(errormsg, Severity.Error);
                return;
            }
            _process = Loc["pedit_uploadprocess"];
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