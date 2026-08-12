using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Blackjack.Core.Rounds;
using Blackjack.Core.Rules;
using Blackjack.Protocol;
using Blackjack.Protocol.Dtos;
using Xunit;

namespace Blackjack.Server.Tests
{
    /// <summary>
    /// Comprueba que lo jugado queda guardado: saldo, historial de manos y
    /// estadísticas sobreviven a la partida y se pueden consultar después.
    /// </summary>
    public sealed class PersistenceIntegrationTests : IClassFixture<TableTestHost>
    {
        private readonly TableTestHost _host;

        public PersistenceIntegrationTests(TableTestHost host)
        {
            _host = host;
        }

        private static string NewTableId() => "mesa-" + Guid.NewGuid().ToString("N")[..8];

        // ------------------------------------------------------------------
        // Cuentas
        // ------------------------------------------------------------------

        [Fact]
        public async Task RegistrarseCreaCuentaConSaldoInicial()
        {
            AuthResponse auth = await _host.RegisterAsync("Nuevo");

            Assert.NotEqual(Guid.Empty, auth.UserId);
            Assert.Equal(1000m, auth.Balance);
            Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        }

        [Fact]
        public async Task ElMismoCorreo_NoSePuedeRegistrarDosVeces()
        {
            using HttpClient http = _host.CreateClient();

            var request = new RegisterRequest
            {
                Email = $"{Guid.NewGuid():N}@test.local",
                Password = "Prueba-1234",
                DisplayName = "Duplicado"
            };

            HttpResponseMessage first = await http.PostAsJsonAsync("/api/auth/register", request);
            HttpResponseMessage second = await http.PostAsJsonAsync("/api/auth/register", request);

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        }

