namespace Blackjack.Protocol
{
    /// <summary>
    /// Nombres de los métodos de SignalR, compartidos entre servidor y cliente.
    ///
    /// Existe para que un cambio de nombre rompa la compilación en vez de
    /// producir un cliente que llama al vacío en tiempo de ejecución. Los
    /// strings sueltos son la forma más fácil de desincronizar un protocolo.
    /// </summary>
    public static class HubMethods
    {
        /// <summary>Métodos que el cliente invoca en el servidor.</summary>
        public static class Client
        {
            public const string JoinTable = nameof(JoinTable);
            public const string LeaveTable = nameof(LeaveTable);
            public const string Sit = nameof(Sit);
            public const string StandUp = nameof(StandUp);
            public const string PlaceBet = nameof(PlaceBet);

            /// <summary>
            /// "Ya he apostado, por mí podemos empezar". Si lo dicen todos los
            /// que juegan la ronda, la mesa reparte sin esperar al reloj.
            /// </summary>
            public const string Ready = nameof(Ready);
            public const string RespondInsurance = nameof(RespondInsurance);
            public const string Act = nameof(Act);
        }

        /// <summary>Mensajes que el servidor envía al cliente.</summary>
        public static class Server
        {
            /// <summary>Estado completo de la mesa. Se manda al entrar y al reconectar.</summary>
            public const string Snapshot = nameof(Snapshot);

            /// <summary>Lote de hechos de la ronda, en orden, para animar.</summary>
            public const string RoundEvents = nameof(RoundEvents);

            /// <summary>Cambio de fase con su fecha límite.</summary>
            public const string PhaseChanged = nameof(PhaseChanged);

            /// <summary>Saldo del jugador tras un movimiento del ledger.</summary>
            public const string BalanceChanged = nameof(BalanceChanged);

            /// <summary>Comando rechazado, con el motivo.</summary>
            public const string CommandRejected = nameof(CommandRejected);

            /// <summary>
            /// Índices de los asientos que ocupa quien lo recibe.
            ///
            /// Va aparte del snapshot porque el snapshot se difunde igual a
            /// toda la mesa: identificar los asientos propios por el nombre
            /// visible fallaría en cuanto dos jugadores coincidieran de nombre.
            /// </summary>
            public const string YourSeats = nameof(YourSeats);
        }
    }
}
