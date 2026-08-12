using System;
using System.Collections.Generic;

namespace Blackjack.Protocol.Dtos
{
    public sealed class RegisterRequest
    {
        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        /// <summary>Nombre que verán los demás en la mesa.</summary>
        public string DisplayName { get; set; } = string.Empty;
    }

    public sealed class LoginRequest
    {
        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Sesión iniciada. El token se pasa al hub como <c>access_token</c> en la
    /// query string, que es la forma estándar en SignalR porque el navegador
    /// no deja poner cabeceras en el WebSocket.
    /// </summary>
    public sealed class AuthResponse
    {
        public string Token { get; set; } = string.Empty;

        public DateTime ExpiresUtc { get; set; }

        public Guid UserId { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public decimal Balance { get; set; }
    }

    public sealed class PlayerStatsDto
    {
        public int RoundsPlayed { get; set; }

        public int HandsPlayed { get; set; }

        public int HandsWon { get; set; }

        public int HandsLost { get; set; }

        public int HandsPushed { get; set; }

        public int HandsSurrendered { get; set; }

        public int Blackjacks { get; set; }

        public int Busts { get; set; }

        public decimal TotalWagered { get; set; }

        public decimal TotalReturned { get; set; }

        public decimal BiggestWin { get; set; }

        /// <summary>
        /// Resultado acumulado. A la larga tenderá a negativo: la mesa tiene
        /// ventaja y el juego no lo esconde.
        /// </summary>
        public decimal NetResult { get; set; }
    }

    public sealed class ProfileResponse
    {
        public Guid UserId { get; set; }

        public string Email { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public decimal Balance { get; set; }

        public DateTime CreatedUtc { get; set; }

        public PlayerStatsDto Stats { get; set; } = new();
    }

    /// <summary>Una mano dentro de una ronda del historial.</summary>
    public sealed class HandHistoryDto
    {
        public int HandIndex { get; set; }

        /// <summary>Notación de póker: "8s,3c".</summary>
        public string Cards { get; set; } = string.Empty;

        public int Total { get; set; }

        public string Outcome { get; set; } = string.Empty;

        public decimal Bet { get; set; }

        public decimal Returned { get; set; }

        public bool IsBlackjack { get; set; }

        public bool IsFromSplit { get; set; }

        public bool IsDoubled { get; set; }
    }

    public sealed class RoundHistoryDto
    {
        public string RoundId { get; set; } = string.Empty;

        public string TableId { get; set; } = string.Empty;

        public DateTime PlayedUtc { get; set; }

        public int SeatIndex { get; set; }

        public decimal MainBet { get; set; }

        public decimal InsuranceBet { get; set; }

        public decimal TotalReturned { get; set; }

        public decimal NetProfit { get; set; }

        public string DealerCards { get; set; } = string.Empty;

        public int DealerTotal { get; set; }

        public bool DealerBlackjack { get; set; }

        public List<HandHistoryDto> Hands { get; set; } = new();
    }
}
