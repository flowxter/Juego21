using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Blackjack.Core.Rules;
using Blackjack.Protocol;
using Blackjack.Protocol.Dtos;
using Microsoft.AspNetCore.SignalR.Client;
using UnityEngine;

namespace Blackjack.Client.Net
{
    /// <summary>
    /// Conexión con la mesa.
    ///
    /// Envuelve SignalR y traslada todo al hilo principal antes de avisar, de
    /// forma que quien se suscriba pueda tocar la escena sin preocuparse de
    /// hilos. El cliente no decide nada de juego: manda intenciones y pinta lo
    /// que el servidor responde.
    /// </summary>
    public sealed class GameConnection : IAsyncDisposable
    {
        private readonly ServerConfig _config;
        private readonly string _token;
        private HubConnection _hub;

        public GameConnection(ServerConfig config, string token)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _token = token ?? throw new ArgumentNullException(nameof(token));
        }

        /// <summary>Estado completo de la mesa. Llega al entrar y al reconectar.</summary>
        public event Action<TableSnapshot> SnapshotReceived;

        /// <summary>Hechos de la ronda en orden, para reproducir como animación.</summary>
        public event Action<List<RoundEventDto>> RoundEventsReceived;

        public event Action<decimal> BalanceChanged;

        /// <summary>Comando rechazado por el servidor, con el motivo.</summary>
        public event Action<string> CommandRejected;

        /// <summary>
        /// Asientos que ocupas ahora mismo. Llega aparte del snapshot, que se
        /// difunde igual a toda la mesa y no puede decir cuáles son tuyos.
        /// </summary>
        public event Action<List<int>> SeatsChanged;

        public event Action<string> ConnectionLost;

        public event Action Reconnected;

        public bool IsConnected => _hub != null && _hub.State == HubConnectionState.Connected;

        public async Task<bool> ConnectAsync()
        {
            MainThreadDispatcher.EnsureExists();

            _hub = new HubConnectionBuilder()
                .WithUrl(_config.HubUrl, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(_token);
                })
                // Reintentos escalonados. El servidor guarda el asiento 60 s,
                // así que merece la pena insistir un rato antes de rendirse.
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(20)
                })
                .Build();

            RegisterHandlers();

            try
            {
                await _hub.StartAsync();
                await _hub.InvokeAsync(HubMethods.Client.JoinTable, _config.TableId);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("No se pudo conectar con la mesa: " + ex.Message);
                MainThreadDispatcher.Enqueue(() => ConnectionLost?.Invoke(ex.Message));
                return false;
            }
        }

        private void RegisterHandlers()
        {
            _hub.On<TableSnapshot>(HubMethods.Server.Snapshot, snapshot =>
                MainThreadDispatcher.Enqueue(() => SnapshotReceived?.Invoke(snapshot)));

            _hub.On<List<RoundEventDto>>(HubMethods.Server.RoundEvents, events =>
                MainThreadDispatcher.Enqueue(() => RoundEventsReceived?.Invoke(events)));

            _hub.On<decimal>(HubMethods.Server.BalanceChanged, balance =>
                MainThreadDispatcher.Enqueue(() => BalanceChanged?.Invoke(balance)));

            _hub.On<CommandRejected>(HubMethods.Server.CommandRejected, rejection =>
                MainThreadDispatcher.Enqueue(() => CommandRejected?.Invoke(rejection.Reason)));

            _hub.On<List<int>>(HubMethods.Server.YourSeats, seats =>
                MainThreadDispatcher.Enqueue(() => SeatsChanged?.Invoke(seats)));

            _hub.Closed += error =>
            {
                MainThreadDispatcher.Enqueue(() =>
                    ConnectionLost?.Invoke(error?.Message ?? "Conexión cerrada."));
                return Task.CompletedTask;
            };

            _hub.Reconnected += _ =>
            {
                // Al volver hay que reentrar en la mesa: el grupo de SignalR se
                // pierde con la conexión anterior. El servidor devuelve el
                // asiento si la ventana de gracia no expiró.
                MainThreadDispatcher.Enqueue(() => Reconnected?.Invoke());
                return _hub.InvokeAsync(HubMethods.Client.JoinTable, _config.TableId);
            };
        }

        // ------------------------------------------------------------------
        // Comandos
        // ------------------------------------------------------------------

        public Task SitAsync(int seatIndex)
            => SendAsync(HubMethods.Client.Sit, _config.TableId, seatIndex);

        /// <summary>Deja un asiento concreto, o todos con -1.</summary>
        public Task StandUpAsync(int seatIndex = -1)
            => SendAsync(HubMethods.Client.StandUp, _config.TableId, seatIndex);

        public Task PlaceBetAsync(int seatIndex, decimal main,
            decimal perfectPairs = 0m, decimal twentyOnePlus3 = 0m)
            => SendAsync(HubMethods.Client.PlaceBet, _config.TableId, seatIndex, new PlaceBetRequest
            {
                MainBet = main,
                PerfectPairsBet = perfectPairs,
                TwentyOnePlus3Bet = twentyOnePlus3
            });

        /// <summary>
        /// "Ya he apostado, podemos empezar." Si lo dicen todos los que juegan
        /// la ronda, el servidor reparte sin esperar al reloj.
        /// </summary>
        public Task ReadyAsync()
            => SendAsync(HubMethods.Client.Ready, _config.TableId);

        public Task ActAsync(PlayerAction action)
            => SendAsync(HubMethods.Client.Act, _config.TableId, new ActionRequest { Action = action });

        /// <summary>Responde al seguro en un asiento, o en todos con -1.</summary>
        public Task RespondInsuranceAsync(bool take, decimal amount = 0m, int seatIndex = -1)
            => SendAsync(HubMethods.Client.RespondInsurance, _config.TableId, seatIndex, new InsuranceRequest
            {
                Take = take,
                Amount = amount
            });

        private async Task SendAsync(string method, params object[] args)
        {
            if (!IsConnected)
            {
                MainThreadDispatcher.Enqueue(() => CommandRejected?.Invoke("Sin conexión con la mesa."));
                return;
            }

            try
            {
                await _hub.InvokeCoreAsync(method, args);
            }
            catch (Exception ex)
            {
                // Un comando que no llega no debe tumbar el cliente: el
                // servidor es la autoridad y el siguiente snapshot corrige.
                Debug.LogWarning($"Falló el comando {method}: {ex.Message}");
                MainThreadDispatcher.Enqueue(() => CommandRejected?.Invoke(ex.Message));
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_hub == null) return;

            try { await _hub.StopAsync(); }
            catch (Exception ex) { Debug.LogWarning("Error al cerrar la conexión: " + ex.Message); }

            await _hub.DisposeAsync();
            _hub = null;
        }
    }
}
