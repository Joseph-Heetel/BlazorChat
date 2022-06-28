using BlazorChat.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using System.Security.Claims;

namespace BlazorChat.Client.Services
{
    /// <summary>
    /// The clients session is encoded within a cookie. This custom auth state checks with the server wether the session is valid or not
    /// </summary>
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
            // Get session from server REST api (validates cookie)
            var client = _HttpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_NavManager.BaseUri);
            Session? session = await client.GetFromJSONAsyncNoExcept<Session>("api/session");

            if (session == null || session.User == null)
            {
                // Session is invalid, return empty authstate (== not logged in)
                return new AuthenticationState(new ClaimsPrincipal());
            }
            else
            {
                // Create claim (UserId)
                Claim idClaim = new Claim(ClaimTypes.NameIdentifier, session.User.Id.ToString());
                Claim loginClaim = new Claim(ClaimTypes.Email, session.Login);
                // Create claimsIdentity
                ClaimsIdentity claimsIdentity = new ClaimsIdentity(new[] { idClaim, loginClaim }, "serverAuth");
                // Create claimPrincipal
                ClaimsPrincipal claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
                return new AuthenticationState(claimsPrincipal);
            }
        }
    }
}
