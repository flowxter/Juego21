using System;
using System.Collections.Generic;
using Blackjack.Core.Rules;

namespace Blackjack.Protocol.Dtos
{
    /// <summary>
    /// Estado completo de la mesa. Se envía al entrar y al reconectar.
    ///
    /// Es la red de seguridad del protocolo: si un cliente pierde eventos por
    /// un corte, no hay que reconstruir nada incremental — se le manda el
    /// estado entero y se resincroniza solo.
    /// </summary>
    public sealed class TableSnapshot
    {
        public string TableId { get; set; } = string.Empty;

        public TablePhase Phase { get; set; }

        /// <summary>
        /// Momento en que expira la fase actual, en UTC. Null si la fase no
        /// tiene reloj. El cliente pinta la cuenta atrás a partir de esto en
        /// vez de llevar su propio temporizador, que se desincronizaría.
        /// </summary>
        public DateTime? DeadlineUtc { get; set; }

        public TableRulesDto Rules { get; set; } = new TableRulesDto();

        public List<SeatDto> Seats { get; set; } = new List<SeatDto>();

        /// <summary>
        /// Cartas visibles del croupier. La tapada NO aparece hasta que se
        /// destapa: es la misma garantía que da el registro de eventos.
        /// </summary>
        public List<byte> DealerCards { get; set; } = new List<byte>();

        /// <summary>True mientras el croupier tenga una carta boca abajo.</summary>
        public bool DealerHasHoleCard { get; set; }

        /// <summary>
        /// Total del croupier contando solo lo visible, como el badge de las
        /// mesas de referencia.
        /// </summary>
        public int DealerVisibleTotal { get; set; }

        public bool DealerVisibleSoft { get; set; }

        /// <summary>Asiento en turno, o -1 si no hay turno activo.</summary>
        public int CurrentSeat { get; set; } = -1;

        public int CurrentHand { get; set; } = -1;

        /// <summary>
        /// Acciones ofrecidas al asiento en turno. El cliente las usa para
        /// habilitar botones; el servidor las revalida igualmente.
        /// </summary>
        public List<PlayerAction> LegalActions { get; set; } = new List<PlayerAction>();

        /// <summary>Cartas repartidas desde el último barajado, para el indicador del zapato.</summary>
        public int ShoeCardsDealt { get; set; }

        public int ShoeTotalCards { get; set; }
    }
}
