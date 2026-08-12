using Blackjack.Core.Rules;

namespace Blackjack.Protocol.Dtos
{
    /// <summary>
    /// Apuestas que un jugador pone en la ventana de apuestas.
    ///
    /// El servidor valida importes, límites de mesa y saldo antes de aceptar
    /// nada: estos números vienen del cliente y por tanto no son de fiar.
    /// </summary>
    public sealed class PlaceBetRequest
    {
        public decimal MainBet { get; set; }

        public decimal PerfectPairsBet { get; set; }

        public decimal TwentyOnePlus3Bet { get; set; }
    }

    /// <summary>Respuesta al seguro. Aceptar con importe, o rechazar.</summary>
    public sealed class InsuranceRequest
    {
        public bool Take { get; set; }

        /// <summary>Ignorado si <see cref="Take"/> es false. Tope: mitad de la apuesta.</summary>
        public decimal Amount { get; set; }
    }

    public sealed class ActionRequest
    {
        public PlayerAction Action { get; set; }
    }

    /// <summary>
    /// Comando rechazado. Se manda solo a quien lo envió.
    ///
    /// Rechazar y explicar, nunca corregir en silencio: si el cliente pidió
    /// algo imposible es que va desincronizado, y lo que necesita es
    /// enterarse y pedir un snapshot nuevo.
    /// </summary>
    public sealed class CommandRejected
    {
        public CommandRejected()
        {
        }

        public CommandRejected(string reason)
        {
            Reason = reason;
        }

        public string Reason { get; set; } = string.Empty;
    }
}
