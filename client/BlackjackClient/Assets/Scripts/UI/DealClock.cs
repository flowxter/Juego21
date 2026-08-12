namespace Blackjack.Client.UI
{
    /// <summary>
    /// Reparte los retardos de las cartas que llegan en un mismo lote.
    ///
    /// El servidor manda las cuatro cartas del reparto inicial de golpe, y
    /// animarlas a la vez se ve como si alguien volcara la baraja sobre la
    /// mesa. Un croupier reparte de una en una, y ese ritmo es justo lo que
    /// hace que la mesa parezca viva.
    /// </summary>
    public sealed class DealClock
    {
        /// <summary>Separación entre cartas consecutivas, en segundos.</summary>
        private const float Step = 0.16f;

        private int _index;

        /// <summary>Retardo de la siguiente carta del lote.</summary>
        public float Next()
        {
            float delay = _index * Step;
            _index++;
            return delay;
        }

        /// <summary>
        /// Empieza un lote nuevo. Se llama al recibir cada snapshot: las cartas
        /// que lleguen a partir de ahí forman una tanda propia.
        /// </summary>
        public void Reset() => _index = 0;
    }
}
