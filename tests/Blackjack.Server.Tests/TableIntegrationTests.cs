using System;
using System.Linq;
using System.Threading.Tasks;
using Blackjack.Core.Rounds;
using Blackjack.Core.Rules;
using Blackjack.Protocol;
using Blackjack.Protocol.Dtos;
using Xunit;

namespace Blackjack.Server.Tests
{
    /// <summary>
    /// Recorre el servidor de punta a punta con clientes SignalR reales.
    ///
    /// Cada test usa su propia mesa y sus propios jugadores: el monedero es
    /// un singleton compartido, así que reutilizar identificadores haría que
    /// los saldos se pisaran entre pruebas.
    /// </summary>
    public sealed class TableIntegrationTests : IClassFixture<TableTestHost>
    {
        private readonly TableTestHost _host;

        public TableIntegrationTests(TableTestHost host)
        {
            _host = host;
        }

        private static string NewTableId() => "mesa-" + Guid.NewGuid().ToString("N")[..8];

        // ------------------------------------------------------------------
        // Entrar y sentarse
        // ------------------------------------------------------------------

        [Fact]
        public async Task AlEntrar_LlegaElEstadoDeLaMesaYElSaldo()
        {
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");

            await ana.JoinTableAsync(table);

            await TableTestHost.WaitForAsync(() => ana.Snapshot != null, "el snapshot inicial");

            Assert.Equal(table, ana.Snapshot!.TableId);
            Assert.Equal(TablePhase.WaitingForPlayers, ana.Snapshot.Phase);
            Assert.Equal(5, ana.Snapshot.Seats.Count);
            Assert.Equal(1000m, ana.Balance);

            // Las reglas viajan al cliente para poder rotular el fieltro.
            Assert.Equal(6, ana.Snapshot.Rules.DeckCount);
            Assert.False(ana.Snapshot.Rules.DealerHitsSoft17);
            Assert.Equal(1.5m, ana.Snapshot.Rules.BlackjackPayout);
        }

        [Fact]
        public async Task SentarseSeVeDesdeLosDemasAsientos()
        {
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");
            await using TestClient beto = await _host.ConnectNewPlayerAsync("Beto");

            await ana.JoinTableAsync(table);
            await beto.JoinTableAsync(table);
            await TableTestHost.WaitForAsync(() => beto.Snapshot != null, "el snapshot de Beto");

            await ana.SitAsync(table, 2);

            await TableTestHost.WaitForAsync(
                () => beto.Snapshot!.Seats[2].PlayerName == "Ana",
                "que Beto vea a Ana sentada");

            Assert.True(beto.Snapshot!.Seats[2].IsConnected);
            Assert.Equal(TablePhase.Betting, beto.Snapshot.Phase);
            Assert.NotNull(beto.Snapshot.DeadlineUtc);
        }

        [Fact]
        public async Task ElAsientoOcupado_NoSePuedeRobar()
        {
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");
            await using TestClient beto = await _host.ConnectNewPlayerAsync("Beto");

            await ana.JoinTableAsync(table);
            await beto.JoinTableAsync(table);
            await ana.SitAsync(table, 0);
            await TableTestHost.WaitForAsync(() => ana.Snapshot?.Seats[0].PlayerName == "Ana", "a Ana sentada");

            await beto.SitAsync(table, 0);

            await TableTestHost.WaitForAsync(() => beto.Rejections.Count > 0, "el rechazo a Beto");
            Assert.Contains("ocupado", beto.Rejections[0].Reason);
        }

        // ------------------------------------------------------------------
        // Apuestas
        // ------------------------------------------------------------------

        [Fact]
        public async Task Apostar_DescuentaDelSaldoAlInstante()
        {
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");

            await ana.JoinTableAsync(table);
            await ana.SitAsync(table, 0);
            await TableTestHost.WaitForAsync(() => ana.Snapshot?.Phase == TablePhase.Betting, "la ventana de apuestas");

            await ana.PlaceBetAsync(table, 100m, perfectPairs: 10m);

            await TableTestHost.WaitForAsync(() => ana.Balance == 890m, "el saldo descontado");
            Assert.Equal(890m, ana.Balance); // 1000 - 100 - 10
        }

