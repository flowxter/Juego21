namespace Blackjack.Core.Rules
{
    /// <summary>
    /// Acciones que un jugador puede pedir sobre una mano. El servidor valida
    /// siempre contra <see cref="ActionValidator"/>: una acción que llega en
    /// el momento equivocado se descarta, no se corrige.
    /// </summary>
    public enum PlayerAction : byte
    {
        Hit = 0,
        Stand = 1,
        Double = 2,
        Split = 3,
        Surrender = 4
    }

    /// <summary>
    /// Modalidades de doblado. Cuanto más restrictiva, mayor ventaja de la casa.
    /// </summary>
    public enum DoubleRule : byte
    {
        /// <summary>Doblar con cualquier par de cartas (Vegas Strip).</summary>
        AnyTwoCards = 0,

        /// <summary>Solo con total duro 9, 10 u 11 (habitual en Europa).</summary>
        NineToEleven = 1,

        /// <summary>Solo con total duro 10 u 11 (la más restrictiva).</summary>
        TenToEleven = 2
    }

    /// <summary>
    /// Momento en que el dealer comprueba si tiene blackjack.
    /// </summary>
    public enum HoleCardRule : byte
    {
        /// <summary>
        /// Americana: el dealer recibe carta tapada y la mira de inmediato si
        /// enseña As o figura. Si tiene blackjack la ronda acaba ahí y los
        /// jugadores solo pierden la apuesta inicial.
        /// </summary>
        AmericanPeek = 0,

        /// <summary>
        /// Europea: no hay carta tapada hasta que todos los jugadores actúan.
        /// Más dura, porque las fichas de doblados y splits también se pierden
        /// si el dealer acaba con blackjack.
        /// </summary>
        EuropeanNoHoleCard = 1
    }
}
