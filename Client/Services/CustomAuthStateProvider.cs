using BlazorChat.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using System.Security.Claims;

namespace BlazorChat.Client.Services
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        public CustomAuthStateProvider(IHttpClientFactory clientFactory, NavigationManager nav)
        {
            _HttpClientFactory = clientFactory;
            _NavManager = nav;
        }

        private readonly IHttpClientFactory _HttpClientFactory;

        private readonly NavigationManager _NavManager;

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var client = _HttpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_NavManager.BaseUri);
            Session? session = await client.GetFromJSONAsyncNoExcept<Session>("api/session");
            if (session == null || session.User == null)
            {
                return new AuthenticationState(new ClaimsPrincipal());
            }
            else
            {
                // Create claim (UserId)
                Claim claim = new Claim(ClaimTypes.NameIdentifier, session.User.Id.ToString());
                // Create claimsIdentity
                ClaimsIdentity claimsIdentity = new ClaimsIdentity(new[] { claim }, "serverAuth");
                // Create claimPrincipal
                ClaimsPrincipal claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
                return new AuthenticationState(claimsPrincipal);
            }
        }
    }
}
