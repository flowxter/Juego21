using System;
using Blackjack.Core.Hands;
using Blackjack.Core.Rules;

namespace Blackjack.Core.Payouts
{
    /// <summary>
    /// Convierte manos terminadas en dinero. Función pura: mismas entradas,
    /// mismo resultado, sin estado. Es lo que permite testear los pagos sin
    /// levantar servidor ni base de datos.
    /// </summary>
    public static class PayoutCalculator
    {
        /// <summary>
        /// Liquida una mano del jugador contra la del croupier.
        /// </summary>
        public static HandResolution Resolve(PlayerHand hand, Hand dealerHand, TableRules rules)
        {
            if (hand == null) throw new ArgumentNullException(nameof(hand));
            if (dealerHand == null) throw new ArgumentNullException(nameof(dealerHand));
            if (rules == null) throw new ArgumentNullException(nameof(rules));

            decimal bet = hand.Bet;

            // 1. Rendición: se resolvió antes de mirar nada más.
            if (hand.IsSurrendered)
                return new HandResolution(HandOutcome.Surrender, bet, bet * 0.5m);

            // 2. Pasarse pierde SIEMPRE, aunque el croupier se pase después.
            //    Esta única regla es de donde sale casi toda la ventaja de la
            //    casa: ambos se pasan y aun así gana la banca.
            if (hand.Hand.IsBust)
                return new HandResolution(HandOutcome.Bust, bet, 0m);

            bool playerBlackjack = hand.Hand.IsBlackjack;
            bool dealerBlackjack = dealerHand.IsBlackjack;

            // 3. Blackjacks naturales.
            if (playerBlackjack && dealerBlackjack)
                return new HandResolution(HandOutcome.Push, bet, bet);

            if (playerBlackjack)
                return new HandResolution(HandOutcome.PlayerBlackjack, bet, bet + (bet * rules.BlackjackPayout));

            if (dealerBlackjack)
                return new HandResolution(HandOutcome.Lose, bet, 0m);

            // 4. Croupier pasado: gana todo el que siga en pie.
            if (dealerHand.IsBust)
                return new HandResolution(HandOutcome.Win, bet, bet * 2m);

            // 5. Comparación de puntos.
            int player = hand.Hand.Value.Total;
            int dealer = dealerHand.Value.Total;

            if (player > dealer) return new HandResolution(HandOutcome.Win, bet, bet * 2m);
            if (player == dealer) return new HandResolution(HandOutcome.Push, bet, bet);

            return new HandResolution(HandOutcome.Lose, bet, 0m);
        }

        /// <summary>
        /// Liquida el seguro. Solo gana si el croupier tiene blackjack natural.
        ///
        /// El seguro es matemáticamente malo para el jugador (con 6 barajas
        /// pierde ~7.4% de lo apostado), pero es parte de la experiencia real
        /// y las mesas de referencia lo anuncian. Lo implementamos con su pago
        /// correcto de 2:1, no lo maquillamos.
        /// </summary>
        public static HandResolution ResolveInsurance(decimal insuranceBet, Hand dealerHand, TableRules rules)
        {
            if (dealerHand == null) throw new ArgumentNullException(nameof(dealerHand));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (insuranceBet < 0m) throw new ArgumentOutOfRangeException(nameof(insuranceBet));

            if (insuranceBet == 0m)
                return new HandResolution(HandOutcome.Lose, 0m, 0m);

            if (dealerHand.IsBlackjack)
            {
                decimal returned = insuranceBet + (insuranceBet * rules.InsurancePayout);
                return new HandResolution(HandOutcome.Win, insuranceBet, returned);
            }

            return new HandResolution(HandOutcome.Lose, insuranceBet, 0m);
        }

        /// <summary>
        /// Apuesta máxima de seguro: la mitad de la apuesta principal.
        /// </summary>
        public static decimal MaxInsuranceBet(decimal mainBet) => mainBet * 0.5m;
    }
}
