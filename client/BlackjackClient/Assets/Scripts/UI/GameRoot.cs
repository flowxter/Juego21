using System.Collections.Generic;
using System.Threading.Tasks;
using Blackjack.Client.Net;
using Blackjack.Core.Rules;
using Blackjack.Protocol.Dtos;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blackjack.Client.UI
{
    /// <summary>
    /// Punto de entrada del cliente: monta el lienzo, encadena acceso y mesa, y
    /// conecta la interfaz con el servidor.
    ///
    /// Es el único componente que hay que poner en la escena; todo lo demás se
    /// construye desde aquí.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class GameRoot : MonoBehaviour
    {
        [SerializeField] private ServerConfig server = new ServerConfig();

        [Header("Credenciales precargadas (solo comodidad al probar)")]
        [SerializeField] private string email = "jugador@test.local";
        [SerializeField] private string password = "Prueba-1234";
        [SerializeField] private string displayName = "Jugador";

        private Canvas _canvas;
        private LoginView _login;
        private TableView _table;
        private GameConnection _connection;

        private void Start()
        {
            MainThreadDispatcher.EnsureExists();

            // Se toca la instancia para que sintetice los clips y arranque la
            // música ya en la pantalla de acceso: generar el audio la primera
            // vez cuesta unos milisegundos y no conviene que caigan justo
            // cuando vuela la primera carta.
            _ = CasinoAudio.Instance;

            BuildCanvas();

            _login = LoginView.Create(_canvas.transform, email, password, displayName,
                server.BaseUrl, server.TableId);
            _login.Submitted += OnLoginSubmitted;
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);

            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Escalado por resolución: la mesa se diseñó para 1920x1080 y debe
            // encogerse entera, no recolocar sus piezas, para no romper el arco
            // de asientos.
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem));
                events.AddComponent<StandaloneInputModule>();
            }
        }

        // ------------------------------------------------------------------
        // Acceso
        // ------------------------------------------------------------------

        private async void OnLoginSubmitted(string mail, string pass, string name, bool register)
        {
            // El servidor y la mesa se toman de la pantalla de acceso, para
            // poder apuntar a otro equipo sin recompilar.
            server.SetBaseUrl(_login.ServerUrl);
            server.SetTableId(_login.TableId);

            ApiResult<AuthResponse> result = register
                ? await AuthApi.RegisterAsync(server, mail, pass, name)
                : await AuthApi.LoginAsync(server, mail, pass);

            if (!result.Ok)
            {
                _login.SetBusy(false);
                _login.SetStatus(result.Error);
                return;
            }

            displayName = result.Value.DisplayName;

            _login.SetStatus("Conectando con la mesa...");
            await EnterTableAsync(result.Value);
        }

        private async Task EnterTableAsync(AuthResponse auth)
        {
            _connection = new GameConnection(server, auth.Token);

            _connection.SnapshotReceived += OnSnapshot;
            _connection.BalanceChanged += OnBalance;
            _connection.RoundEventsReceived += OnRoundEvents;
            _connection.CommandRejected += OnRejected;
            _connection.ConnectionLost += OnConnectionLost;
            _connection.Reconnected += OnReconnected;

            bool ok = await _connection.ConnectAsync();

            if (!ok)
            {
                _login.SetBusy(false);
                _login.SetStatus("No se pudo conectar. ¿Está el servidor levantado?");
                return;
            }

            Destroy(_login.gameObject);
            _login = null;

            _table = TableView.Create(_canvas.transform, auth.DisplayName);
            _table.SetBalance(auth.Balance);

            _connection.SeatsChanged += seats => _table.SetMySeats(seats);

            _table.SitRequested += seat => Send(_connection.SitAsync(seat));
            _table.BetRequested += (seat, main, pairs, trio) =>
                Send(_connection.PlaceBetAsync(seat, main, pairs, trio));
            _table.StandUpRequested += () => Send(_connection.StandUpAsync());
            _table.StandUpSeatRequested += seat => Send(_connection.StandUpAsync(seat));
            _table.ActionRequested += action => Send(_connection.ActAsync(action));
            _table.InsuranceAnswered += OnInsuranceAnswered;
            _table.ReadyRequested += () => Send(_connection.ReadyAsync());
        }

        // ------------------------------------------------------------------
        // Mesa
        // ------------------------------------------------------------------

        private TableSnapshot _lastSnapshot;

        private void OnSnapshot(TableSnapshot snapshot)
        {
            _lastSnapshot = snapshot;
            _table?.ApplySnapshot(snapshot);
        }

        private void OnBalance(decimal balance) => _table?.SetBalance(balance);

        private void OnRoundEvents(List<RoundEventDto> events) => _table?.OnRoundEvents(events);

        private void OnRejected(string reason) => _table?.ShowMessage(reason);

        private void OnConnectionLost(string reason)
            => _table?.ShowMessage("Conexión perdida, reintentando...", 6f);

        private void OnReconnected()
            => _table?.ShowMessage("Reconectado. Tu asiento sigue ahí.", 3f);

        private void OnInsuranceAnswered(bool take)
        {
            // Con -1 el servidor responde lo mismo en todos tus asientos, que
            // es lo que se espera al pulsar un único botón.
            if (!take)
            {
                Send(_connection.RespondInsuranceAsync(false));
                return;
            }

            // El seguro es la mitad de la apuesta de cada mano. Se manda asiento
            // por asiento porque el importe puede diferir entre ellos.
            if (_lastSnapshot == null) return;

            foreach (int index in _table.MySeats)
            {
                if (index < 0 || index >= _lastSnapshot.Seats.Count) continue;

                decimal half = _lastSnapshot.Seats[index].MainBet * 0.5m;
                Send(_connection.RespondInsuranceAsync(true, half, index));
            }
        }

        private static async void Send(Task command)
        {
            await command;
        }

        private async void OnDestroy()
        {
            if (_connection != null) await _connection.DisposeAsync();
        }
    }
}
