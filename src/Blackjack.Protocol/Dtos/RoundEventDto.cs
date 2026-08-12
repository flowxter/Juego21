using System.Collections.Generic;
using Blackjack.Core.Payouts;
using Blackjack.Core.Rounds;
using Blackjack.Core.Rules;

namespace Blackjack.Protocol.Dtos
{
    /// <summary>
    /// Un hecho de la ronda listo para enviar. Es el reflejo serializable de
    /// <see cref="RoundEvent"/>.
    ///
    /// El cliente reproduce estos eventos en orden como animación: carta que
    /// vuela desde el zapato, ficha que se dobla, cartel de resultado. Por eso
    /// se mandan en lote y con el orden intacto.
    /// </summary>
    public sealed class RoundEventDto
    {
        public RoundEventType Type { get; set; }

        /// <summary>-1 para el croupier.</summary>
        public int SeatIndex { get; set; }

        public int HandIndex { get; set; }

        /// <summary>Id 0..51, o null si la carta va tapada.</summary>
        public byte? CardId { get; set; }

        public bool FaceDown { get; set; }

        public decimal Amount { get; set; }

        public string? Label { get; set; }

        public HandOutcome? Outcome { get; set; }

        public List<PlayerAction>? LegalActions { get; set; }

        /// <summary>
        /// Convierte un evento del motor a su forma serializable. La carta
        /// tapada ya viene sin valor desde el motor, así que aquí no hay nada
        /// que filtrar: la garantía se sostiene sola.
        /// </summary>
        public static RoundEventDto From(RoundEvent source)
        {
            var dto = new RoundEventDto
            {
                Type = source.Type,
                SeatIndex = source.SeatIndex,
                HandIndex = source.HandIndex,
                CardId = source.Card?.Id,
                FaceDown = source.FaceDown,
                Amount = source.Amount,
                Label = source.Label,
                Outcome = source.Outcome
            };

            if (source.LegalActions != null)
                dto.LegalActions = new List<PlayerAction>(source.LegalActions);

            return dto;
        }
    }
}
