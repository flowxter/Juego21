using System;

namespace Blackjack.Server.Tables
{
    /// <summary>
    /// Asiento desde el punto de vista de la mesa: quién lo ocupa y qué ha
    /// apostado. Es distinto de Blackjack.Core.Rounds.Seat, que solo existe
    /// mientras dura una ronda y solo sabe de cartas.
    /// </summary>
    public sealed class TableSeat
    {
        public TableSeat(int index)
        {
            Index = index;
        }

        public int Index { get; }

        /// <summary>Identificador de Identity. Null si el asiento está libre.</summary>
        public Guid? PlayerId { get; private set; }

        public string? PlayerName { get; private set; }

        public bool IsOccupied => PlayerId.HasValue;

        public bool IsConnected { get; private set; }

        /// <summary>Cuándo se cayó. Null si sigue conectado.</summary>
        public DateTime? DisconnectedAtUtc { get; private set; }

        public decimal MainBet { get; private set; }

        public decimal PerfectPairsBet { get; private set; }

        public decimal TwentyOnePlus3Bet { get; private set; }

        public bool HasBet => MainBet > 0m;

        /// <summary>
        /// El jugador dio el visto bueno para repartir ya. Se borra al empezar
        /// cada ventana de apuestas: es una decisión por ronda, no permanente.
        /// </summary>
        public bool IsReady { get; private set; }

        /// <summary>Total cobrado en la ronda anterior, para el cartel de resultado.</summary>
        public decimal LastRoundReturned { get; set; }

        public decimal TotalStaked => MainBet + PerfectPairsBet + TwentyOnePlus3Bet;

        public void Occupy(Guid playerId, string playerName)
        {
            PlayerId = playerId;
            PlayerName = playerName;
            IsConnected = true;
            DisconnectedAtUtc = null;
        }

        public void Vacate()
        {
            PlayerId = null;
            PlayerName = null;
            IsConnected = false;
            DisconnectedAtUtc = null;
            ClearBets();
            LastRoundReturned = 0m;
        }

        public void MarkDisconnected(DateTime nowUtc)
        {
            IsConnected = false;
            DisconnectedAtUtc = nowUtc;
        }

        public void MarkReconnected()
        {
            IsConnected = true;
            DisconnectedAtUtc = null;
        }

        /// <summary>
        /// True si lleva caído más que la ventana de gracia y toca liberar el
        /// asiento para que otro pueda sentarse.
        /// </summary>
        public bool HasAbandonedSeat(DateTime nowUtc, int reconnectWindowSeconds)
        {
            if (IsConnected || DisconnectedAtUtc == null) return false;
            return (nowUtc - DisconnectedAtUtc.Value).TotalSeconds > reconnectWindowSeconds;
        }

        public void SetBets(decimal main, decimal perfectPairs, decimal twentyOnePlus3)
        {
            MainBet = main;
            PerfectPairsBet = perfectPairs;
            TwentyOnePlus3Bet = twentyOnePlus3;
        }

        public void ClearBets()
        {
            MainBet = 0m;
            PerfectPairsBet = 0m;
            TwentyOnePlus3Bet = 0m;
            IsReady = false;
        }

        /// <summary>Solo tiene sentido estar listo si ya se apostó.</summary>
        public void MarkReady()
        {
            if (HasBet) IsReady = true;
        }
    }
}
