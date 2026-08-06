using System;
using System.Security.Cryptography;

namespace Blackjack.Core.Shuffling
{
    /// <summary>
    /// RNG criptográfico con muestreo por rechazo. Nunca usar System.Random
    /// para repartir cartas: su estado se puede reconstruir observando unas
    /// pocas salidas, y con él la baraja entera queda expuesta.
    /// </summary>
    public sealed class CryptoRandomSource : IRandomSource, IDisposable
    {
        private readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
        private readonly byte[] _buffer = new byte[4];
        private bool _disposed;

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "El límite debe ser positivo.");
            if (_disposed)
                throw new ObjectDisposedException(nameof(CryptoRandomSource));

            if (maxExclusive == 1) return 0;

            // Descartamos el resto final del rango de uint para que todos los
            // valores tengan exactamente la misma probabilidad. Sin esto los
            // valores bajos salen algo más a menudo (sesgo de módulo).
            uint limit = uint.MaxValue - (uint.MaxValue % (uint)maxExclusive) - 1;

            uint value;
            do
            {
                _rng.GetBytes(_buffer);
                value = BitConverter.ToUInt32(_buffer, 0);
            }
            while (value > limit);

            return (int)(value % (uint)maxExclusive);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _rng.Dispose();
            _disposed = true;
        }
    }
}
