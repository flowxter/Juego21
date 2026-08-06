namespace Blackjack.Core.Shuffling
{
    /// <summary>
    /// Fuente de aleatoriedad del barajado. Se inyecta para que los tests
    /// puedan fijar una secuencia conocida sin tocar la lógica del shoe.
    /// En producción el servidor usa siempre <see cref="CryptoRandomSource"/>.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>
        /// Entero uniforme en [0, maxExclusive). Las implementaciones deben
        /// estar libres de sesgo de módulo: un barajado con sesgo es un
        /// barajado predecible.
        /// </summary>
        int NextInt(int maxExclusive);
    }
}
