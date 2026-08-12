using System.Collections.Generic;

namespace Blackjack.Protocol.Dtos
{
    /// <summary>
    /// Una mano tal y como la ve el cliente.
    ///
    /// Las cartas viajan como id 0..51 (ver Blackjack.Core.Cards.Card.Id); el
    /// cliente las traduce a sprite. El total viene ya calculado por el
    /// servidor para que el badge de la mesa nunca discrepe de la autoridad.
    /// </summary>
    public sealed class HandDto
    {
        public List<byte> Cards { get; set; } = new List<byte>();

        public int Total { get; set; }

        public bool IsSoft { get; set; }

        public bool IsBlackjack { get; set; }

        public bool IsBust { get; set; }

        public decimal Bet { get; set; }

        public bool IsDoubled { get; set; }

        public bool IsSurrendered { get; set; }

        public bool IsFinished { get; set; }

        /// <summary>True si nació de un split: el cliente lo marca en la mesa.</summary>
        public bool IsFromSplit { get; set; }
    }
}
