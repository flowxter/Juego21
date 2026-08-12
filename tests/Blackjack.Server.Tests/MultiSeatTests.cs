using System;
using System.Linq;
using System.Threading.Tasks;
using Blackjack.Core.Rounds;
using Blackjack.Protocol;
using Blackjack.Protocol.Dtos;
using Xunit;

namespace Blackjack.Server.Tests
{
    /// <summary>
    /// Un mismo jugador ocupando varios asientos, como quien juega dos o tres
    /// manos a la vez en una mesa real.
    /// </summary>
    public sealed class MultiSeatTests : IClassFixture<TableTestHost>
    {
        private readonly TableTestHost _host;

        public MultiSeatTests(TableTestHost host)
        {
            _host = host;
        }

        private static string NewTableId() => "mesa-" + Guid.NewGuid().ToString("N")[..8];

        [Fact]
        public async Task UnJugadorPuedeOcuparVariosAsientos()
        {
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");

            await ana.JoinTableAsync(table);
            await ana.SitAsync(table, 0);
            await ana.SitAsync(table, 2);

            await TableTestHost.WaitForAsync(
                () => ana.Snapshot != null
                   && ana.Snapshot.Seats[0].PlayerName == "Ana"
                   && ana.Snapshot.Seats[2].PlayerName == "Ana",
                "los dos asientos ocupados");

            Assert.Null(ana.Snapshot!.Seats[1].PlayerName);
            Assert.Empty(ana.Rejections);
        }

        [Fact]
        public async Task ElServidorAvisaDeQueAsientosSonTuyos()
        {
            // El snapshot se difunde igual a toda la mesa, así que los asientos
            // propios llegan por un canal aparte.
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");

            await ana.JoinTableAsync(table);
            await ana.SitAsync(table, 1);
            await ana.SitAsync(table, 3);

            await TableTestHost.WaitForAsync(
                () => ana.MySeats.Count == 2, "el aviso de asientos propios");

            Assert.Contains(1, ana.MySeats);
            Assert.Contains(3, ana.MySeats);
        }

        [Fact]
        public async Task NoSePuedeSuperarElTopeDeAsientos()
        {
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");

            await ana.JoinTableAsync(table);
            await ana.SitAsync(table, 0);
            await ana.SitAsync(table, 1);
            await ana.SitAsync(table, 2);

            await TableTestHost.WaitForAsync(() => ana.MySeats.Count == 3, "los tres asientos");

            // El cuarto sobra: el tope de la mesa es 3.
            await ana.SitAsync(table, 3);

            await TableTestHost.WaitForAsync(() => ana.Rejections.Count > 0, "el rechazo del cuarto");
            Assert.Contains("más de 3", ana.Rejections[^1].Reason);
            Assert.Equal(3, ana.MySeats.Count);
        }

        [Fact]
        public async Task CadaAsientoLlevaSuPropiaApuesta()
        {
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");

            await ana.JoinTableAsync(table);
            await ana.SitAsync(table, 0);
            await ana.SitAsync(table, 1);
            await TableTestHost.WaitForAsync(
                () => ana.Snapshot?.Phase == TablePhase.Betting, "la ventana de apuestas");

            await ana.PlaceBetAsync(table, 100m, seatIndex: 0);
            await ana.PlaceBetAsync(table, 25m, seatIndex: 1);

            await TableTestHost.WaitForAsync(
                () => ana.Snapshot!.Seats[0].MainBet == 100m && ana.Snapshot.Seats[1].MainBet == 25m,
                "las dos apuestas distintas");

            // Se cobran las dos: 1000 - 125.
            await TableTestHost.WaitForAsync(() => ana.Balance == 875m, "el cargo de ambas apuestas");
        }

        [Fact]
        public async Task ApostarEnUnAsientoAjeno_SeRechaza()
        {
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");
            await using TestClient beto = await _host.ConnectNewPlayerAsync("Beto");

            await ana.JoinTableAsync(table);
            await beto.JoinTableAsync(table);
            await ana.SitAsync(table, 0);
            await beto.SitAsync(table, 1);
            await TableTestHost.WaitForAsync(
                () => ana.Snapshot?.Phase == TablePhase.Betting, "la ventana de apuestas");

            // Ana intenta apostar en el asiento de Beto.
            await ana.PlaceBetAsync(table, 100m, seatIndex: 1);

            await TableTestHost.WaitForAsync(() => ana.Rejections.Count > 0, "el rechazo");
            Assert.Contains("no es tuyo", ana.Rejections[^1].Reason);
            Assert.Equal(1000m, ana.Balance);
        }

