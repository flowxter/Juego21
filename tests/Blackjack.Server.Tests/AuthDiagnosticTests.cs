using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Blackjack.Protocol.Dtos;
using Xunit;
using Xunit.Abstractions;

namespace Blackjack.Server.Tests
{
    /// <summary>
    /// Comprueba que el token emitido al registrarse sirve tanto para la API
    /// REST como para abrir conexión con el hub.
    /// </summary>
    public sealed class AuthDiagnosticTests : IClassFixture<TableTestHost>
    {
        private readonly TableTestHost _host;
        private readonly ITestOutputHelper _output;

        public AuthDiagnosticTests(TableTestHost host, ITestOutputHelper output)
        {
            _host = host;
            _output = output;
        }

        [Fact]
        public async Task ElTokenDelRegistro_AbreLaApiYElHub()
        {
            AuthResponse auth = await _host.RegisterAsync("Diagnostico");

            using HttpClient http = _host.CreateClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

            HttpResponseMessage profile = await http.GetAsync("/api/me");
            _output.WriteLine($"/api/me -> {(int)profile.StatusCode}");

            HttpResponseMessage negotiate = await http.PostAsync("/hub/game/negotiate?negotiateVersion=1", null);
            _output.WriteLine($"/hub/game/negotiate -> {(int)negotiate.StatusCode}");

            if (negotiate.StatusCode == HttpStatusCode.Unauthorized)
            {
                // La cabecera WWW-Authenticate dice exactamente qué falló en la
                // validación del token (firma, emisor, caducidad...).
                _output.WriteLine("WWW-Authenticate: " + string.Join(" | ", negotiate.Headers.WwwAuthenticate));
            }

            Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
            Assert.Equal(HttpStatusCode.OK, negotiate.StatusCode);
        }
    }
}
