using System;
using Blackjack.Core.Cards;

namespace Blackjack.Core.Hands
{
    /// <summary>
    /// Una mano apostada por un jugador: las cartas más su estado de juego.
    /// Un jugador puede tener hasta 4 de estas a la vez si parte tres veces.
    /// </summary>
    public sealed class PlayerHand
    {
        public PlayerHand(decimal bet, int splitDepth = 0, bool isFromSplit = false, bool isSplitAces = false)
        {
            if (bet < 0m) throw new ArgumentOutOfRangeException(nameof(bet), bet, "La apuesta no puede ser negativa.");

            Hand = new Hand(isFromSplit, isSplitAces);
            Bet = bet;
            SplitDepth = splitDepth;
        }

        public Hand Hand { get; }

        /// <summary>
        /// Fichas en juego en esta mano. Se duplica al doblar; el ledger
        /// registra el cargo adicional en ese momento, no al final.
        /// </summary>
        public decimal Bet { get; private set; }

        /// <summary>Cuántos splits hubo antes de llegar a esta mano.</summary>
        public int SplitDepth { get; }

        public bool IsDoubled { get; private set; }

        public bool IsSurrendered { get; private set; }

        public bool HasStood { get; private set; }

        /// <summary>
        /// True cuando la mano ya no admite acciones: plantada, pasada o
        /// rendida. El turno pasa a la siguiente mano o al siguiente asiento.
        /// </summary>
        public bool IsFinished => HasStood || IsSurrendered || Hand.IsBust;

        public bool IsActive => !IsFinished;

        public void Deal(Card card)
        {
            if (IsFinished)
                throw new InvalidOperationException("No se puede repartir a una mano ya cerrada.");

            Hand.Add(card);

            // Los ases partidos reciben una sola carta y se plantan solos.
            // Igual que en la mesa: el croupier no vuelve a pasar por ahí.
            if (Hand.IsSplitAces && Hand.Count == 2) HasStood = true;

            // Doblar da derecho a exactamente una carta.
            if (IsDoubled && Hand.Count >= 2) HasStood = true;

            // Con 21 no hay nada que decidir; se planta para no hacer perder
            // tiempo a los demás jugadores de la mesa.
            if (Hand.Value.Total >= 21) HasStood = true;
        }

        public void Stand() => HasStood = true;

        /// <summary>
        /// Dobla la apuesta. La carta se reparte después con <see cref="Deal"/>,
        /// que cerrará la mano automáticamente.
        /// </summary>
        public void Double()
        {
            if (IsDoubled) throw new InvalidOperationException("Esta mano ya está doblada.");
            if (Hand.Count != 2) throw new InvalidOperationException("Solo se doblan manos de 2 cartas.");

            Bet *= 2m;
            IsDoubled = true;
        }

        public void Surrender()
        {
            if (Hand.Count != 2) throw new InvalidOperationException("Solo se rinde una mano de 2 cartas.");
            IsSurrendered = true;
        }

        public override string ToString()
        {
            string state = IsSurrendered ? " [rendida]"
                : Hand.IsBust ? " [pasada]"
                : HasStood ? " [plantada]"
                : string.Empty;
            return Hand + " · " + Bet.ToString("0.##") + (IsDoubled ? " (doblada)" : string.Empty) + state;
        }
    }
}
