using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Blackjack.Protocol.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Blackjack.Server.Tests
{
    /// <summary>
    /// Levanta el servidor en memoria contra una base de datos de pruebas y
    /// con los temporizadores acelerados.
    ///
    /// La base es 'blackjack_test', separada de la de desarrollo: los tests
    /// crean usuarios y rondas continuamente y no deben ensuciar los datos con
    /// los que se juega a mano. EF la crea sola al aplicar las migraciones.
    ///
    /// Con los tiempos de producción (15 s de apuestas, 20 s por turno) un
    /// test de una ronda tardaría casi un minuto. Aquí se bajan a segundos:
    /// lo que se prueba es la máquina de estados, no la duración del reloj.
    /// </summary>
    public class TableTestHost : WebApplicationFactory<Program>
    {
        public const string TestConnectionString =
            "Host=localhost;Port=5433;Database=blackjack_test;Username=blackjack;Password=blackjack_dev";

        /// <summary>
        /// Ventana de reconexión de la mesa. Se sobrescribe en los tests que
        /// necesitan verla expirar sin esperar un minuto real.
        /// </summary>
        protected virtual int ReconnectWindowSeconds => 60;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);

            builder.ConfigureAppConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Blackjack"] = TestConnectionString,
                    ["Jwt:Key"] = "clave-de-pruebas-suficientemente-larga-para-hmac-sha256",
                    ["Jwt:Issuer"] = "blackjack-server",
                    ["Jwt:Audience"] = "blackjack-client",

                    ["Table:SeatCount"] = "5",
                    ["Table:BettingSeconds"] = "3",
                    ["Table:InsuranceSeconds"] = "2",
                    ["Table:TurnSeconds"] = "3",
                    ["Table:PayoutSeconds"] = "1",
                    ["Table:ReconnectWindowSeconds"] = ReconnectWindowSeconds.ToString()
                });
            });
        }

        /// <summary>
        /// Da de alta un jugador nuevo y devuelve su sesión. Cada test usa
        /// correos únicos para no pisarse el saldo con los demás.
        /// </summary>
        public async Task<AuthResponse> RegisterAsync(string displayName)
        {
            using HttpClient http = CreateClient();

            var request = new RegisterRequest
            {
                Email = $"{Guid.NewGuid():N}@test.local",
                Password = "Prueba-1234",
                DisplayName = displayName
            };

            HttpResponseMessage response = await http.PostAsJsonAsync("/api/auth/register", request);
            response.EnsureSuccessStatusCode();

            AuthResponse? auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            return auth ?? throw new InvalidOperationException("El registro no devolvió sesión.");
        }

        /// <summary>
        /// Cliente SignalR autenticado.
        ///
        /// Se usa AccessTokenProvider en vez de meter el token en la query
        /// string: el cliente .NET lo envía como cabecera Authorization, que
        /// es lo correcto fuera del navegador. La query string solo hace falta
        /// para WebSockets desde el navegador, y el servidor la sigue
        /// aceptando para ese caso.
        ///
        /// Se fuerza long polling porque es el transporte que TestServer
        /// soporta sin montar WebSockets, y a un juego por turnos le da igual.
        /// </summary>
        public async Task<TestClient> ConnectAsync(AuthResponse auth)
        {
            var url = new Uri(Server.BaseAddress, "hub/game");

            HubConnection connection = new HubConnectionBuilder()
                .WithUrl(url, options =>
                {
                    options.Transports = HttpTransportType.LongPolling;

                    // La cabecera va explícita, no por AccessTokenProvider: al
                    // sustituir el handler por el de TestServer se descarta la
                    // cadena de SignalR, y con ella el handler que inyecta el
                    // token. Así el Authorization viaja seguro.
                    options.Headers["Authorization"] = "Bearer " + auth.Token;
                    options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                })
                .Build();

            var client = new TestClient(auth, connection);
            await connection.StartAsync();
            return client;
        }

        /// <summary>Cliente REST con la sesión ya puesta en la cabecera.</summary>
        public HttpClient CreateAuthedClient(AuthResponse auth)
        {
            HttpClient http = CreateClient();
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Token);
            return http;
        }

        /// <summary>Alta y conexión en un solo paso, que es lo habitual.</summary>
        public async Task<TestClient> ConnectNewPlayerAsync(string displayName)
            => await ConnectAsync(await RegisterAsync(displayName));

        /// <summary>
        /// Espera a que se cumpla una condición, sondeando. Los tests son
        /// asíncronos por naturaleza: la mesa avanza sola por temporizador.
        /// </summary>
        public static async Task WaitForAsync(Func<bool> condition, string description, int timeoutMs = 15000)
        {
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (condition()) return;
                await Task.Delay(25);
            }

            throw new TimeoutException($"Se agotó la espera de: {description}");
        }
    }
}
