using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Blackjack.Data;
using Blackjack.Data.Entities;
using Blackjack.Data.Wallet;
using Blackjack.Protocol.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Blackjack.Server.Auth
{
    /// <summary>
    /// Registro, inicio de sesión, perfil e historial.
    ///
    /// Todo lo que necesita el cliente para tener una cuenta persistente. El
    /// juego en sí va por SignalR; esto es REST porque son operaciones
    /// puntuales de petición y respuesta.
    /// </summary>
    public static class AuthEndpoints
    {
        public static void MapAuthEndpoints(this WebApplication app)
        {
            RouteGroupBuilder auth = app.MapGroup("/api/auth");

            auth.MapPost("/register", RegisterAsync);
            auth.MapPost("/login", LoginAsync);

            RouteGroupBuilder me = app.MapGroup("/api/me").RequireAuthorization();

            me.MapGet("/", GetProfileAsync);
            me.MapGet("/history", GetHistoryAsync);
            me.MapGet("/ledger", GetLedgerAsync);
        }

        private static async Task<IResult> RegisterAsync(
            RegisterRequest request,
            UserManager<AppUser> users,
            IWalletService wallet,
            TokenService tokens)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest(new { error = "Hacen falta correo y contraseña." });

            string displayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? request.Email.Split('@')[0]
                : request.DisplayName.Trim();

            if (displayName.Length > 40) displayName = displayName[..40];

            var user = new AppUser
            {
                UserName = request.Email,
                Email = request.Email,
                DisplayName = displayName
            };

            IdentityResult result = await users.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                // Identity ya valida longitud, complejidad y correo duplicado.
                return Results.BadRequest(new { errors = result.Errors.Select(e => e.Description) });
            }

            decimal balance = await wallet.EnsureAccountAsync(user.Id);
            (string token, DateTime expires) = tokens.CreateToken(user);

            return Results.Ok(new AuthResponse
            {
                Token = token,
                ExpiresUtc = expires,
                UserId = user.Id,
                DisplayName = user.DisplayName,
                Balance = balance
            });
        }

        private static async Task<IResult> LoginAsync(
            LoginRequest request,
            UserManager<AppUser> users,
            IWalletService wallet,
            TokenService tokens,
            BlackjackDbContext db)
        {
            AppUser? user = await users.FindByEmailAsync(request.Email);

            // Mismo mensaje para usuario inexistente y contraseña incorrecta:
            // distinguirlos permitiría averiguar qué correos están registrados.
            if (user == null || !await users.CheckPasswordAsync(user, request.Password))
                return Results.Unauthorized();

            if (await users.IsLockedOutAsync(user))
                return Results.Problem("Cuenta bloqueada temporalmente por intentos fallidos.", statusCode: 423);

            user.LastSeenUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();

            decimal balance = await wallet.EnsureAccountAsync(user.Id);
            (string token, DateTime expires) = tokens.CreateToken(user);

            return Results.Ok(new AuthResponse
            {
                Token = token,
                ExpiresUtc = expires,
                UserId = user.Id,
                DisplayName = user.DisplayName,
                Balance = balance
            });
        }

        private static async Task<IResult> GetProfileAsync(
            ClaimsPrincipal principal,
            BlackjackDbContext db,
            IWalletService wallet)
        {
            if (!TryGetUserId(principal, out Guid userId)) return Results.Unauthorized();

            AppUser? user = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return Results.NotFound();

            PlayerStats? stats = await db.PlayerStats
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId);

            return Results.Ok(new ProfileResponse
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName,
                CreatedUtc = user.CreatedUtc,
                Balance = await wallet.GetBalanceAsync(userId),
                Stats = MapStats(stats)
            });
        }

        private static async Task<IResult> GetHistoryAsync(
            ClaimsPrincipal principal,
            BlackjackDbContext db,
            int limit = 20)
        {
            if (!TryGetUserId(principal, out Guid userId)) return Results.Unauthorized();

            limit = Math.Clamp(limit, 1, 100);

            List<RoundRecord> rounds = await db.RoundRecords
                .AsNoTracking()
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.PlayedUtc)
                .ThenByDescending(r => r.Id)
                .Take(limit)
                .Include(r => r.Hands)
                .ToListAsync();

            return Results.Ok(rounds.Select(MapRound).ToList());
        }

        private static async Task<IResult> GetLedgerAsync(
            ClaimsPrincipal principal,
            IWalletService wallet,
            int limit = 50)
        {
            if (!TryGetUserId(principal, out Guid userId)) return Results.Unauthorized();

            limit = Math.Clamp(limit, 1, 200);

            IReadOnlyList<LedgerTransaction> entries = await wallet.GetHistoryAsync(userId, limit);

            return Results.Ok(entries.Select(e => new
            {
                e.Type,
                e.Amount,
                e.BalanceAfter,
                e.RoundId,
                e.CreatedUtc
            }).ToList());
        }

        private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
            => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

        private static PlayerStatsDto MapStats(PlayerStats? stats)
        {
            if (stats == null) return new PlayerStatsDto();

            return new PlayerStatsDto
            {
                RoundsPlayed = stats.RoundsPlayed,
                HandsPlayed = stats.HandsPlayed,
                HandsWon = stats.HandsWon,
                HandsLost = stats.HandsLost,
                HandsPushed = stats.HandsPushed,
                HandsSurrendered = stats.HandsSurrendered,
                Blackjacks = stats.Blackjacks,
                Busts = stats.Busts,
                TotalWagered = stats.TotalWagered,
                TotalReturned = stats.TotalReturned,
                BiggestWin = stats.BiggestWin,
                NetResult = stats.NetResult
            };
        }

        private static RoundHistoryDto MapRound(RoundRecord round) => new()
        {
            RoundId = round.RoundId,
            TableId = round.TableId,
            PlayedUtc = round.PlayedUtc,
            SeatIndex = round.SeatIndex,
            MainBet = round.MainBet,
            InsuranceBet = round.InsuranceBet,
            TotalReturned = round.TotalReturned,
            NetProfit = round.NetProfit,
            DealerCards = round.DealerCards,
            DealerTotal = round.DealerTotal,
            DealerBlackjack = round.DealerBlackjack,
            Hands = round.Hands
                .OrderBy(h => h.HandIndex)
                .Select(h => new HandHistoryDto
                {
                    HandIndex = h.HandIndex,
                    Cards = h.Cards,
                    Total = h.Total,
                    Outcome = h.Outcome.ToString(),
                    Bet = h.Bet,
                    Returned = h.Returned,
                    IsBlackjack = h.IsBlackjack,
                    IsFromSplit = h.IsFromSplit,
                    IsDoubled = h.IsDoubled
                })
                .ToList()
        };
    }
}
