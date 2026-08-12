using System.Collections.Generic;

namespace Blackjack.Protocol.Dtos
{
    /// <summary>
    /// Un asiento de la mesa visto desde fuera.
    ///
    /// Nunca lleva el saldo del jugador: eso solo se le manda a su dueño por
    /// <see cref="HubMethods.Server.BalanceChanged"/>. Los demás jugadores ven
    /// las apuestas sobre la mesa, no la cartera ajena.
    /// </summary>
    public sealed class SeatDto
    {
        public int Index { get; set; }

        /// <summary>Null si el asiento está libre.</summary>
        public string? PlayerName { get; set; }

        /// <summary>
        /// False mientras el jugador está caído pero dentro de su ventana de
        /// reconexión. El cliente lo pinta atenuado en vez de vaciar el sitio.
        /// </summary>
        public bool IsConnected { get; set; }

        public decimal MainBet { get; set; }

        public decimal PerfectPairsBet { get; set; }

        public decimal TwentyOnePlus3Bet { get; set; }

        public decimal InsuranceBet { get; set; }

        public bool HasBetThisRound { get; set; }

        /// <summary>
        /// El jugador ya dijo que está listo. La mesa reparte en cuanto lo
        /// estén todos los que hayan apostado, sin agotar la cuenta atrás.
        /// </summary>
        public bool IsReady { get; set; }

        public List<HandDto> Hands { get; set; } = new List<HandDto>();

        /// <summary>Total devuelto al liquidar. Alimenta el cartel de resultado.</summary>
        public decimal LastRoundReturned { get; set; }
    }
}
