using System;
using System.Collections.Generic;
using Blackjack.Core.Hands;

namespace Blackjack.Core.Rules
{
    /// <summary>
    /// Decide qué acciones son legales sobre una mano concreta.
    ///
    /// Esta clase es la razón de ser del motor compartido: el servidor la usa
    /// para RECHAZAR comandos ilegales y el cliente Unity la usa para decidir
    /// qué botones habilitar. Al ser el mismo código, el jugador nunca ve un
    /// botón que el servidor le vaya a denegar.
    /// </summary>
    public static class ActionValidator
    {
        /// <summary>
        /// Acciones legales sobre <paramref name="hand"/>.
        /// </summary>
        /// <param name="hand">Mano en turno.</param>
        /// <param name="rules">Reglas de la mesa.</param>
        /// <param name="availableBalance">
        /// Saldo libre del jugador, ya descontadas las apuestas comprometidas.
        /// Doblar y partir requieren cubrir una apuesta adicional.
        /// </param>
        /// <param name="handCountInSeat">
        /// Manos que el jugador tiene ahora mismo en el asiento. Se usa para
        /// el tope de splits.
        /// </param>
        public static IReadOnlyList<PlayerAction> LegalActions(
            PlayerHand hand,
            TableRules rules,
            decimal availableBalance,
            int handCountInSeat)
        {
            if (hand == null) throw new ArgumentNullException(nameof(hand));
            if (rules == null) throw new ArgumentNullException(nameof(rules));

            var actions = new List<PlayerAction>(5);

            if (hand.IsFinished) return actions;

            if (CanHit(hand, rules)) actions.Add(PlayerAction.Hit);

            actions.Add(PlayerAction.Stand);

            if (CanDouble(hand, rules, availableBalance)) actions.Add(PlayerAction.Double);
            if (CanSplit(hand, rules, availableBalance, handCountInSeat)) actions.Add(PlayerAction.Split);
            if (CanSurrender(hand, rules)) actions.Add(PlayerAction.Surrender);

            return actions;
        }

        public static bool IsLegal(
            PlayerAction action,
            PlayerHand hand,
            TableRules rules,
            decimal availableBalance,
            int handCountInSeat)
        {
            switch (action)
            {
                case PlayerAction.Hit: return !hand.IsFinished && CanHit(hand, rules);
                case PlayerAction.Stand: return !hand.IsFinished;
                case PlayerAction.Double: return !hand.IsFinished && CanDouble(hand, rules, availableBalance);
                case PlayerAction.Split: return !hand.IsFinished && CanSplit(hand, rules, availableBalance, handCountInSeat);
                case PlayerAction.Surrender: return !hand.IsFinished && CanSurrender(hand, rules);
                default: return false;
            }
        }

        private static bool CanHit(PlayerHand hand, TableRules rules)
        {
            // Los ases partidos reciben una sola carta salvo que la mesa
            // permita lo contrario (rarísimo).
            if (hand.Hand.IsSplitAces && !rules.HitSplitAces) return false;

            return hand.Hand.Value.Total < 21;
        }

        private static bool CanDouble(PlayerHand hand, TableRules rules, decimal availableBalance)
        {
            if (hand.Hand.Count != 2) return false;
            if (hand.IsDoubled) return false;
            if (hand.Hand.IsSplitAces && !rules.HitSplitAces) return false;
            if (hand.Hand.IsFromSplit && !rules.DoubleAfterSplit) return false;

            // Hace falta poder cubrir una apuesta igual a la actual.
            if (availableBalance < hand.Bet) return false;

            // El total duro es el que manda: con A-9 (soft 20) el total duro
            // es 10, así que una mesa 9-11 permitiría doblarlo.
            int total = hand.Hand.Value.Total;

            switch (rules.DoubleRule)
            {
                case DoubleRule.AnyTwoCards:
                    return true;
                case DoubleRule.NineToEleven:
                    return total >= 9 && total <= 11;
                case DoubleRule.TenToEleven:
                    return total >= 10 && total <= 11;
                default:
                    return false;
            }
        }

        private static bool CanSplit(PlayerHand hand, TableRules rules, decimal availableBalance, int handCountInSeat)
        {
            if (hand.Hand.Count != 2) return false;

            bool isPair = rules.SplitByExactRank ? hand.Hand.IsExactRankPair : hand.Hand.IsPair;
            if (!isPair) return false;

            // MaxSplits = 3 significa 4 manos como mucho.
            if (hand.SplitDepth >= rules.MaxSplits) return false;
            if (handCountInSeat > rules.MaxSplits) return false;

            // Volver a partir ases suele estar prohibido.
            if (hand.Hand.Cards[0].IsAce && hand.Hand.IsFromSplit && !rules.ResplitAces) return false;

            return availableBalance >= hand.Bet;
        }

        private static bool CanSurrender(PlayerHand hand, TableRules rules)
        {
            if (!rules.LateSurrender) return false;
            if (hand.Hand.Count != 2) return false;

            // No se rinde una mano que ya se partió ni una ya doblada.
            if (hand.Hand.IsFromSplit) return false;
            if (hand.IsDoubled) return false;

            return true;
        }
    }
}
