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
        public CustomAuthStateProvider(NavigationManager nav, ILocalCacheService cacheService, IChatApiService chatApi)
        {
            _apiService = chatApi;
            _navManager = nav;
            _cacheService = cacheService;
        }

        private readonly IChatApiService _apiService;

        private readonly NavigationManager _navManager;
        private readonly ILocalCacheService _cacheService;


        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            // Get session from server REST api (validates cookie)
            Session? session = await _apiService.RecoverExistingSession();
            if (session == null || session.User == null)
            {
                // Session is invalid, return empty authstate (== not logged in)
                return new AuthenticationState(new ClaimsPrincipal());
            }
            else
            {
                return MakeAuthState(session);
            }
        }

        private static AuthenticationState MakeAuthState(Session session)
        {
            // Create claim (UserId)
            Claim idClaim = new Claim(ClaimTypes.NameIdentifier, session.User!.Id.ToString());
            Claim loginClaim = new Claim(ClaimTypes.Email, session.Login);
            // Create claimsIdentity
            ClaimsIdentity claimsIdentity = new ClaimsIdentity(new[] { idClaim, loginClaim }, "serverAuth");
            // Create claimPrincipal
            ClaimsPrincipal claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
            return new AuthenticationState(claimsPrincipal);
        }
    }
}
