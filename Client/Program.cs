using BlazorChat.Client;
using BlazorChat.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Net.Http;
using MudBlazor.Services;
using MudBlazor;
using Blazored.LocalStorage;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json;
using System.Globalization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

//builder.Services.AddSingleton(client => new HttpClient() { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)});
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddSingleton<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddSingleton<IChatApiService, ChatApiService>();
builder.Services.AddSingleton<IChatStateService, ChatStateService>();
builder.Services.AddSingleton<IChatHubService, ChatHubService>();
builder.Services.AddSingleton<ICallService, WebRTCCallservice>();
builder.Services.AddSingleton<IMediaResolverService, MediaResolverService>();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddSingleton<ILocalCacheService, LocalCacheService>();
builder.Services.AddSingleton<IThemeService, ThemeService>();
builder.Services.AddScoped<IDialogCloseService, DialogCloseService>();
builder.Services.AddSingleton<IMessageDispatchService, MessageDispatchService>();
builder.Services.AddOptions();
builder.Services.AddHttpClient();
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopRight;

    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 5000;
    config.SnackbarConfiguration.HideTransitionDuration = 300;
    config.SnackbarConfiguration.ShowTransitionDuration = 300;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});
builder.Services.AddAuthorizationCore();
//"CustomBlazorChat", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.RemoveAll<IHttpMessageHandlerBuilderFilter>();
builder.Services.AddLocalization();

var host = builder.Build();
await host.RunAsync();