        [Fact]
        public async Task ContrasenaIncorrecta_NoIniciaSesion()
        {
            AuthResponse auth = await _host.RegisterAsync("Alguien");
            using HttpClient http = _host.CreateClient();

            ProfileResponse? profile = await _host.CreateAuthedClient(auth)
                .GetFromJsonAsync<ProfileResponse>("/api/me");

            HttpResponseMessage bad = await http.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Email = profile!.Email,
                Password = "no-es-la-buena"
            });

            Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        }

        [Fact]
        public async Task ElPerfil_DevuelveSaldoYEstadisticas()
        {
            AuthResponse auth = await _host.RegisterAsync("Perfilado");
            using HttpClient http = _host.CreateAuthedClient(auth);

            ProfileResponse? profile = await http.GetFromJsonAsync<ProfileResponse>("/api/me");

            Assert.NotNull(profile);
            Assert.Equal(auth.UserId, profile!.UserId);
            Assert.Equal("Perfilado", profile.DisplayName);
            Assert.Equal(1000m, profile.Balance);
            Assert.Equal(0, profile.Stats.RoundsPlayed);
        }

        [Fact]
        public async Task SinToken_LaApiPrivadaRechaza()
        {
            using HttpClient http = _host.CreateClient();

            HttpResponseMessage response = await http.GetAsync("/api/me");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // ------------------------------------------------------------------
        // Partidas guardadas
        // ------------------------------------------------------------------

        [Fact]
        public async Task TrasJugarUnaRonda_QuedaGuardadaEnElHistorial()
        {
            string table = NewTableId();
            AuthResponse auth = await _host.RegisterAsync("Historiado");
            await using TestClient player = await _host.ConnectAsync(auth);

            await PlayOneRoundAsync(player, table, bet: 100m);

            using HttpClient http = _host.CreateAuthedClient(auth);
            List<RoundHistoryDto>? history = await http.GetFromJsonAsync<List<RoundHistoryDto>>("/api/me/history");

            Assert.NotNull(history);
            RoundHistoryDto round = Assert.Single(history!);

            Assert.Equal(table, round.TableId);
            Assert.Equal(100m, round.MainBet);
            Assert.NotEmpty(round.Hands);

            // Las cartas se guardan legibles: "As,Kh", no ids numéricos.
            Assert.Matches("^[2-9TJQKA][cdhs](,[2-9TJQKA][cdhs])*$", round.Hands[0].Cards);
            Assert.Matches("^[2-9TJQKA][cdhs](,[2-9TJQKA][cdhs])*$", round.DealerCards);

            // El croupier siempre acaba con al menos 17, o pasado.
            Assert.True(round.DealerTotal >= 17, $"El croupier se quedó en {round.DealerTotal}.");
        }

        [Fact]
        public async Task LasEstadisticas_SeActualizanAlJugar()
        {
            string table = NewTableId();
            AuthResponse auth = await _host.RegisterAsync("Estadistico");
            await using TestClient player = await _host.ConnectAsync(auth);

            await PlayOneRoundAsync(player, table, bet: 50m);

            using HttpClient http = _host.CreateAuthedClient(auth);
            ProfileResponse? profile = await http.GetFromJsonAsync<ProfileResponse>("/api/me");

            PlayerStatsDto stats = profile!.Stats;

            Assert.Equal(1, stats.RoundsPlayed);
            Assert.True(stats.HandsPlayed >= 1);
            Assert.Equal(50m, stats.TotalWagered);

            // Cada mano acabó en algún estado: la cuenta tiene que cuadrar.
            int resolved = stats.HandsWon + stats.HandsLost + stats.HandsPushed + stats.HandsSurrendered;
            Assert.Equal(stats.HandsPlayed, resolved);
        }

        [Fact]
        public async Task ElSaldoSobrevive_AlVolverAIniciarSesion()
        {
            string table = NewTableId();
            AuthResponse auth = await _host.RegisterAsync("Persistente");

            await using (TestClient player = await _host.ConnectAsync(auth))
            {
                await PlayOneRoundAsync(player, table, bet: 100m);
            }

            // Nueva sesión, mismo jugador: el saldo es el que dejó.
            using HttpClient http = _host.CreateAuthedClient(auth);
            ProfileResponse? profile = await http.GetFromJsonAsync<ProfileResponse>("/api/me");

            Assert.NotNull(profile);

            // Apostó 100: o los perdió, o cobró algo. Nunca sigue en 1000
            // salvo que empatara, así que se comprueba contra el historial.
            List<RoundHistoryDto>? history = await http.GetFromJsonAsync<List<RoundHistoryDto>>("/api/me/history");
            RoundHistoryDto round = Assert.Single(history!);

            Assert.Equal(1000m + round.NetProfit, profile!.Balance);
        }

        // ------------------------------------------------------------------
        // Integridad contable
        // ------------------------------------------------------------------

        [Fact]
        public async Task ElSaldo_CoincideConLaSumaDelLibro()
        {
            // Es LA comprobación del ledger: si el saldo cacheado y la suma de
            // movimientos divergen, hay fichas apareciendo o desapareciendo.
            string table = NewTableId();
            AuthResponse auth = await _host.RegisterAsync("Auditado");
            await using TestClient player = await _host.ConnectAsync(auth);

            await PlayOneRoundAsync(player, table, bet: 100m);

            using HttpClient http = _host.CreateAuthedClient(auth);

            ProfileResponse? profile = await http.GetFromJsonAsync<ProfileResponse>("/api/me");
            List<LedgerRow>? ledger = await http.GetFromJsonAsync<List<LedgerRow>>("/api/me/ledger?limit=200");

            Assert.NotNull(ledger);
            decimal sum = ledger!.Sum(e => e.Amount);

            Assert.Equal(profile!.Balance, sum);
        }

        [Fact]
        public async Task ElExtracto_RegistraElDepositoYLaApuesta()
        {
            string table = NewTableId();
            AuthResponse auth = await _host.RegisterAsync("Extractado");
            await using TestClient player = await _host.ConnectAsync(auth);

            await player.JoinTableAsync(table);
            await player.SitAsync(table, 0);
            await TableTestHost.WaitForAsync(
                () => player.Snapshot?.Phase == TablePhase.Betting, "la ventana de apuestas");
            await player.PlaceBetAsync(table, 100m);
            await TableTestHost.WaitForAsync(() => player.Balance == 900m, "el cargo de la apuesta");

            using HttpClient http = _host.CreateAuthedClient(auth);
            List<LedgerRow>? ledger = await http.GetFromJsonAsync<List<LedgerRow>>("/api/me/ledger");

            Assert.NotNull(ledger);

            // El más reciente primero: la apuesta de 100 y, al fondo, el
            // depósito inicial de la cuenta.
            Assert.Contains(ledger!, e => e.Amount == -100m);
            Assert.Contains(ledger!, e => e.Amount == 1000m);
        }

        // ------------------------------------------------------------------
        // Utilidades
        // ------------------------------------------------------------------

        /// <summary>
        /// Juega una ronda entera plantándose siempre, y espera a que quede
        /// liquidada. No depende de qué cartas salgan.
        /// </summary>
        private static async Task PlayOneRoundAsync(TestClient client, string table, decimal bet)
        {
            await client.JoinTableAsync(table);
            await client.SitAsync(table, 0);

            await TableTestHost.WaitForAsync(
                () => client.Snapshot?.Phase == TablePhase.Betting, "la ventana de apuestas");

            await client.PlaceBetAsync(table, bet);

            var deadline = DateTime.UtcNow.AddSeconds(25);

            while (DateTime.UtcNow < deadline)
            {
                if (client.Events.Any(e => e.Type == RoundEventType.RoundComplete))
                {
                    // Un respiro para que termine de escribirse el historial.
                    await Task.Delay(400);
                    return;
                }

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

        private sealed class LedgerRow
        {
            /// <summary>Se recibe como número: el enum viaja sin convertidor.</summary>
            public int Type { get; set; }

            public decimal Amount { get; set; }

            public decimal BalanceAfter { get; set; }

            public string? RoundId { get; set; }

            public DateTime CreatedUtc { get; set; }
        }
    }
}
