using System;
using System.Collections.Generic;
using Blackjack.Core.Payouts;

namespace Blackjack.Data.Entities
{
    /// <summary>
    /// Una ronda jugada por un jugador concreto en un asiento concreto.
    ///
    /// Se guarda una fila por jugador y ronda, no una por mesa: así consultar
    /// "mis últimas partidas" es una lectura directa sin desenredar quién
    /// estaba sentado dónde.
    /// </summary>
    public sealed class RoundRecord
    {
        public long Id { get; set; }

        /// <summary>Identificador que agrupa a todos los jugadores de la misma ronda.</summary>
        public string RoundId { get; set; } = string.Empty;

        public string TableId { get; set; } = string.Empty;

        public Guid UserId { get; set; }

        public AppUser? User { get; set; }

        public int SeatIndex { get; set; }

        public DateTime PlayedUtc { get; set; } = DateTime.UtcNow;

        public decimal MainBet { get; set; }

        public decimal PerfectPairsBet { get; set; }

        public decimal TwentyOnePlus3Bet { get; set; }

        public decimal InsuranceBet { get; set; }

        /// <summary>Total devuelto al saldo: manos, seguro y apuestas laterales.</summary>
        public decimal TotalReturned { get; set; }

        /// <summary>Ganancia neta de la ronda. Negativa si perdió.</summary>
        public decimal NetProfit { get; set; }

        /// <summary>Mano final del croupier en notación de póker ("Ad,Kc").</summary>
        public string DealerCards { get; set; } = string.Empty;

        public int DealerTotal { get; set; }

        public bool DealerBlackjack { get; set; }

        public List<HandRecord> Hands { get; set; } = new();
    }

    /// <summary>
    /// Una de las manos del jugador en esa ronda. Hay más de una si partió.
    /// </summary>
    public sealed class HandRecord
    {
        public long Id { get; set; }

        public long RoundRecordId { get; set; }

        public RoundRecord? Round { get; set; }

        public int HandIndex { get; set; }

        /// <summary>Cartas en notación de póker ("8s,3c").</summary>
        public string Cards { get; set; } = string.Empty;

        public int Total { get; set; }

        public bool IsSoft { get; set; }

        public HandOutcome Outcome { get; set; }

        public decimal Bet { get; set; }

        public decimal Returned { get; set; }

        public bool IsBlackjack { get; set; }

        public bool IsFromSplit { get; set; }

        public bool IsDoubled { get; set; }

        public bool IsSurrendered { get; set; }
    }
}
