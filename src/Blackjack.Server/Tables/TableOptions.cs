namespace Blackjack.Server.Tables
{
    /// <summary>
    /// Tiempos y tamaño de una mesa.
    ///
    /// Los temporizadores son lo que separa una mesa multijugador de una de un
    /// solo jugador: sin ellos, un ausente congela la partida de los otros
    /// cuatro. Toda fase que espere una decisión humana tiene reloj y una
    /// acción por defecto.
    /// </summary>
    public sealed class TableOptions
    {
        public int SeatCount { get; set; } = 5;

        /// <summary>
        /// Asientos que puede ocupar un mismo jugador a la vez.
        ///
        /// Jugar varias manos es habitual en casinos reales, pero conviene un
        /// tope: sin él, uno solo podría acaparar la mesa entera y dejar sin
        /// sitio a los demás.
        /// </summary>
        public int MaxSeatsPerPlayer { get; set; } = 3;

        /// <summary>Ventana de apuestas. Quien no apueste se queda fuera de la ronda.</summary>
        public int BettingSeconds { get; set; } = 15;

        /// <summary>Decisión de seguro. Al expirar se rechaza por los ausentes.</summary>
        public int InsuranceSeconds { get; set; } = 10;

        /// <summary>Turno de un jugador. Al expirar se planta por él.</summary>
        public int TurnSeconds { get; set; } = 20;

        /// <summary>Pausa para que se vean los pagos antes de limpiar la mesa.</summary>
        public int PayoutSeconds { get; set; } = 5;

        /// <summary>
        /// Margen para volver tras una caída de conexión. Durante este tiempo
        /// el asiento sigue siendo suyo y no se ocupa: en una mesa real nadie
        /// pierde el sitio por levantarse un momento.
        /// </summary>
        public int ReconnectWindowSeconds { get; set; } = 60;
    }
}
