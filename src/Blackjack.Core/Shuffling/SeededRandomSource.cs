using System;

namespace Blackjack.Core.Shuffling
{
    /// <summary>
    /// PRNG determinista (PCG32) para tests y para reproducir una partida a
    /// partir de su semilla. Es reproducible entre plataformas y versiones,
    /// cosa que System.Random no garantiza: su algoritmo cambió entre .NET
    /// Framework y .NET Core, lo que rompería tests con valores esperados.
    ///
    /// No apto para producción: la secuencia es predecible por diseño.
    /// </summary>
    public sealed class SeededRandomSource : IRandomSource
    {
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong _state;
        private readonly ulong _increment;

        public SeededRandomSource(ulong seed, ulong sequence = 1UL)
        {
            _increment = (sequence << 1) | 1UL; // el incremento debe ser impar
            _state = 0UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        private uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * Multiplier + _increment);

            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "El límite debe ser positivo.");

            if (maxExclusive == 1) return 0;

            // Mismo muestreo por rechazo que el RNG cripto, para que ambas
            // fuentes tengan idéntica distribución y los tests sean válidos.
            uint limit = uint.MaxValue - (uint.MaxValue % (uint)maxExclusive) - 1;

            uint value;
            do
            {
                value = NextUInt();
            }
            while (value > limit);

            return (int)(value % (uint)maxExclusive);
        }
    }
}
