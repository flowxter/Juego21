namespace Blackjack.Protocol
{
    /// <summary>
    /// Fase de la MESA, que es un superconjunto de la fase de la ronda: añade
    /// la espera de jugadores y la ventana de apuestas, que ocurren antes de
    /// que exista ronda alguna.
    /// </summary>
    public enum TablePhase : byte
    {
        /// <summary>Mesa vacía o sin nadie que apueste. No corre el reloj.</summary>
        WaitingForPlayers = 0,

        /// <summary>Ventana de apuestas. Al agotarse, quien no apostó no juega.</summary>
        Betting = 1,

        /// <summary>Reparto inicial en curso; se emite como animación.</summary>
        Dealing = 2,

        /// <summary>Seguro ofrecido. Solo con As descubierto.</summary>
        Insurance = 3,

        /// <summary>Turnos de los jugadores.</summary>
        PlayerTurns = 4,

        /// <summary>El croupier destapa y juega.</summary>
        DealerPlay = 5,

        /// <summary>Pagos en pantalla antes de limpiar la mesa.</summary>
        Payout = 6
    }
}
