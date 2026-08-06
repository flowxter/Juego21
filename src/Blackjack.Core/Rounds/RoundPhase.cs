namespace Blackjack.Core.Rounds
{
    /// <summary>
    /// Fases de una ronda. La transición la controla <see cref="Round"/> y
    /// nunca el cliente: un comando que llega en la fase equivocada se
    /// rechaza, no se encola ni se corrige.
    /// </summary>
    public enum RoundPhase : byte
    {
        /// <summary>Aún no se ha repartido.</summary>
        NotStarted = 0,

        /// <summary>
        /// Seguro ofrecido. Solo ocurre con As descubierto del croupier y
        /// bloquea la ronda hasta que todos los asientos responden.
        /// </summary>
        Insurance = 1,

        /// <summary>Turnos de los jugadores, asiento a asiento y mano a mano.</summary>
        PlayerTurns = 2,

        /// <summary>El croupier destapa y juega su mano según S17/H17.</summary>
        DealerPlay = 3,

        /// <summary>Ronda liquidada. Los resultados ya están en cada asiento.</summary>
        Complete = 4
    }
}
