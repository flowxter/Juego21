using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Blackjack.Protocol.Dtos;
using Blackjack.Server.Tables;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Blackjack.Server.Hubs
{
    /// <summary>
    /// Puerta de entrada de los clientes.
    ///
    /// El hub no decide nada de juego: valida lo mínimo, traduce a mensajes y
    /// los encola en la mesa correspondiente. Toda la autoridad vive en
    /// <see cref="TableActor"/>, que los procesa de uno en uno.
    ///
    /// La identidad sale del token JWT ya validado, nunca de un parámetro que
    /// mande el cliente: eso es lo que impide suplantar a otro jugador.
    /// </summary>
    [Authorize]
    public sealed class GameHub : Hub
    {
        private const string CurrentTableKey = "table";

        private readonly TableManager _tables;

        public GameHub(TableManager tables)
        {
            _tables = tables;
        }

        private Guid PlayerId
        {
            get
            {
                string? id = Context.UserIdentifier;

                if (!Guid.TryParse(id, out Guid playerId))
                    throw new HubException("El token no identifica a ningún jugador.");

                return playerId;
            }
        }

        private string PlayerName
        {
            get
            {
                string? name = Context.User?.FindFirstValue(ClaimTypes.Name);
                return string.IsNullOrWhiteSpace(name) ? "Jugador" : name!;
            }
        }

        public override async Task OnConnectedAsync()
        {
            // Un grupo por jugador permite hablarle a él aunque cambie de
            // conexión, que es lo que hace posible reconectar sin perder nada.
            await Groups.AddToGroupAsync(Context.ConnectionId, TableActor.PlayerGroup(PlayerId));
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, TableActor.PlayerGroup(PlayerId));

            if (Context.Items.TryGetValue(CurrentTableKey, out object? tableId)
                && tableId is string id
                && _tables.TryGet(id, out TableActor? table))
            {
                // No se le echa de la mesa: arranca su ventana de reconexión.
                table!.Post(new DisconnectedMessage(PlayerId));
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinTable(string tableId)
        {
            TableActor table = _tables.GetOrCreate(tableId);

            await Groups.AddToGroupAsync(Context.ConnectionId, TableActor.TableGroup(tableId));
            Context.Items[CurrentTableKey] = tableId;

            table.Post(new JoinMessage(PlayerId, PlayerName));
        }

        public async Task LeaveTable(string tableId)
        {
            if (_tables.TryGet(tableId, out TableActor? table))
            {
                table!.Post(new LeaveMessage(PlayerId));
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, TableActor.TableGroup(tableId));
            Context.Items.Remove(CurrentTableKey);
        }

        public void Sit(string tableId, int seatIndex)
            => Send(tableId, new SitMessage(PlayerId, PlayerName, seatIndex));

        /// <summary>Deja un asiento concreto, o todos si se pasa -1.</summary>
        public void StandUp(string tableId, int seatIndex = -1)
            => Send(tableId, new StandUpMessage(PlayerId, seatIndex));

        public void PlaceBet(string tableId, int seatIndex, PlaceBetRequest request)
            => Send(tableId, new PlaceBetMessage(PlayerId, seatIndex, request));

        public void Ready(string tableId)
            => Send(tableId, new ReadyMessage(PlayerId));

        /// <summary>Responde al seguro en un asiento, o en todos si se pasa -1.</summary>
        public void RespondInsurance(string tableId, int seatIndex, InsuranceRequest request)
            => Send(tableId, new InsuranceMessage(PlayerId, seatIndex, request));

        public void Act(string tableId, ActionRequest request)
            => Send(tableId, new ActMessage(PlayerId, request.Action));

        private void Send(string tableId, TableMessage message)
        {
            if (_tables.TryGet(tableId, out TableActor? table))
            {
                table!.Post(message);
            }
        }
    }
}
