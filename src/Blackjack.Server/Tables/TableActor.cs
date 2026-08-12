using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Blackjack.Core.Cards;
using Blackjack.Core.Hands;
using Blackjack.Core.Rounds;
using Blackjack.Core.Rules;
using Blackjack.Core.Shuffling;
using Blackjack.Data.Entities;
using Blackjack.Data.History;
using Blackjack.Data.Wallet;
using Blackjack.Protocol;
using Blackjack.Protocol.Dtos;
using Blackjack.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Blackjack.Server.Tables
{
    /// <summary>
    /// Una mesa de blackjack multijugador.
    ///
    /// Es un actor: recibe mensajes por un buzón y los procesa de uno en uno
    /// en un bucle propio. Nada externo toca su estado, así que no hay locks
    /// ni carreras entre "el jugador pide carta" y "expiró su turno" — ambas
    /// cosas son mensajes en la misma cola y se atienden en orden.
    ///
    /// Las reglas no están aquí: viven en Blackjack.Core. Esta clase solo
    /// gestiona quién se sienta, el reloj y a quién se le manda cada cosa.
    /// </summary>
    public sealed class TableActor : IAsyncDisposable
    {
        private readonly Channel<TableMessage> _mailbox =
            Channel.CreateUnbounded<TableMessage>(new UnboundedChannelOptions { SingleReader = true });

        private readonly CancellationTokenSource _stopping = new();
        private readonly Task _loop;

        private readonly IHubContext<GameHub> _hub;
        private readonly IWalletService _wallet;
        private readonly IRoundArchive _archive;
        private readonly ILogger<TableActor> _log;
        private readonly TableOptions _options;
        private readonly TableRules _rules;
        private readonly Shoe _shoe;
        private readonly TableSeat[] _seats;

        private Round? _round;
        private string? _roundId;
        private int _emittedEvents;
        private TablePhase _phase = TablePhase.WaitingForPlayers;
        private DateTime? _deadlineUtc;

        public TableActor(
            string tableId,
            TableRules rules,
            TableOptions options,
            IHubContext<GameHub> hub,
            IWalletService wallet,
            IRoundArchive archive,
            ILogger<TableActor> log)
        {
            TableId = tableId;
            _rules = rules;
            _options = options;
            _hub = hub;
            _wallet = wallet;
            _archive = archive;
            _log = log;

            _shoe = new Shoe(rules.DeckCount, rules.Penetration);
            _seats = new TableSeat[options.SeatCount];
            for (int i = 0; i < options.SeatCount; i++) _seats[i] = new TableSeat(i);

            _loop = Task.Run(() => RunAsync(_stopping.Token));
        }

        public string TableId { get; }

        public static string TableGroup(string tableId) => "table:" + tableId;

        public static string PlayerGroup(Guid playerId) => "player:" + playerId.ToString("N");

        /// <summary>Encola un mensaje. No bloquea ni espera a que se procese.</summary>
        public void Post(TableMessage message) => _mailbox.Writer.TryWrite(message);

        // ------------------------------------------------------------------
        // Bucle
        // ------------------------------------------------------------------

        private async Task RunAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    TableMessage? message = await ReceiveAsync(ct).ConfigureAwait(false);
                    if (ct.IsCancellationRequested) break;

                    if (message == null)
                    {
                        await OnDeadlineAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        await HandleAsync(message).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    // Una mesa nunca debe morir por un mensaje malo: se
                    // registra y se sigue atendiendo a los demás jugadores.
                    _log.LogError(ex, "Fallo procesando un mensaje en la mesa {TableId}", TableId);
                }
            }
        }

        /// <summary>
        /// Espera un mensaje, o devuelve null si vence el plazo de la fase.
        /// Un solo await cubre las dos cosas, que es lo que mantiene todo
        /// dentro del mismo hilo lógico.
        /// </summary>
        private async Task<TableMessage?> ReceiveAsync(CancellationToken ct)
        {
            TimeSpan? remaining = TimeUntilDeadline();

            if (remaining == null)
            {
                try { return await _mailbox.Reader.ReadAsync(ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return null; }
            }

            if (remaining.Value <= TimeSpan.Zero) return null;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(remaining.Value);

            try { return await _mailbox.Reader.ReadAsync(timeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return null; }
        }

        private TimeSpan? TimeUntilDeadline()
        {
            if (_deadlineUtc == null) return null;
            TimeSpan remaining = _deadlineUtc.Value - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        private async Task HandleAsync(TableMessage message)
        {
            switch (message)
            {
                case JoinMessage join:
                    await OnJoinAsync(join).ConfigureAwait(false);
                    break;
                case SitMessage sit:
                    await OnSitAsync(sit).ConfigureAwait(false);
                    break;
                case StandUpMessage standUp:
                    await OnStandUpAsync(standUp).ConfigureAwait(false);
                    break;
                case PlaceBetMessage bet:
                    await OnPlaceBetAsync(bet).ConfigureAwait(false);
                    break;
                case ReadyMessage ready:
                    await OnReadyAsync(ready).ConfigureAwait(false);
                    break;
                case InsuranceMessage insurance:
                    await OnInsuranceAsync(insurance).ConfigureAwait(false);
                    break;
                case ActMessage act:
                    await OnActAsync(act).ConfigureAwait(false);
                    break;
                case DisconnectedMessage disconnected:
                    await OnDisconnectedAsync(disconnected).ConfigureAwait(false);
                    break;
                case LeaveMessage leave:
                    await OnStandUpAsync(new StandUpMessage(leave.PlayerId)).ConfigureAwait(false);
                    break;
            }
        }

        private async Task OnDeadlineAsync()
        {
            switch (_phase)
            {
                case TablePhase.Betting:
                    await StartRoundAsync().ConfigureAwait(false);
                    break;

                case TablePhase.Insurance:
                    _round?.CloseInsurance();
                    await FlushRoundAsync().ConfigureAwait(false);
                    break;

                case TablePhase.PlayerTurns:
                    // Se planta por el ausente. La mesa no espera a nadie.
                    _round?.ForceStand();
                    await FlushRoundAsync().ConfigureAwait(false);
                    break;

                case TablePhase.Payout:
                    await CleanupAndReopenAsync().ConfigureAwait(false);
                    break;

                default:
                    _deadlineUtc = null;
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Entrar, sentarse, levantarse
        // ------------------------------------------------------------------

        private async Task OnJoinAsync(JoinMessage message)
        {
            await _wallet.EnsureAccountAsync(message.PlayerId).ConfigureAwait(false);

            // Reconexión: recupera todos los asientos que tuviera, tal cual.
            List<TableSeat> seats = SeatsOf(message.PlayerId);

            foreach (TableSeat seat in seats)
            {
                seat.MarkReconnected();
                _log.LogInformation("{Player} volvió al asiento {Seat} de {Table}",
                    message.PlayerId, seat.Index, TableId);
            }

            await SendSnapshotToPlayerAsync(message.PlayerId).ConfigureAwait(false);
            await SendBalanceAsync(message.PlayerId).ConfigureAwait(false);
            await SendSeatsAsync(message.PlayerId).ConfigureAwait(false);

            if (seats.Count > 0) await BroadcastSnapshotAsync().ConfigureAwait(false);
        }

        private async Task OnSitAsync(SitMessage message)
        {
            if (message.SeatIndex < 0 || message.SeatIndex >= _seats.Length)
            {
                await RejectAsync(message.PlayerId, "Ese asiento no existe en esta mesa.").ConfigureAwait(false);
                return;
            }

            // Un jugador puede ocupar varios asientos y jugar una mano en cada
            // uno, como en una mesa real. El tope evita que uno solo acapare
            // la mesa entera.
            int owned = CountSeatsOf(message.PlayerId);
            if (owned >= _options.MaxSeatsPerPlayer)
            {
                await RejectAsync(message.PlayerId,
                    $"No puedes ocupar más de {_options.MaxSeatsPerPlayer} asientos.").ConfigureAwait(false);
                return;
            }

            TableSeat seat = _seats[message.SeatIndex];
            if (seat.IsOccupied)
            {
                await RejectAsync(message.PlayerId, "Ese asiento está ocupado.").ConfigureAwait(false);
                return;
            }

            await _wallet.EnsureAccountAsync(message.PlayerId).ConfigureAwait(false);
            seat.Occupy(message.PlayerId, message.PlayerName);

            // Sentarse a media ronda es legal, pero se juega desde la siguiente.
            if (_phase == TablePhase.WaitingForPlayers) SetPhase(TablePhase.Betting, _options.BettingSeconds);

            await SendSeatsAsync(message.PlayerId).ConfigureAwait(false);
            await BroadcastSnapshotAsync().ConfigureAwait(false);
        }

        private async Task OnStandUpAsync(StandUpMessage message)
        {
            // Con índice se deja ese asiento; sin él, todos los del jugador.
            List<TableSeat> seats = message.SeatIndex >= 0
                ? new List<TableSeat>(1)
                : SeatsOf(message.PlayerId);

            if (message.SeatIndex >= 0)
            {
                TableSeat? single = FindSeat(message.PlayerId, message.SeatIndex);
                if (single == null) return;
                seats.Add(single);
            }

            if (seats.Count == 0) return;

            foreach (TableSeat seat in seats)
            {
                // Si se levanta con apuestas puestas y aún no se repartió, se
                // le devuelven: no ha visto ninguna carta.
                if (_round == null && seat.TotalStaked > 0m)
                {
                    await _wallet.CreditAsync(message.PlayerId, seat.TotalStaked, LedgerEntryType.Refund, _roundId)
                        .ConfigureAwait(false);
                }

                seat.Vacate();
            }

            await SendBalanceAsync(message.PlayerId).ConfigureAwait(false);
            await SendSeatsAsync(message.PlayerId).ConfigureAwait(false);

            if (!AnyoneSeated()) SetPhase(TablePhase.WaitingForPlayers, null);

            await BroadcastSnapshotAsync().ConfigureAwait(false);
        }

        private async Task OnDisconnectedAsync(DisconnectedMessage message)
        {
            List<TableSeat> seats = SeatsOf(message.PlayerId);
            if (seats.Count == 0) return;

            // No se liberan los asientos: empieza la ventana de gracia. Si el
            // jugador está en turno, el temporizador normal se planta por él.
            DateTime now = DateTime.UtcNow;
            foreach (TableSeat seat in seats) seat.MarkDisconnected(now);

            _log.LogInformation("{Player} perdió conexión en {Table}; {Count} asiento(s) reservado(s)",
                message.PlayerId, TableId, seats.Count);

            await BroadcastSnapshotAsync().ConfigureAwait(false);
        }

        // ------------------------------------------------------------------
        // Apuestas
        // ------------------------------------------------------------------

        private async Task OnPlaceBetAsync(PlaceBetMessage message)
        {
            if (_phase != TablePhase.Betting)
            {
                await RejectAsync(message.PlayerId, "La ventana de apuestas está cerrada.").ConfigureAwait(false);
                return;
            }

            TableSeat? seat = FindSeat(message.PlayerId, message.SeatIndex);
            if (seat == null)
            {
                await RejectAsync(message.PlayerId, "Ese asiento no es tuyo.").ConfigureAwait(false);
                return;
            }

            PlaceBetRequest request = message.Request;

            if (request.MainBet < _rules.MinBet || request.MainBet > _rules.MaxBet)
            {
                await RejectAsync(message.PlayerId,
                    $"La apuesta debe estar entre {_rules.MinBet:0.##} y {_rules.MaxBet:0.##}.").ConfigureAwait(false);
                return;
            }

            if (request.PerfectPairsBet < 0m || request.TwentyOnePlus3Bet < 0m)
            {
                await RejectAsync(message.PlayerId, "Las apuestas laterales no pueden ser negativas.").ConfigureAwait(false);
                return;
            }

            if (!_rules.SideBetsEnabled && (request.PerfectPairsBet > 0m || request.TwentyOnePlus3Bet > 0m))
            {
                await RejectAsync(message.PlayerId, "Esta mesa no admite apuestas laterales.").ConfigureAwait(false);
                return;
            }

            // Cambiar de apuesta devuelve la anterior antes de cobrar la nueva,
            // para que el libro refleje ambos movimientos por separado.
            if (seat.TotalStaked > 0m)
            {
                await _wallet.CreditAsync(message.PlayerId, seat.TotalStaked, LedgerEntryType.Refund, _roundId)
                    .ConfigureAwait(false);
                seat.ClearBets();
            }

            decimal total = request.MainBet + request.PerfectPairsBet + request.TwentyOnePlus3Bet;

            if (!await _wallet.TryDebitAsync(message.PlayerId, total, LedgerEntryType.Bet, _roundId).ConfigureAwait(false))
            {
                await RejectAsync(message.PlayerId, "Saldo insuficiente para esa apuesta.").ConfigureAwait(false);
                await SendBalanceAsync(message.PlayerId).ConfigureAwait(false);
                return;
            }

            seat.SetBets(request.MainBet, request.PerfectPairsBet, request.TwentyOnePlus3Bet);

            await SendBalanceAsync(message.PlayerId).ConfigureAwait(false);
            await BroadcastSnapshotAsync().ConfigureAwait(false);
        }

        private async Task OnReadyAsync(ReadyMessage message)
        {
            if (_phase != TablePhase.Betting) return;

            // "Listo" vale por todos los asientos del jugador: si ocupa tres,
            // no tiene sentido obligarle a confirmarlos uno a uno.
            List<TableSeat> seats = SeatsOf(message.PlayerId);
            if (seats.Count == 0) return;

            bool anyBet = false;
            foreach (TableSeat seat in seats)
            {
                if (!seat.HasBet) continue;
                seat.MarkReady();
                anyBet = true;
            }

            if (!anyBet)
            {
                await RejectAsync(message.PlayerId, "Apuesta antes de marcarte como listo.").ConfigureAwait(false);
                return;
            }

            await BroadcastSnapshotAsync().ConfigureAwait(false);

            // Si nadie más tiene que decidir, se reparte ya: hacer esperar a
            // una mesa entera por un reloj que nadie necesita es tiempo muerto.
            if (AllBettorsReady()) await StartRoundAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// True si todos los asientos que han apostado están listos. Los que no
        /// apostaron no cuentan: esa ronda no la juegan.
        /// </summary>
        private bool AllBettorsReady()
        {
            bool anyBettor = false;

            for (int i = 0; i < _seats.Length; i++)
            {
                TableSeat seat = _seats[i];
                if (!seat.IsOccupied || !seat.HasBet) continue;

                anyBettor = true;
                if (!seat.IsReady) return false;
            }

            return anyBettor;
        }

        // ------------------------------------------------------------------
        // Ronda
        // ------------------------------------------------------------------

        private async Task StartRoundAsync()
        {
            var bets = new List<SeatBet>();

            for (int i = 0; i < _seats.Length; i++)
            {
                TableSeat seat = _seats[i];
                if (!seat.IsOccupied || !seat.HasBet) continue;

                // El saldo ya tiene descontadas las apuestas: esto es lo que
                // le queda libre para doblar o partir.
                decimal available = await _wallet.GetBalanceAsync(seat.PlayerId!.Value).ConfigureAwait(false);

                bets.Add(new SeatBet(
                    seat.Index,
                    seat.MainBet,
                    available,
                    seat.PerfectPairsBet,
                    seat.TwentyOnePlus3Bet));
            }

            if (bets.Count == 0)
            {
                // Nadie apostó. Hay que purgar aquí también: si los ocupantes
                // se desconectaron sin apostar nunca se llega a la fase de
                // pagos, y sin esta llamada sus asientos quedarían reservados
                // para siempre y nadie más podría sentarse.
                PurgeAbandonedSeats();

                SetPhase(AnyoneSeated() ? TablePhase.Betting : TablePhase.WaitingForPlayers,
                         AnyoneSeated() ? _options.BettingSeconds : (int?)null);
                await BroadcastSnapshotAsync().ConfigureAwait(false);
                return;
            }

            // Al cruzar la cut card se baraja ENTRE rondas, nunca a media mano.
            if (_shoe.NeedsShuffle) _shoe.Shuffle();

            _roundId = Guid.NewGuid().ToString("N");
            _emittedEvents = 0;
            _round = new Round(_shoe, _rules, bets);

            SetPhase(TablePhase.Dealing, null);
            _round.Start();

            await FlushRoundAsync().ConfigureAwait(false);
        }

        private async Task OnInsuranceAsync(InsuranceMessage message)
        {
            if (_phase != TablePhase.Insurance || _round == null)
            {
                await RejectAsync(message.PlayerId, "Ahora no se está ofreciendo seguro.").ConfigureAwait(false);
                return;
            }

            // Con varios asientos hay que responder por cada uno; el cliente
            // manda -1 cuando quiere la misma respuesta para todos.
            List<TableSeat> seats = message.SeatIndex >= 0
                ? new List<TableSeat>(1)
                : SeatsOf(message.PlayerId);

            if (message.SeatIndex >= 0)
            {
                TableSeat? single = FindSeat(message.PlayerId, message.SeatIndex);
                if (single == null) return;
                seats.Add(single);
            }

            foreach (TableSeat seat in seats)
            {
                await ApplyInsuranceAsync(message, seat).ConfigureAwait(false);
            }

            await FlushRoundAsync().ConfigureAwait(false);
        }

        private async Task ApplyInsuranceAsync(InsuranceMessage message, TableSeat seat)
        {
            if (_round == null) return;

            bool charged = false;

            try
            {
                if (message.Request.Take)
                {
                    decimal amount = message.Request.Amount;

                    if (!await _wallet.TryDebitAsync(message.PlayerId, amount, LedgerEntryType.Insurance, _roundId)
                            .ConfigureAwait(false))
                    {
                        await RejectAsync(message.PlayerId, "Saldo insuficiente para el seguro.").ConfigureAwait(false);
                        return;
                    }

                    charged = true;
                    _round.TakeInsurance(seat.Index, amount);
                    await SendBalanceAsync(message.PlayerId).ConfigureAwait(false);
                }
                else
                {
                    _round.DeclineInsurance(seat.Index);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
            {
                // El motor rechazó el seguro (importe fuera de rango, por
                // ejemplo). Se devuelve lo cobrado antes de avisar.
                if (charged)
                {
                    await _wallet.CreditAsync(message.PlayerId, message.Request.Amount, LedgerEntryType.Refund, _roundId)
                        .ConfigureAwait(false);
                    await SendBalanceAsync(message.PlayerId).ConfigureAwait(false);
                }

                await RejectAsync(message.PlayerId, ex.Message).ConfigureAwait(false);
            }

            // Quien vuelca los eventos es OnInsuranceAsync, una sola vez tras
            // recorrer todos los asientos.
        }

        private async Task OnActAsync(ActMessage message)
        {
            if (_phase != TablePhase.PlayerTurns || _round == null)
            {
                await RejectAsync(message.PlayerId, "No es momento de jugar.").ConfigureAwait(false);
                return;
            }

            int seatIndex = _round.CurrentSeatIndex;
            if (seatIndex < 0 || _seats[seatIndex].PlayerId != message.PlayerId)
            {
                await RejectAsync(message.PlayerId, "No es tu turno.").ConfigureAwait(false);
                return;
            }

            // Doblar y partir cobran fichas adicionales. Se descuentan aquí
            // para que el libro cuadre con lo que hay sobre el tapete.
            decimal extra = ExtraStakeFor(message.Action, seatIndex);
            if (extra > 0m
                && !await _wallet.TryDebitAsync(message.PlayerId, extra, LedgerEntryType.Bet, _roundId).ConfigureAwait(false))
            {
                await RejectAsync(message.PlayerId, "Saldo insuficiente para esa acción.").ConfigureAwait(false);
                return;
            }

            try
            {
                _round.Act(seatIndex, message.Action);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
            {
                // La acción no era legal: se devuelve lo cobrado y se avisa.
                if (extra > 0m)
                {
                    await _wallet.CreditAsync(message.PlayerId, extra, LedgerEntryType.Refund, _roundId).ConfigureAwait(false);
                    await SendBalanceAsync(message.PlayerId).ConfigureAwait(false);
                }

                await RejectAsync(message.PlayerId, ex.Message).ConfigureAwait(false);
                return;
            }

            if (extra > 0m) await SendBalanceAsync(message.PlayerId).ConfigureAwait(false);

            await FlushRoundAsync().ConfigureAwait(false);
        }

        private decimal ExtraStakeFor(PlayerAction action, int seatIndex)
        {
            if (action != PlayerAction.Double && action != PlayerAction.Split) return 0m;
            if (_round == null) return 0m;

            foreach (Seat seat in _round.Seats)
            {
                if (seat.Index != seatIndex) continue;
                if (_round.CurrentHandIndex < 0 || _round.CurrentHandIndex >= seat.Hands.Count) return 0m;
                return seat.Hands[_round.CurrentHandIndex].Bet;
            }

            return 0m;
        }

        /// <summary>
        /// Manda los eventos nuevos del motor y ajusta la fase de la mesa a la
        /// de la ronda. Se llama tras cada operación que pueda mover la ronda.
        /// </summary>
        private async Task FlushRoundAsync()
        {
            if (_round == null) return;

            var pending = new List<RoundEventDto>();
            for (int i = _emittedEvents; i < _round.Events.Count; i++)
            {
                pending.Add(RoundEventDto.From(_round.Events[i]));
            }
            _emittedEvents = _round.Events.Count;

            if (pending.Count > 0)
            {
                await _hub.Clients.Group(TableGroup(TableId))
                    .SendAsync(HubMethods.Server.RoundEvents, pending).ConfigureAwait(false);
            }

            switch (_round.Phase)
            {
                case RoundPhase.Insurance:
                    SetPhase(TablePhase.Insurance, _options.InsuranceSeconds);
                    break;

                case RoundPhase.PlayerTurns:
                    // Se reinicia el reloj: cada jugador dispone de su turno
                    // completo, no de lo que sobre del anterior.
                    SetPhase(TablePhase.PlayerTurns, _options.TurnSeconds);
                    break;

                case RoundPhase.Complete:
                    await SettleRoundAsync().ConfigureAwait(false);
                    return;
            }

            await BroadcastSnapshotAsync().ConfigureAwait(false);
        }

        private async Task SettleRoundAsync()
        {
            if (_round == null) return;

            foreach (Seat roundSeat in _round.Seats)
            {
                TableSeat seat = _seats[roundSeat.Index];
                if (!seat.IsOccupied) continue;

                Guid playerId = seat.PlayerId!.Value;
                seat.LastRoundReturned = roundSeat.TotalReturned;

                if (roundSeat.TotalReturned > 0m)
                {
                    await _wallet.CreditAsync(playerId, roundSeat.TotalReturned, LedgerEntryType.Payout, _roundId)
                        .ConfigureAwait(false);
                }

                await SendBalanceAsync(playerId).ConfigureAwait(false);
                await ArchiveSeatAsync(playerId, roundSeat).ConfigureAwait(false);
            }

            SetPhase(TablePhase.Payout, _options.PayoutSeconds);
            await BroadcastSnapshotAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Guarda la ronda en el historial. Si falla se registra y se sigue:
        /// perder una fila de historial es malo, tumbar la mesa a cinco
        /// jugadores lo es más.
        /// </summary>
        private async Task ArchiveSeatAsync(Guid playerId, Seat roundSeat)
        {
            if (_round == null || _roundId == null) return;

            try
            {
                await _archive.ArchiveAsync(_roundId, TableId, playerId, roundSeat, _round.DealerHand)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "No se pudo archivar la ronda {RoundId} del jugador {Player}", _roundId, playerId);
            }
        }

        private async Task CleanupAndReopenAsync()
        {
            _round = null;
            _roundId = null;
            _emittedEvents = 0;

            for (int i = 0; i < _seats.Length; i++)
            {
                _seats[i].ClearBets();
            }

            PurgeAbandonedSeats();

            SetPhase(AnyoneSeated() ? TablePhase.Betting : TablePhase.WaitingForPlayers,
                     AnyoneSeated() ? _options.BettingSeconds : (int?)null);

            await BroadcastSnapshotAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Libera los asientos de quienes agotaron su ventana de reconexión.
        ///
        /// Se llama entre rondas, nunca durante una: quitarle el sitio a
        /// alguien con cartas y fichas sobre la mesa dejaría la ronda a medias.
        /// </summary>
        private void PurgeAbandonedSeats()
        {
            DateTime now = DateTime.UtcNow;

            for (int i = 0; i < _seats.Length; i++)
            {
                TableSeat seat = _seats[i];

                if (!seat.HasAbandonedSeat(now, _options.ReconnectWindowSeconds)) continue;

                _log.LogInformation("{Player} agotó su ventana de reconexión; se libera el asiento {Seat}",
                    seat.PlayerId, seat.Index);
                seat.Vacate();
            }
        }

        // ------------------------------------------------------------------
        // Estado y envío
        // ------------------------------------------------------------------

        private void SetPhase(TablePhase phase, int? seconds)
        {
            _phase = phase;
            _deadlineUtc = seconds.HasValue ? DateTime.UtcNow.AddSeconds(seconds.Value) : null;
        }

        private bool AnyoneSeated()
        {
            for (int i = 0; i < _seats.Length; i++)
            {
                if (_seats[i].IsOccupied) return true;
            }
            return false;
        }

        /// <summary>
        /// Primer asiento del jugador, o null. Solo vale cuando da igual cuál
        /// de ellos sea; para apostar o actuar hay que usar el índice concreto,
        /// porque un jugador puede ocupar varios.
        /// </summary>
        private TableSeat? FindSeat(Guid playerId)
        {
            for (int i = 0; i < _seats.Length; i++)
            {
                if (_seats[i].PlayerId == playerId) return _seats[i];
            }
            return null;
        }

        /// <summary>
        /// Asiento concreto del jugador, comprobando que sea suyo. Devuelve
        /// null si el índice no existe o lo ocupa otro.
        /// </summary>
        private TableSeat? FindSeat(Guid playerId, int seatIndex)
        {
            if (seatIndex < 0 || seatIndex >= _seats.Length) return null;
            return _seats[seatIndex].PlayerId == playerId ? _seats[seatIndex] : null;
        }

        private List<TableSeat> SeatsOf(Guid playerId)
        {
            var seats = new List<TableSeat>(2);

            for (int i = 0; i < _seats.Length; i++)
            {
                if (_seats[i].PlayerId == playerId) seats.Add(_seats[i]);
            }

            return seats;
        }

        private int CountSeatsOf(Guid playerId)
        {
            int count = 0;

            for (int i = 0; i < _seats.Length; i++)
            {
                if (_seats[i].PlayerId == playerId) count++;
            }

            return count;
        }

        /// <summary>
        /// Avisa al jugador de qué asientos ocupa. El snapshot no puede
        /// llevarlo: se difunde igual a toda la mesa.
        /// </summary>
        private Task SendSeatsAsync(Guid playerId)
        {
            var indexes = new List<int>(2);

            for (int i = 0; i < _seats.Length; i++)
            {
                if (_seats[i].PlayerId == playerId) indexes.Add(i);
            }

            return _hub.Clients.Group(PlayerGroup(playerId))
                .SendAsync(HubMethods.Server.YourSeats, indexes);
        }

        public TableSnapshot BuildSnapshot()
        {
            var snapshot = new TableSnapshot
            {
                TableId = TableId,
                Phase = _phase,
                DeadlineUtc = _deadlineUtc,
                Rules = BuildRulesDto(),
                CurrentSeat = _round?.CurrentSeatIndex ?? -1,
                CurrentHand = _round?.CurrentHandIndex ?? -1,
                ShoeCardsDealt = _shoe.CardsDealt,
                ShoeTotalCards = _shoe.TotalCards
            };

            if (_round != null && _phase == TablePhase.PlayerTurns)
            {
                snapshot.LegalActions = new List<PlayerAction>(_round.CurrentLegalActions);
            }

            BuildDealerView(snapshot);

            for (int i = 0; i < _seats.Length; i++)
            {
                snapshot.Seats.Add(BuildSeatDto(_seats[i]));
            }

            return snapshot;
        }

        private void BuildDealerView(TableSnapshot snapshot)
        {
            if (_round == null) return;

            IReadOnlyList<Card> cards = _round.DealerHand.Cards;

            if (_round.HoleCardRevealed)
            {
                foreach (Card card in cards) snapshot.DealerCards.Add(card.Id);
                snapshot.DealerHasHoleCard = false;
                snapshot.DealerVisibleTotal = _round.DealerHand.Value.Total;
                snapshot.DealerVisibleSoft = _round.DealerHand.Value.IsSoft;
                return;
            }

            // Solo la descubierta. La tapada existe en el motor pero no sale
            // de él: es la misma garantía que da el registro de eventos.
            if (cards.Count > 0)
            {
                snapshot.DealerCards.Add(cards[0].Id);
                HandValue visible = Hand.Evaluate(new[] { cards[0] });
                snapshot.DealerVisibleTotal = visible.Total;
                snapshot.DealerVisibleSoft = visible.IsSoft;
            }

            snapshot.DealerHasHoleCard = cards.Count > 1;
        }

        private SeatDto BuildSeatDto(TableSeat seat)
        {
            var dto = new SeatDto
            {
                Index = seat.Index,
                PlayerName = seat.PlayerName,
                IsConnected = seat.IsConnected,
                MainBet = seat.MainBet,
                PerfectPairsBet = seat.PerfectPairsBet,
                TwentyOnePlus3Bet = seat.TwentyOnePlus3Bet,
                HasBetThisRound = seat.HasBet,
                IsReady = seat.IsReady,
                LastRoundReturned = seat.LastRoundReturned
            };

            if (_round == null) return dto;

            foreach (Seat roundSeat in _round.Seats)
            {
                if (roundSeat.Index != seat.Index) continue;

                dto.InsuranceBet = roundSeat.InsuranceBet;

                foreach (PlayerHand hand in roundSeat.Hands)
                {
                    dto.Hands.Add(BuildHandDto(hand));
                }
                break;
            }

            return dto;
        }

        private static HandDto BuildHandDto(PlayerHand hand)
        {
            var dto = new HandDto
            {
                Total = hand.Hand.Value.Total,
                IsSoft = hand.Hand.Value.IsSoft,
                IsBlackjack = hand.Hand.IsBlackjack,
                IsBust = hand.Hand.IsBust,
                Bet = hand.Bet,
                IsDoubled = hand.IsDoubled,
                IsSurrendered = hand.IsSurrendered,
                IsFinished = hand.IsFinished,
                IsFromSplit = hand.Hand.IsFromSplit
            };

            foreach (Card card in hand.Hand.Cards) dto.Cards.Add(card.Id);

            return dto;
        }

        private TableRulesDto BuildRulesDto() => new()
        {
            DeckCount = _rules.DeckCount,
            DealerHitsSoft17 = _rules.DealerHitsSoft17,
            BlackjackPayout = _rules.BlackjackPayout,
            InsurancePayout = _rules.InsurancePayout,
            MaxSplits = _rules.MaxSplits,
            DoubleAfterSplit = _rules.DoubleAfterSplit,
            LateSurrender = _rules.LateSurrender,
            MinBet = _rules.MinBet,
            MaxBet = _rules.MaxBet,
            SideBetsEnabled = _rules.SideBetsEnabled,
            SeatCount = _options.SeatCount
        };

        private Task BroadcastSnapshotAsync()
            => _hub.Clients.Group(TableGroup(TableId))
                .SendAsync(HubMethods.Server.Snapshot, BuildSnapshot());

        private Task SendSnapshotToPlayerAsync(Guid playerId)
            => _hub.Clients.Group(PlayerGroup(playerId))
                .SendAsync(HubMethods.Server.Snapshot, BuildSnapshot());

        private async Task SendBalanceAsync(Guid playerId)
        {
            decimal balance = await _wallet.GetBalanceAsync(playerId).ConfigureAwait(false);
            await _hub.Clients.Group(PlayerGroup(playerId))
                .SendAsync(HubMethods.Server.BalanceChanged, balance).ConfigureAwait(false);
        }

        private Task RejectAsync(Guid playerId, string reason)
            => _hub.Clients.Group(PlayerGroup(playerId))
                .SendAsync(HubMethods.Server.CommandRejected, new CommandRejected(reason));

        public async ValueTask DisposeAsync()
        {
            _stopping.Cancel();
            _mailbox.Writer.TryComplete();

            try { await _loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* cierre esperado */ }

            _stopping.Dispose();
        }
    }
}
