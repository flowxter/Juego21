using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Blackjack.Core.Rules;
using Blackjack.Protocol;
using Blackjack.Protocol.Dtos;
using Microsoft.AspNetCore.SignalR.Client;

namespace Blackjack.Server.Tests
{
    /// <summary>
    /// Un jugador de prueba. Guarda todo lo que le manda el servidor para que
    /// los tests puedan afirmar sobre ello.
    ///
    /// Los callbacks de SignalR llegan en hilos del pool, así que todo acceso
    /// compartido va bajo lock: sin eso los tests fallarían de vez en cuando
    /// y por motivos que no tienen nada que ver con el juego.
    /// </summary>
    public sealed class TestClient : IAsyncDisposable
    {
        private readonly object _gate = new();
        private readonly List<RoundEventDto> _events = new();
        private readonly List<CommandRejected> _rejections = new();
        private readonly List<int> _mySeats = new();
        private TableSnapshot? _snapshot;
        private decimal _balance;

        public TestClient(AuthResponse auth, HubConnection connection)
        {
            Auth = auth;
            Connection = connection;
            _balance = auth.Balance;

            connection.On<TableSnapshot>(HubMethods.Server.Snapshot, snapshot =>
            {
                lock (_gate) _snapshot = snapshot;
            });

            connection.On<List<RoundEventDto>>(HubMethods.Server.RoundEvents, batch =>
            {
                lock (_gate) _events.AddRange(batch);
            });

            connection.On<decimal>(HubMethods.Server.BalanceChanged, balance =>
            {
                lock (_gate) _balance = balance;
            });

            connection.On<CommandRejected>(HubMethods.Server.CommandRejected, rejection =>
            {
                lock (_gate) _rejections.Add(rejection);
            });

            connection.On<List<int>>(HubMethods.Server.YourSeats, seats =>
            {
                lock (_gate)
                {
                    _mySeats.Clear();
                    _mySeats.AddRange(seats);
                }
            });
        }

        public AuthResponse Auth { get; }

        public Guid PlayerId => Auth.UserId;

        public string DisplayName => Auth.DisplayName;

        public HubConnection Connection { get; }

        public TableSnapshot? Snapshot
        {
            get { lock (_gate) return _snapshot; }
        }

        public decimal Balance
        {
            get { lock (_gate) return _balance; }
        }

        public IReadOnlyList<RoundEventDto> Events
        {
            get { lock (_gate) return _events.ToList(); }
        }

        public IReadOnlyList<CommandRejected> Rejections
        {
            get { lock (_gate) return _rejections.ToList(); }
        }

        /// <summary>Asientos que ocupa, según el propio servidor.</summary>
        public IReadOnlyList<int> MySeats
        {
            get { lock (_gate) return _mySeats.ToList(); }
        }

        public void ClearEvents()
        {
            lock (_gate) _events.Clear();
        }

        public Task JoinTableAsync(string tableId)
            => Connection.InvokeAsync(HubMethods.Client.JoinTable, tableId);

        /// <summary>
        /// Último asiento en el que se sentó. Los comandos que necesitan
        /// asiento lo usan por defecto, ya que un jugador puede ocupar varios.
        /// </summary>
        public int SeatIndex { get; private set; } = -1;

        public Task SitAsync(string tableId, int seatIndex)
        {
            SeatIndex = seatIndex;
            return Connection.InvokeAsync(HubMethods.Client.Sit, tableId, seatIndex);
        }

        public Task StandUpAsync(string tableId, int seatIndex = -1)
            => Connection.InvokeAsync(HubMethods.Client.StandUp, tableId, seatIndex);

        public Task PlaceBetAsync(string tableId, decimal main,
            decimal perfectPairs = 0m, decimal twentyOnePlus3 = 0m, int seatIndex = -1)
            => Connection.InvokeAsync(HubMethods.Client.PlaceBet, tableId,
                seatIndex >= 0 ? seatIndex : SeatIndex, new PlaceBetRequest
                {
                    MainBet = main,
                    PerfectPairsBet = perfectPairs,
                    TwentyOnePlus3Bet = twentyOnePlus3
                });

        public Task ActAsync(string tableId, PlayerAction action)
            => Connection.InvokeAsync(HubMethods.Client.Act, tableId, new ActionRequest { Action = action });

        public Task RespondInsuranceAsync(string tableId, bool take, decimal amount = 0m, int seatIndex = -1)
            => Connection.InvokeAsync(HubMethods.Client.RespondInsurance, tableId, seatIndex,
                new InsuranceRequest
                {
                    Take = take,
                    Amount = amount
                });

        public Task ReadyAsync(string tableId)
            => Connection.InvokeAsync(HubMethods.Client.Ready, tableId);

        public async ValueTask DisposeAsync()
        {
            await Connection.DisposeAsync();
        }
    }
}
