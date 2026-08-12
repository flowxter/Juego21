using System;
using System.Threading;
using System.Threading.Tasks;
using Blackjack.Core.Hands;
using Blackjack.Core.Payouts;
using Blackjack.Data.Entities;
using Microsoft.EntityFrameworkCore;
using CoreHand = Blackjack.Core.Hands.Hand;
using CoreSeat = Blackjack.Core.Rounds.Seat;

namespace Blackjack.Data.History
{
    public sealed class EfRoundArchive : IRoundArchive
    {
        private readonly IDbContextFactory<BlackjackDbContext> _factory;

        public EfRoundArchive(IDbContextFactory<BlackjackDbContext> factory)
        {
            _factory = factory;
        }

        public async Task ArchiveAsync(
            string roundId,
            string tableId,
            Guid userId,
            CoreSeat seat,
            CoreHand dealerHand,
            CancellationToken ct = default)
        {
            await using BlackjackDbContext db = await _factory.CreateDbContextAsync(ct);

            decimal wagered = TotalWagered(seat);

            var record = new RoundRecord
            {
                RoundId = roundId,
                TableId = tableId,
                UserId = userId,
                SeatIndex = seat.Index,
                PlayedUtc = DateTime.UtcNow,
                MainBet = seat.InitialBet,
                PerfectPairsBet = seat.PerfectPairsBet,
                TwentyOnePlus3Bet = seat.TwentyOnePlus3Bet,
                InsuranceBet = seat.InsuranceBet,
                TotalReturned = seat.TotalReturned,
                NetProfit = seat.TotalReturned - wagered,
                DealerCards = CardCodec.Encode(dealerHand.Cards),
                DealerTotal = dealerHand.Value.Total,
                DealerBlackjack = dealerHand.IsBlackjack
            };

            for (int i = 0; i < seat.Hands.Count; i++)
            {
                PlayerHand hand = seat.Hands[i];

                // Results puede ir por detrás si la ronda se cortó antes de
                // liquidar; en ese caso se guarda la mano sin resultado.
                HandOutcome outcome = i < seat.Results.Count ? seat.Results[i].Outcome : HandOutcome.Push;
                decimal returned = i < seat.Results.Count ? seat.Results[i].Returned : 0m;

                record.Hands.Add(new HandRecord
                {
                    HandIndex = i,
                    Cards = CardCodec.Encode(hand.Hand.Cards),
                    Total = hand.Hand.Value.Total,
                    IsSoft = hand.Hand.Value.IsSoft,
                    Outcome = outcome,
                    Bet = hand.Bet,
                    Returned = returned,
                    IsBlackjack = hand.Hand.IsBlackjack,
                    IsFromSplit = hand.Hand.IsFromSplit,
                    IsDoubled = hand.IsDoubled,
                    IsSurrendered = hand.IsSurrendered
                });
            }

            db.RoundRecords.Add(record);

            await UpdateStatsAsync(db, userId, seat, wagered, ct);

            await db.SaveChangesAsync(ct);
        }

        private static async Task UpdateStatsAsync(
            BlackjackDbContext db, Guid userId, CoreSeat seat, decimal wagered, CancellationToken ct)
        {
            PlayerStats? stats = await db.PlayerStats.FirstOrDefaultAsync(s => s.UserId == userId, ct);

            if (stats == null)
            {
                stats = new PlayerStats { UserId = userId };
                db.PlayerStats.Add(stats);
            }

            stats.RoundsPlayed++;
            stats.HandsPlayed += seat.Hands.Count;
            stats.TotalWagered += wagered;
            stats.TotalReturned += seat.TotalReturned;
            stats.UpdatedUtc = DateTime.UtcNow;

            decimal netProfit = seat.TotalReturned - wagered;
            if (netProfit > stats.BiggestWin) stats.BiggestWin = netProfit;

            for (int i = 0; i < seat.Hands.Count; i++)
            {
                PlayerHand hand = seat.Hands[i];

                if (hand.Hand.IsBlackjack) stats.Blackjacks++;
                if (hand.Hand.IsBust) stats.Busts++;

                if (i >= seat.Results.Count) continue;

                switch (seat.Results[i].Outcome)
                {
                    case HandOutcome.PlayerBlackjack:
                    case HandOutcome.Win:
                        stats.HandsWon++;
                        break;
                    case HandOutcome.Push:
                        stats.HandsPushed++;
                        break;
                    case HandOutcome.Surrender:
                        stats.HandsSurrendered++;
                        break;
                    default:
                        stats.HandsLost++;
                        break;
                }
            }
        }

        /// <summary>
        /// Todo lo que el jugador puso sobre la mesa. Las apuestas de las
        /// manos ya incluyen dobles y splits, así que sumarlas basta; añadir
        /// aparte la apuesta inicial la contaría dos veces.
        /// </summary>
        private static decimal TotalWagered(CoreSeat seat)
        {
            decimal total = seat.InsuranceBet + seat.PerfectPairsBet + seat.TwentyOnePlus3Bet;

            for (int i = 0; i < seat.Hands.Count; i++)
            {
                total += seat.Hands[i].Bet;
            }

            return total;
        }
    }
}
