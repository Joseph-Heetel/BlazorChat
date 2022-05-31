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

namespace CustomBlazorApp.Client.Shared
{
    public interface IThemeInfo
    {
        public MudTheme? Theme { get; }
        public bool IsDarkMode { get; }
    }
    public class ThemeInfo : IThemeInfo
    {
        public MudTheme? Theme { get; set; }
        public bool IsDarkMode { get; set; }
    }

    public partial class MainLayout
    {
        MudThemeProvider? themeProvider;
        private ThemeInfo? themeInfo;
        private bool isDarkMode = false;
        protected override void OnInitialized()
        {
            ThemeService.IsDarkMode.StateChanged += IsDarkMode_StateChanged;
            themeInfo = new ThemeInfo()
            {
                Theme = themeProvider?.Theme,
                IsDarkMode = isDarkMode
            };
        }

        private void IsDarkMode_StateChanged(bool value)
        {
            isDarkMode = value;
            if (themeInfo != null)
            {
                themeInfo.IsDarkMode = value;
            }
            StateHasChanged();
        }

        public void Dispose()
        {
            ThemeService.IsDarkMode.StateChanged -= IsDarkMode_StateChanged;
        }
    }
}