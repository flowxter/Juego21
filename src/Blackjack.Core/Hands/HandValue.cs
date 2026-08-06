using System;

namespace Blackjack.Core.Hands
{
    /// <summary>
    /// Resultado de evaluar una mano. Inmutable: se recalcula al añadir carta.
    /// </summary>
    public readonly struct HandValue : IEquatable<HandValue>
    {
        public HandValue(int total, bool isSoft, int hardTotal)
        {
            Total = total;
            IsSoft = isSoft;
            HardTotal = hardTotal;
        }

        /// <summary>
        /// Mejor total posible sin pasarse. Si la mano se pasó, es el total
        /// contando todos los ases como 1 (y por tanto &gt; 21).
        /// </summary>
        public int Total { get; }

        /// <summary>
        /// True si hay un As contando como 11. Una mano blanda no se puede
        /// pasar al pedir carta, y eso cambia por completo la estrategia:
        /// A-6 (17 blando) se pide, 10-7 (17 duro) se planta.
        /// </summary>
        public bool IsSoft { get; }

        /// <summary>Total contando todos los ases como 1.</summary>
        public int HardTotal { get; }

        public bool IsBust => Total > 21;

        public bool Is21 => Total == 21;

        public bool Equals(HandValue other)
            => Total == other.Total && IsSoft == other.IsSoft && HardTotal == other.HardTotal;

        public override bool Equals(object? obj) => obj is HandValue other && Equals(other);

        public override int GetHashCode() => (Total * 397) ^ (IsSoft ? 1 : 0);

        public static bool operator ==(HandValue left, HandValue right) => left.Equals(right);

        public static bool operator !=(HandValue left, HandValue right) => !left.Equals(right);

        public override string ToString()
        {
            if (IsBust) return Total + " (pasado)";
            return IsSoft ? "blando " + Total : Total.ToString();
        }
    }
}