        [Fact]
        public async Task ApostarMasDelSaldo_SeRechaza()
        {
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");

            await ana.JoinTableAsync(table);
            await ana.SitAsync(table, 0);
            await TableTestHost.WaitForAsync(() => ana.Snapshot?.Phase == TablePhase.Betting, "la ventana de apuestas");

            // Por encima del máximo de mesa, que además supera el saldo.
            await ana.PlaceBetAsync(table, 5000m);

            await TableTestHost.WaitForAsync(() => ana.Rejections.Count > 0, "el rechazo de la apuesta");
            Assert.Equal(1000m, ana.Balance); // intacto
        }

        [Fact]
        public async Task UnEspectadorNoPuedeApostar()
        {
            // Beto se sienta para que la ventana de apuestas esté abierta;
            // Ana mira desde fuera e intenta apostar igualmente.
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");
            await using TestClient beto = await _host.ConnectNewPlayerAsync("Beto");

            await ana.JoinTableAsync(table);
            await beto.JoinTableAsync(table);
            await beto.SitAsync(table, 0);
            await TableTestHost.WaitForAsync(() => ana.Snapshot?.Phase == TablePhase.Betting, "la ventana de apuestas");

            await ana.PlaceBetAsync(table, 100m);

            await TableTestHost.WaitForAsync(() => ana.Rejections.Count > 0, "el rechazo a Ana");
            Assert.Contains("asiento", ana.Rejections[0].Reason);
            Assert.Equal(1000m, ana.Balance); // no se le cobró nada
        }

        [Fact]
        public async Task ApostarConLaMesaVacia_SeRechazaPorVentanaCerrada()
        {
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");

            await ana.JoinTableAsync(table);
            await TableTestHost.WaitForAsync(() => ana.Snapshot != null, "el snapshot");

            // Sin nadie sentado la mesa ni siquiera abre apuestas.
            await ana.PlaceBetAsync(table, 100m);

            await TableTestHost.WaitForAsync(() => ana.Rejections.Count > 0, "el rechazo");
            Assert.Contains("apuestas está cerrada", ana.Rejections[0].Reason);
        }

        [Fact]
        public async Task LevantarseAntesDeRepartir_DevuelveLasFichas()
        {
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");

            await ana.JoinTableAsync(table);
            await ana.SitAsync(table, 0);
            await TableTestHost.WaitForAsync(() => ana.Snapshot?.Phase == TablePhase.Betting, "la ventana de apuestas");

            await ana.PlaceBetAsync(table, 100m);
            await TableTestHost.WaitForAsync(() => ana.Balance == 900m, "el cargo de la apuesta");

            await ana.StandUpAsync(table);

            await TableTestHost.WaitForAsync(() => ana.Balance == 1000m, "la devolución de las fichas");
        }

        // ------------------------------------------------------------------
        // Ronda
        // ------------------------------------------------------------------

        [Fact]
        public async Task LaRondaArranca_SolaAlCerrarseLasApuestas()
        {
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");

            await ana.JoinTableAsync(table);
            await ana.SitAsync(table, 0);
            await TableTestHost.WaitForAsync(() => ana.Snapshot?.Phase == TablePhase.Betting, "la ventana de apuestas");
            await ana.PlaceBetAsync(table, 100m);

            // Nadie pulsa nada: el temporizador reparte por sí solo.
            await TableTestHost.WaitForAsync(
                () => ana.Events.Count(e => e.Type == RoundEventType.CardDealt) >= 4,
                "el reparto inicial");

            var repartidas = ana.Events.Where(e => e.Type == RoundEventType.CardDealt).ToList();

            // Dos al jugador y dos al croupier.
            Assert.Equal(2, repartidas.Count(e => e.SeatIndex == 0));
            Assert.Equal(2, repartidas.Count(e => e.SeatIndex == RoundEvent.DealerSeat));
        }