        [Fact]
        public async Task ConVariosAsientos_SeJuegaUnaManoEnCadaUno()
        {
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");

            await ana.JoinTableAsync(table);
            await ana.SitAsync(table, 0);
            await ana.SitAsync(table, 1);
            await TableTestHost.WaitForAsync(
                () => ana.Snapshot?.Phase == TablePhase.Betting, "la ventana de apuestas");

            await ana.PlaceBetAsync(table, 50m, seatIndex: 0);
            await ana.PlaceBetAsync(table, 50m, seatIndex: 1);

            await PlayUntilCompleteAsync(ana, table);

            // Ambos asientos recibieron cartas y se liquidaron por separado.
            var settled = ana.Events
                .Where(e => e.Type == RoundEventType.HandSettled)
                .Select(e => e.SeatIndex)
                .Distinct()
                .ToList();

            Assert.Contains(0, settled);
            Assert.Contains(1, settled);
        }

        [Fact]
        public async Task DejarUnAsiento_MantieneElOtro()
        {
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");

            await ana.JoinTableAsync(table);
            await ana.SitAsync(table, 0);
            await ana.SitAsync(table, 4);
            await TableTestHost.WaitForAsync(() => ana.MySeats.Count == 2, "los dos asientos");

            await ana.StandUpAsync(table, 0);

            await TableTestHost.WaitForAsync(
                () => ana.MySeats.Count == 1 && ana.MySeats[0] == 4,
                "que quede solo el asiento 4");

            Assert.Null(ana.Snapshot!.Seats[0].PlayerName);
            Assert.Equal("Ana", ana.Snapshot.Seats[4].PlayerName);
        }

        // ------------------------------------------------------------------
        // Botón de listo
        // ------------------------------------------------------------------

        [Fact]
        public async Task MarcarseListo_RepartrSinEsperarAlReloj()
        {
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");

            await ana.JoinTableAsync(table);
            await ana.SitAsync(table, 0);
            await TableTestHost.WaitForAsync(
                () => ana.Snapshot?.Phase == TablePhase.Betting, "la ventana de apuestas");

            await ana.PlaceBetAsync(table, 100m);
            await TableTestHost.WaitForAsync(() => ana.Balance == 900m, "el cargo de la apuesta");

            DateTime before = DateTime.UtcNow;
            await ana.ReadyAsync(table);

            await TableTestHost.WaitForAsync(
                () => ana.Events.Any(e => e.Type == RoundEventType.CardDealt),
                "el reparto inmediato");

            // La ventana de apuestas del host de test dura 3 s; con "listo"
            // tiene que repartir bastante antes.
            Assert.True((DateTime.UtcNow - before).TotalSeconds < 2.5,
                "El reparto no se adelantó al pulsar Listo.");
        }

        [Fact]
        public async Task MarcarseListoSinApostar_SeRechaza()
        {
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");

            await ana.JoinTableAsync(table);
            await ana.SitAsync(table, 0);
            await TableTestHost.WaitForAsync(
                () => ana.Snapshot?.Phase == TablePhase.Betting, "la ventana de apuestas");

            await ana.ReadyAsync(table);

            await TableTestHost.WaitForAsync(() => ana.Rejections.Count > 0, "el rechazo");
            Assert.Contains("Apuesta antes", ana.Rejections[^1].Reason);
        }

        [Fact]
        public async Task LaMesaEsperaAQueTodosEstenListos()
        {
            string table = NewTableId();
            await using TestClient ana = await _host.ConnectNewPlayerAsync("Ana");
            await using TestClient beto = await _host.ConnectNewPlayerAsync("Beto");

            await ana.JoinTableAsync(table);
            await beto.JoinTableAsync(table);
            await ana.SitAsync(table, 0);
            await beto.SitAsync(table, 1);
            await TableTestHost.WaitForAsync(
                () => ana.Snapshot?.Phase == TablePhase.Betting, "la ventana de apuestas");

            await ana.PlaceBetAsync(table, 50m);
            await beto.PlaceBetAsync(table, 50m);
            await TableTestHost.WaitForAsync(
                () => ana.Snapshot!.Seats[0].MainBet == 50m && ana.Snapshot.Seats[1].MainBet == 50m,
                "las dos apuestas");

            await ana.ReadyAsync(table);

            await TableTestHost.WaitForAsync(
                () => ana.Snapshot!.Seats[0].IsReady, "que Ana conste como lista");

            // Beto no ha dicho nada, así que la mesa no reparte por su cuenta.
            Assert.False(ana.Snapshot!.Seats[1].IsReady);
        }

        // ------------------------------------------------------------------

        private static async Task PlayUntilCompleteAsync(TestClient client, string table)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);

            while (DateTime.UtcNow < deadline)
            {
                if (client.Events.Any(e => e.Type == RoundEventType.RoundComplete)) return;

                TableSnapshot? snapshot = client.Snapshot;

                if (snapshot?.Phase == TablePhase.Insurance)
                {
                    await client.RespondInsuranceAsync(table, take: false);
                }
                else if (snapshot?.Phase == TablePhase.PlayerTurns)
                {
                    await client.ActAsync(table, Core.Rules.PlayerAction.Stand);
                }

                await Task.Delay(50);
            }

            throw new TimeoutException("La ronda no llegó a liquidarse.");
        }
    }
}
