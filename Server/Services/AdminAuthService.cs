using BlazorChat.Shared;
using System.Text;

namespace BlazorChat.Server.Services
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

    /// <summary>
    /// Validates request by comparing the Authorization field to a bearer token
    /// </summary>
    public class BearerCompareAdminAuthService : IAdminAuthService
    {
        private static readonly string? _token = Environment.GetEnvironmentVariable("AdminApiBearerToken");

        public Task<bool> ValidateBearer(HttpRequest request)
        {
            if (_token != null)
            {
                if (request.Headers.TryGetValue("Authorization", out var value))
                {
                    string? bearer = value.FirstOrDefault();
                    if (!string.IsNullOrEmpty(bearer) && bearer.StartsWith("Bearer "))
                    {
                        bearer = bearer.Substring("Bearer ".Length);
                        return Task.FromResult(_token == bearer);
                    }
                }
            }
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Will validate any request regardless of content
    /// </summary>
    public class PlaceholderAuthAdminAuthService : IAdminAuthService
    {
        public Task<bool> ValidateBearer(HttpRequest request)
        {
            return Task.FromResult(true);
        }
    }
}
