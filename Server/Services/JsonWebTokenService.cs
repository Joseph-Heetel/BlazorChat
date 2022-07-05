using BlazorChat.Shared;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BlazorChat.Server.Services
{
    public interface ITokenService
    {
        public string MakeToken(string login, DateTimeOffset expires);
        public bool ValidateToken(string token, out string login, out DateTimeOffset expires);

    }
    public class JsonWebTokenService : ITokenService
    {
        private readonly JwtSecurityTokenHandler _jwtHandler;
        private readonly SymmetricSecurityKey _jwtSecret;

        public JsonWebTokenService()
        {
            _jwtHandler = new JwtSecurityTokenHandler();
            string? secret = Environment.GetEnvironmentVariable(EnvironmentVarKeys.JWTTOKENSECRET);
            byte[] bytes;
            if (!string.IsNullOrEmpty(secret))
            {
                using HashAlgorithm hash = HashAlgorithm.Create(HashAlgorithmName.SHA256.Name!)!;
                bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(secret));
            }
            else
            {
                using RandomNumberGenerator rng = RandomNumberGenerator.Create();
                bytes = new byte[32];
                rng.GetBytes(bytes);
            }
            _jwtSecret = new SymmetricSecurityKey(bytes);
        }

        public string MakeToken(string login, DateTimeOffset expires)
        {
            SecurityTokenDescriptor descriptor = new SecurityTokenDescriptor()
            {
                Claims = new Dictionary<string, object>() {
                    { JwtRegisteredClaimNames.Name, login}, 
                    {JwtRegisteredClaimNames.Exp, expires.ToUnixTimeSeconds().ToString()} 
                },
                Expires = expires.ToUniversalTime().DateTime,
                SigningCredentials = new SigningCredentials(_jwtSecret, "HS256")
            };
            return _jwtHandler.CreateEncodedJwt(descriptor);
        }

        public bool ValidateToken(string tokenstr, out string login, out DateTimeOffset expires)
        {
            login = "";
            expires = default;
            TokenValidationParameters parameters = new TokenValidationParameters()
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                IssuerSigningKey = _jwtSecret
            };
            try
            {
                var principal = _jwtHandler.ValidateToken(tokenstr, parameters, out SecurityToken token);
                login = principal.FindFirst(JwtRegisteredClaimNames.Name)?.Value ?? "";
                var expiresclaim = principal.FindFirst(JwtRegisteredClaimNames.Exp);
                if (long.TryParse(expiresclaim?.Value, out long unixseconds))
                {
                    expires = DateTimeOffset.FromUnixTimeSeconds(unixseconds);
                }
                return !string.IsNullOrEmpty(login) && expires > DateTimeOffset.UtcNow;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
