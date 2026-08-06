using System;
using Blackjack.Core.Cards;

namespace Blackjack.Core.SideBets
{
    public enum PerfectPairsCategory : byte
    {
        None = 0,

        /// <summary>Misma figura, distinto color. Ej. 7♥ 7♣. Paga 6:1.</summary>
        MixedPair = 1,

        /// <summary>Misma figura, mismo color, distinto palo. Ej. 7♥ 7♦. Paga 12:1.</summary>
        ColoredPair = 2,

        /// <summary>Misma figura y mismo palo. Ej. 7♥ 7♥. Paga 25:1.</summary>
        PerfectPair = 3
    }

    /// <summary>
    /// Perfect Pairs: apuesta sobre las dos primeras cartas del jugador.
    /// Es la side bet de la mesa de referencia de bet365.
    ///
    /// Ojo: aquí "par" significa MISMA FIGURA, no mismo valor. K-Q no es par
    /// para esta apuesta aunque ambas valgan 10 y sí sean partibles en la
    /// mano principal. Confundir ambos criterios es el error clásico.
    /// </summary>
    public sealed class PerfectPairs
    {
        public PerfectPairs(decimal mixedPayout = 6m, decimal coloredPayout = 12m, decimal perfectPayout = 25m)
        {
            MixedPayout = mixedPayout;
            ColoredPayout = coloredPayout;
            PerfectPayout = perfectPayout;
        }

        public decimal MixedPayout { get; }

        public decimal ColoredPayout { get; }

        public decimal PerfectPayout { get; }

        /// <summary>Tabla de pagos estándar de casino: 6:1 / 12:1 / 25:1.</summary>
        public static PerfectPairs Standard => new PerfectPairs();

        public static PerfectPairsCategory Categorize(Card first, Card second)
        {
            if (first.Rank != second.Rank) return PerfectPairsCategory.None;

            if (first.Suit == second.Suit) return PerfectPairsCategory.PerfectPair;

            return first.Color == second.Color
                ? PerfectPairsCategory.ColoredPair
                : PerfectPairsCategory.MixedPair;
        }

        public SideBetResolution Resolve(Card first, Card second, decimal bet)
        {
            if (bet < 0m) throw new ArgumentOutOfRangeException(nameof(bet));

            PerfectPairsCategory category = Categorize(first, second);

            switch (category)
            {
                case PerfectPairsCategory.PerfectPair:
                    return new SideBetResolution("Par perfecto", bet, PerfectPayout);
                case PerfectPairsCategory.ColoredPair:
                    return new SideBetResolution("Par del mismo color", bet, ColoredPayout);
                case PerfectPairsCategory.MixedPair:
                    return new SideBetResolution("Par mixto", bet, MixedPayout);
                default:
                    return new SideBetResolution(string.Empty, bet, 0m);
            }
        }
    }
}
