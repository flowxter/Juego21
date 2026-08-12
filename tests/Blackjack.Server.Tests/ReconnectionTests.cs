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
    /// Host con la ventana de reconexión reducida a 2 segundos, para poder ver
    /// expirar la gracia sin esperar el minuto real de producción.
    /// </summary>
    public sealed class ShortReconnectHost : TableTestHost
    {
        protected override int ReconnectWindowSeconds => 2;
    }

    /// <summary>
    /// Caídas de conexión y vueltas.
    ///
    /// Es lo que separa una mesa jugable de una frustrante: perder el wifi un
    /// momento no puede costarte el asiento ni las fichas que ya pusiste.
    /// </summary>
    public sealed class ReconnectionTests : IClassFixture<TableTestHost>
    {
        private readonly TableTestHost _host;

        public ReconnectionTests(TableTestHost host)
        {
            _host = host;
        }

        private static string NewTableId() => "mesa-" + Guid.NewGuid().ToString("N")[..8];

        [Fact]
        public async Task CaerseNoLiberaElAsiento()
        {
            string table = NewTableId();
            AuthResponse auth = await _host.RegisterAsync("Caido");
            await using TestClient beto = await _host.ConnectNewPlayerAsync("Beto");

            TestClient ana = await _host.ConnectAsync(auth);
            await ana.JoinTableAsync(table);
            await ana.SitAsync(table, 1);

            await beto.JoinTableAsync(table);
            await TableTestHost.WaitForAsync(
                () => beto.Snapshot?.Seats[1].PlayerName == "Caido", "a Ana sentada");

            // Se corta la conexión de Ana.
            await ana.DisposeAsync();

            await TableTestHost.WaitForAsync(
                () => beto.Snapshot!.Seats[1].IsConnected == false,
                "que Beto vea a Ana desconectada");

            // Sigue siendo su asiento: el nombre continúa ahí, solo atenuado.
            Assert.Equal("Caido", beto.Snapshot!.Seats[1].PlayerName);
        }

        [Fact]
        public async Task VolverConElMismoToken_RecuperaElAsiento()
        {
            string table = NewTableId();
            AuthResponse auth = await _host.RegisterAsync("Volvedor");

            TestClient primera = await _host.ConnectAsync(auth);
            await primera.JoinTableAsync(table);
            await primera.SitAsync(table, 3);
            await TableTestHost.WaitForAsync(
                () => primera.Snapshot?.Seats[3].PlayerName == "Volvedor", "el asiento ocupado");

            await primera.DisposeAsync();

            // Nueva conexión, mismo jugador.
            await using TestClient segunda = await _host.ConnectAsync(auth);
            await segunda.JoinTableAsync(table);

            await TableTestHost.WaitForAsync(
                () => segunda.Snapshot?.Seats[3].IsConnected == true,
                "la recuperación del asiento");

            Assert.Equal("Volvedor", segunda.Snapshot!.Seats[3].PlayerName);
            Assert.Equal(1000m, segunda.Balance);
        }

        [Fact]
        public async Task AlVolver_LlegaElEstadoCompletoSinHaberPerdidoNada()
        {
            // El snapshot es la red de seguridad: quien vuelve no necesita
            // reconstruir nada a partir de los eventos que se perdió.
            string table = NewTableId();
            AuthResponse auth = await _host.RegisterAsync("Resincronizado");

            TestClient primera = await _host.ConnectAsync(auth);
            await primera.JoinTableAsync(table);
            await primera.SitAsync(table, 0);
            await TableTestHost.WaitForAsync(
                () => primera.Snapshot?.Phase == TablePhase.Betting, "la ventana de apuestas");

            await primera.PlaceBetAsync(table, 100m);
            await TableTestHost.WaitForAsync(() => primera.Balance == 900m, "el cargo de la apuesta");
            await primera.DisposeAsync();

            await using TestClient segunda = await _host.ConnectAsync(auth);
            await segunda.JoinTableAsync(table);

            await TableTestHost.WaitForAsync(() => segunda.Snapshot != null, "el snapshot al volver");

            // El saldo que llega es el real, con la apuesta ya descontada.
            await TableTestHost.WaitForAsync(() => segunda.Balance == 900m, "el saldo tras volver");

            Assert.Equal(table, segunda.Snapshot!.TableId);
            Assert.Equal(5, segunda.Snapshot.Seats.Count);
        }

        [Fact]
        public async Task LasFichasApostadas_SiguenEnLaMesaTrasCaerse()
        {
            // Caerse después de apostar no devuelve las fichas: la apuesta
            // está puesta y la ronda se juega con ella, plantándose sola si
            // el jugador no vuelve a tiempo.
            string table = NewTableId();
            AuthResponse auth = await _host.RegisterAsync("Apostador");
            await using TestClient beto = await _host.ConnectNewPlayerAsync("Beto");

            TestClient ana = await _host.ConnectAsync(auth);
            await ana.JoinTableAsync(table);
            await ana.SitAsync(table, 0);
            await beto.JoinTableAsync(table);

            await TableTestHost.WaitForAsync(
                () => ana.Snapshot?.Phase == TablePhase.Betting, "la ventana de apuestas");
            await ana.PlaceBetAsync(table, 100m);
            await TableTestHost.WaitForAsync(() => ana.Balance == 900m, "el cargo de la apuesta");

            await ana.DisposeAsync();

            await TableTestHost.WaitForAsync(
                () => beto.Snapshot!.Seats[0].MainBet == 100m && !beto.Snapshot.Seats[0].IsConnected,
                "la apuesta en pie con el jugador caído");

            // La mesa sigue: se reparte y se planta por ella al expirar el turno.
            await TableTestHost.WaitForAsync(
                () => beto.Events.Any(e => e.Type == RoundEventType.RoundComplete),
                "que la ronda termine sin ella",
                timeoutMs: 25000);
        }
    }

    /// <summary>
    /// Expiración de la ventana de gracia, con host de 2 segundos.
    /// </summary>
    public sealed class ReconnectExpiryTests : IClassFixture<ShortReconnectHost>
    {
        private readonly ShortReconnectHost _host;

        public ReconnectExpiryTests(ShortReconnectHost host)
        {
            _host = host;
        }

        private static string NewTableId() => "mesa-" + Guid.NewGuid().ToString("N")[..8];

        [Fact]
        public async Task AlAgotarseLaGracia_ElAsientoQuedaLibre()
        {
            // Este caso no pasaba por la fase de pagos: el jugador se va sin
            // apostar, así que la purga tiene que ocurrir también cuando la
            // ventana de apuestas se cierra vacía.
            string table = NewTableId();
            AuthResponse auth = await _host.RegisterAsync("Fugado");
            await using TestClient observador = await _host.ConnectNewPlayerAsync("Observador");

            TestClient ana = await _host.ConnectAsync(auth);
            await ana.JoinTableAsync(table);
            await ana.SitAsync(table, 2);

            await observador.JoinTableAsync(table);
            await TableTestHost.WaitForAsync(
                () => observador.Snapshot?.Seats[2].PlayerName == "Fugado", "a Ana sentada");

            await ana.DisposeAsync();

            // Pasada la gracia de 2 s, el asiento se libera en el siguiente
            // ciclo de apuestas vacío.
            await TableTestHost.WaitForAsync(
                () => observador.Snapshot!.Seats[2].PlayerName == null,
                "la liberación del asiento",
                timeoutMs: 20000);

            Assert.False(observador.Snapshot!.Seats[2].IsConnected);
        }

        [Fact]
        public async Task LiberadoElAsiento_OtroJugadorPuedeSentarse()
        {
            string table = NewTableId();
            AuthResponse auth = await _host.RegisterAsync("Fugado2");
            await using TestClient beto = await _host.ConnectNewPlayerAsync("Beto");

            TestClient ana = await _host.ConnectAsync(auth);
            await ana.JoinTableAsync(table);
            await ana.SitAsync(table, 4);
            await beto.JoinTableAsync(table);
            await TableTestHost.WaitForAsync(
                () => beto.Snapshot?.Seats[4].PlayerName == "Fugado2", "a Ana sentada");

            await ana.DisposeAsync();

            await TableTestHost.WaitForAsync(
                () => beto.Snapshot!.Seats[4].PlayerName == null,
                "la liberación del asiento",
                timeoutMs: 20000);

            await beto.SitAsync(table, 4);

            await TableTestHost.WaitForAsync(
                () => beto.Snapshot!.Seats[4].PlayerName == "Beto",
                "a Beto ocupando el asiento libre");

            Assert.Empty(beto.Rejections);
        }
    }
}
