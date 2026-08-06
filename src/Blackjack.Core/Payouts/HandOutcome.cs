namespace Blackjack.Core.Payouts
{
    /// <summary>
    /// Cómo terminó una mano frente al croupier. El cliente lo traduce al
    /// cartel que sale sobre la mano ("YOU WIN: $200", "Bust", "Push").
    /// </summary>
    public enum HandOutcome : byte
    {
        /// <summary>Blackjack natural del jugador. Paga 3:2.</summary>
        PlayerBlackjack = 0,

        /// <summary>Gana por puntos o porque el croupier se pasó. Paga 1:1.</summary>
        Win = 1,

        /// <summary>Empate. Se devuelve la apuesta.</summary>
        Push = 2,

        /// <summary>Pierde por puntos o por blackjack del croupier.</summary>
        Lose = 3,

        /// <summary>Se pasó de 21. Pierde de inmediato, incluso si el croupier también se pasa.</summary>
        Bust = 4,

        /// <summary>Se rindió. Recupera la mitad de la apuesta.</summary>
        Surrender = 5
    }
}
