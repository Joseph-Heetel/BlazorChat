using BlazorChat.Shared;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BlazorChat.Server.Services
{
    /// <summary>
    /// Generates an validates signed tokens
    /// </summary>
    public interface ITokenService
    {
        /// <summary>
        /// Returns base64 encoded signed token
        /// </summary>
        /// <param name="login">User login to sign the token for</param>
        /// <param name="expires">Expiration time</param>
        public string MakeToken(string login, DateTimeOffset expires);
        /// <summary>
        /// Validates a token
        /// </summary>
        /// <param name="token">base64 encoded token</param>
        /// <param name="login">Login if valid, <see cref="string.Empty"/> otherwise</param>
        /// <param name="expires">Expiration time if valid, default otherwise</param>
        /// <returns>True, if token has valid signature and login and expiration claims are set and valid</returns>
        public bool ValidateToken(string token, out string login, out DateTimeOffset expires);

    }
    public class JsonWebTokenService : ITokenService
    {
        private readonly JwtSecurityTokenHandler _jwtHandler;
        private readonly SymmetricSecurityKey _jwtSecret;

        public JsonWebTokenService()
        {
            _jwtHandler = new JwtSecurityTokenHandler();
            
            // Configure security key
            string? secret = Environment.GetEnvironmentVariable(EnvironmentVarKeys.JWTSECRET);
            byte[] bytes;
            if (!string.IsNullOrEmpty(secret))
            {
                // Configure with UTF8 represantation (The jwtHandler will automatically hash down or pad to correct size)
                bytes = Encoding.UTF8.GetBytes(secret);
            }
            else
            {
                // Fallback: Configure key with random 256 bit sequence.
                // This means that JWTs will only have a valid signature if the server was not restarted after it was signed!
                using RandomNumberGenerator rng = RandomNumberGenerator.Create();
                bytes = new byte[32];
                rng.GetBytes(bytes);
            }
            _jwtSecret = new SymmetricSecurityKey(bytes);
        }

        public string MakeToken(string login, DateTimeOffset expires)
        {
            // Configure claims and expiration time
            SecurityTokenDescriptor descriptor = new SecurityTokenDescriptor()
            {
                Claims = new Dictionary<string, object>() {
                    { JwtRegisteredClaimNames.Name, login}, 
                    {JwtRegisteredClaimNames.Exp, expires.ToUnixTimeSeconds().ToString()} 
                },
                Expires = expires.ToUniversalTime().DateTime,
                SigningCredentials = new SigningCredentials(_jwtSecret, SecurityAlgorithms.HmacSha256Signature)
            };
            // Signs and base64-encodes the JWT
            return _jwtHandler.CreateEncodedJwt(descriptor);
        }

        public bool ValidateToken(string tokenstr, out string login, out DateTimeOffset expires)
        {
            login = "";
            expires = default;
            // In this simplified example
            // - no issuer (makes sure that this validator can validate tokens for the used issuer, if there are multiple) and
            // - no audience (makes sure that the requesting party is the one the token was intended for)
            // validation was implemented.
            TokenValidationParameters parameters = new TokenValidationParameters()
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                IssuerSigningKey = _jwtSecret
            };
            try
            {
                // Validate. If invalid, will throw an exception
                var principal = _jwtHandler.ValidateToken(tokenstr, parameters, out SecurityToken token);

                // Decode claims
                login = principal.FindFirst(JwtRegisteredClaimNames.Name)?.Value ?? "";
                var expiresclaim = principal.FindFirst(JwtRegisteredClaimNames.Exp);
                if (long.TryParse(expiresclaim?.Value, out long unixseconds))
                {
                    expires = DateTimeOffset.FromUnixTimeSeconds(unixseconds);
                }

                // Token is valid if validation succeeded and the required claims are set
                return !string.IsNullOrEmpty(login) && expires > DateTimeOffset.UtcNow;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
