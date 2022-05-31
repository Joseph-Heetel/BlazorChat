using CustomBlazorApp.Shared;
using System.Text;

namespace CustomBlazorApp.Server.Services
{
    /// <summary>
    /// Service which authenticates admin bearer tokens
    /// </summary>
    public interface IAdminAuthService
    {
        /// <summary>
        /// Returns true, if request header has a valid Bearer token set
        /// </summary>
        public Task<bool> ValidateBearer(HttpRequest request);
    }

    public class BearerCompareAdminAuthService : IAdminAuthService
    {
        private static string? s_key = Environment.GetEnvironmentVariable("AdminApiBearerToken");

        public Task<bool> ValidateBearer(HttpRequest request)
        {
            if (s_key != null)
            {
                if (request.Headers.TryGetValue("Authorization", out var value))
                {
                    string? bearer = value.FirstOrDefault();
                    if (!string.IsNullOrEmpty(bearer) && bearer.StartsWith("Bearer "))
                    {
                        bearer = bearer.Substring("Bearer ".Length);
                        return Task.FromResult(s_key == bearer);
                    }
                }
            }
            return Task.FromResult(false);
        }
    }

    public class NoAuthAdminAuthService : IAdminAuthService
    {
        public Task<bool> ValidateBearer(HttpRequest request)
        {
            return Task.FromResult(true);
        }
    }
}
