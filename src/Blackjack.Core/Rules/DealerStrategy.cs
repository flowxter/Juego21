using System;
using Blackjack.Core.Hands;

namespace Blackjack.Core.Rules
{
    /// <summary>
    /// El croupier no decide: sigue una regla fija y pública, impresa en el
    /// fieltro. Ahí está el atractivo del blackjack, y por eso esta clase no
    /// tiene estado ni aleatoriedad.
    /// </summary>
    public static class DealerStrategy
    {
        /// <summary>
        /// "El croupier debe pedir con 16 y plantarse con 17", literalmente lo
        /// que anuncian las mesas de referencia.
        ///
        /// El único matiz es el 17 blando (A-6): con S17 se planta, con H17
        /// pide. Es la diferencia entre las dos variantes más extendidas.
        /// </summary>
        public static bool ShouldHit(HandValue value, TableRules rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));

            if (value.Total < 17) return true;
            if (value.Total == 17 && value.IsSoft && rules.DealerHitsSoft17) return true;

            return false;
        }

        public static bool ShouldHit(Hand hand, TableRules rules)
        {
            if (hand == null) throw new ArgumentNullException(nameof(hand));
            return ShouldHit(hand.Value, rules);
        }
    }
}