        [Fact]
        public async Task LaCartaTapadaDelCroupier_NoLlegaNuncaAlCliente()
        {
            // La garantía central del diseño: si esto falla, cualquiera puede
            // leer la hole card desde el cliente y el juego pierde el sentido.
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");

            await ana.JoinTableAsync(table);
            await ana.SitAsync(table, 0);
            await TableTestHost.WaitForAsync(() => ana.Snapshot?.Phase == TablePhase.Betting, "la ventana de apuestas");
            await ana.PlaceBetAsync(table, 100m);

            await TableTestHost.WaitForAsync(
                () => ana.Events.Any(e => e.Type == RoundEventType.CardDealt && e.FaceDown),
                "la carta tapada");

            RoundEventDto tapada = ana.Events.First(e => e.Type == RoundEventType.CardDealt && e.FaceDown);

            Assert.Null(tapada.CardId);
            Assert.Equal(RoundEvent.DealerSeat, tapada.SeatIndex);

            // Y el snapshot tampoco la incluye: solo la descubierta.
            await TableTestHost.WaitForAsync(
                () => ana.Snapshot!.DealerCards.Count > 0,
                "las cartas visibles del croupier");

            Assert.Single(ana.Snapshot!.DealerCards);
            Assert.True(ana.Snapshot.DealerHasHoleCard);
        }

        [Fact]
        public async Task UnaRondaCompleta_LiquidaYReabreLasApuestas()
        {
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");

            await ana.JoinTableAsync(table);
            await ana.SitAsync(table, 0);
            await TableTestHost.WaitForAsync(() => ana.Snapshot?.Phase == TablePhase.Betting, "la ventana de apuestas");
            await ana.PlaceBetAsync(table, 100m);

            await PlayUntilSettledAsync(ana, table);

            Assert.Contains(ana.Events, e => e.Type == RoundEventType.HandSettled);
            Assert.Contains(ana.Events, e => e.Type == RoundEventType.RoundComplete);

            // La carta tapada acaba destapándose al jugar el croupier.
            Assert.Contains(ana.Events, e => e.Type == RoundEventType.HoleCardRevealed);
            RoundEventDto revelada = ana.Events.First(e => e.Type == RoundEventType.HoleCardRevealed);
            Assert.NotNull(revelada.CardId);

            // Y la mesa vuelve sola a admitir apuestas.
            await TableTestHost.WaitForAsync(
                () => ana.Snapshot?.Phase == TablePhase.Betting,
                "la reapertura de apuestas");
        }

        [Fact]
        public async Task JugarFueraDeTurno_SeRechaza()
        {
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");
            await using TestClient beto = await _host.ConnectNewPlayerAsync("Beto");

            await ana.JoinTableAsync(table);
            await beto.JoinTableAsync(table);
            await ana.SitAsync(table, 0);
            await beto.SitAsync(table, 1);
            await TableTestHost.WaitForAsync(() => beto.Snapshot?.Phase == TablePhase.Betting, "la ventana de apuestas");

            // Se juegan rondas hasta que le toque al asiento 0. Una mano puede
            // resolverse sin turnos si hay blackjack, y dar por hecho que la
            // primera reparte turno haría el test escamoso.
            await PlayUntilSeatZeroTurnAsync(ana, beto, table);

            Assert.Equal(0, ana.Snapshot!.CurrentSeat);

            beto.ClearEvents();
            await beto.ActAsync(table, PlayerAction.Hit);

            await TableTestHost.WaitForAsync(() => beto.Rejections.Count > 0, "el rechazo a Beto");
            Assert.Contains("turno", beto.Rejections[^1].Reason);
        }

        [Fact]
        public async Task ElTurnoQueExpira_SePlantaSolo()
        {
            // Sin esto, un jugador ausente congela la mesa a los demás.
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");

            await ana.JoinTableAsync(table);
            await ana.SitAsync(table, 0);
            await TableTestHost.WaitForAsync(() => ana.Snapshot?.Phase == TablePhase.Betting, "la ventana de apuestas");
            await ana.PlaceBetAsync(table, 100m);

            // No se responde a nada: ni al seguro ni al turno.
            await TableTestHost.WaitForAsync(
                () => ana.Events.Any(e => e.Type == RoundEventType.RoundComplete),
                "que la ronda termine sola",
                timeoutMs: 20000);

            Assert.Contains(ana.Events, e => e.Type == RoundEventType.HandSettled);
        }

        [Fact]
        public async Task LasAccionesLegales_ViajanConElCambioDeTurno()
        {
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");

            await ana.JoinTableAsync(table);
            await ana.SitAsync(table, 0);

            // Se juegan rondas hasta que haya turno. Una mano puede acabar sin
            // ninguno —blackjack del jugador, o del croupier tras el peek— y
            // esperar un único reparto haría que el test fallara una de cada
            // diez veces sin que nada estuviera roto.
            RoundEventDto turno = await PlayUntilTurnAsync(ana, table);

            Assert.NotNull(turno.LegalActions);

            // Si hay turno, la mano sigue viva y suma menos de 21: pedir y
            // plantarse son legales siempre en ese punto.
            Assert.Contains(PlayerAction.Stand, turno.LegalActions!);
            Assert.Contains(PlayerAction.Hit, turno.LegalActions!);
        }

