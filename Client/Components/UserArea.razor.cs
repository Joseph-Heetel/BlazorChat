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

namespace BlazorChat.Client.Components
{
    public partial class UserArea : IDisposable
    {
        private UserViewParams _selfUserParams = default;
        private bool _isDarkMode = false;
        private bool _showOptions = false;

        private string _buttonIcon => _showOptions ? Icons.Filled.ArrowDropDown : Icons.Filled.ArrowDropUp;
        private string _themeModeIcon => _isDarkMode ? Icons.Filled.LightMode : Icons.Filled.DarkMode;
        private string _themeModeButtonText => _isDarkMode ? "Light Mode" : "Dark Mode";

        protected override void OnInitialized()
        {
            ThemeService.IsDarkMode.StateChanged += IsDarkMode_StateChanged;
            IsDarkMode_StateChanged(ThemeService.IsDarkMode.State);
            ApiService.SelfUser.StateChanged += SelfUser_StateChanged;
            SelfUser_StateChanged(ApiService.SelfUser.State);
        }

        private void SelfUser_StateChanged(User? value)
        {
            _selfUserParams = new UserViewParams()
            {
                DisplayOnlineState = false,
                SelfUserId = default,
                User = value,
                UserId = value?.Id ?? default
            };
            this.StateHasChanged();
        }

        private void IsDarkMode_StateChanged(bool value)
        {
            _isDarkMode = value;
            this.StateHasChanged();
        }

        private void toggleOptionsDisplay()
        {
            _showOptions = !_showOptions;
            this.StateHasChanged();
        }

        private void toggleDarkMode()
        {
            ThemeService.SetDarkMode(!_isDarkMode);
        }

        private Task logout()
        {
            return ApiService.Logout();
        }

        private void editUserProfile()
        {
            if (ApiService.SelfUser.State != null)
            {
                DialogParameters parameters = new DialogParameters();
                var dialog = DialogService.Show<UserEditDialog>("", parameters);
            }
        }

        public void Dispose()
        {
            ThemeService.IsDarkMode.StateChanged -= IsDarkMode_StateChanged;
            ApiService.SelfUser.StateChanged -= SelfUser_StateChanged;
        }
    }
}