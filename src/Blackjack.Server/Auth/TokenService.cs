using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Blackjack.Data.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Blackjack.Server.Auth
{
    public sealed class TokenService
    {
        private readonly JwtOptions _options;

        public TokenService(IOptions<JwtOptions> options)
        {
            _options = options.Value;

            if (string.IsNullOrWhiteSpace(_options.Key) || _options.Key.Length < 32)
            {
                throw new InvalidOperationException(
                    "Jwt:Key debe tener al menos 32 caracteres. Configúrala por variable de entorno, no en appsettings.json.");
            }
        }

        /// <summary>
        /// Firma un token para el jugador.
        ///
        /// El id va en NameIdentifier porque es el claim que SignalR lee para
        /// rellenar Context.UserIdentifier, que es de donde el hub saca la
        /// identidad sin fiarse de nada que mande el cliente.
        /// </summary>
        public (string Token, DateTime ExpiresUtc) CreateToken(AppUser user)
        {
            DateTime expires = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.DisplayName),
                new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expires,
                signingCredentials: credentials);

            return (new JwtSecurityTokenHandler().WriteToken(token), expires);
        }
    }
}