        /// <summary>
        /// Juega rondas con dos asientos hasta que el turno recaiga en el
        /// asiento 0, dejando la mesa lista para probar el orden de juego.
        /// </summary>
        private static async Task PlayUntilSeatZeroTurnAsync(TestClient first, TestClient second, string table)
        {
            var deadline = DateTime.UtcNow.AddSeconds(60);
            bool betsPlaced = false;

            while (DateTime.UtcNow < deadline)
            {
                TableSnapshot? snapshot = first.Snapshot;

                if (snapshot?.Phase == TablePhase.PlayerTurns && snapshot.CurrentSeat == 0) return;

                if (snapshot?.Phase == TablePhase.Betting)
                {
                    if (!betsPlaced)
                    {
                        await first.PlaceBetAsync(table, 100m);
                        await second.PlaceBetAsync(table, 100m);
                        betsPlaced = true;
                    }
                }
                else if (snapshot?.Phase == TablePhase.Insurance)
                {
                    await first.RespondInsuranceAsync(table, take: false);
                    await second.RespondInsuranceAsync(table, take: false);
                }
                else if (snapshot?.Phase == TablePhase.PlayerTurns)
                {
                    // Le tocó al asiento 1 primero (el 0 sacó blackjack): se
                    // planta para que la ronda avance y llegue la siguiente.
                    await second.ActAsync(table, PlayerAction.Stand);
                }
                else
                {
                    betsPlaced = false;
                }

                await Task.Delay(50);
            }

            throw new TimeoutException("El asiento 0 no llegó a tener turno en 60 segundos.");
        }

        /// <summary>
        /// Apuesta ronda tras ronda hasta que el servidor ofrezca un turno, y
        /// devuelve ese evento.
        /// </summary>
        private static async Task<RoundEventDto> PlayUntilTurnAsync(TestClient client, string table)
        {
            var deadline = DateTime.UtcNow.AddSeconds(60);
            bool betPlaced = false;

            while (DateTime.UtcNow < deadline)
            {
                RoundEventDto? turn = client.Events.FirstOrDefault(e => e.Type == RoundEventType.TurnChanged);
                if (turn != null) return turn;

                TableSnapshot? snapshot = client.Snapshot;

                if (snapshot?.Phase == TablePhase.Betting)
                {
                    if (!betPlaced)
                    {
                        await client.PlaceBetAsync(table, 100m);
                        betPlaced = true;
                    }
                }
                else if (snapshot?.Phase == TablePhase.Insurance)
                {
                    await client.RespondInsuranceAsync(table, take: false);
                }
                else
                {
                    // Fuera de la ventana de apuestas: la siguiente vez que se
                    // abra hay que volver a apostar.
                    betPlaced = false;
                }

                await Task.Delay(50);
            }

            throw new TimeoutException("No se ofreció ningún turno en 60 segundos.");
        }

        // ------------------------------------------------------------------
        // Utilidades
        // ------------------------------------------------------------------

        /// <summary>
        /// Se planta en todo hasta que la ronda liquida. Sirve para llegar al
        /// final sin depender de qué cartas hayan salido.
        /// </summary>
        private static async Task PlayUntilSettledAsync(TestClient client, string table)
        {
            var deadline = DateTime.UtcNow.AddSeconds(25);

            while (DateTime.UtcNow < deadline)
            {
                if (client.Events.Any(e => e.Type == RoundEventType.RoundComplete)) return;

                TableSnapshot? snapshot = client.Snapshot;

                if (snapshot?.Phase == TablePhase.Insurance)
                {
                    await client.RespondInsuranceAsync(table, take: false);
                }
                else if (snapshot?.Phase == TablePhase.PlayerTurns && snapshot.CurrentSeat == 0)
                {
                    await client.ActAsync(table, PlayerAction.Stand);
                }

                await Task.Delay(50);
            }

            throw new TimeoutException("La ronda no llegó a liquidarse.");
        }
    }
}
