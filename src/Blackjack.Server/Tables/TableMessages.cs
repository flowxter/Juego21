using System;
using Blackjack.Core.Rules;
using Blackjack.Protocol.Dtos;

namespace Blackjack.Server.Tables
{
    /// <summary>
    /// Mensajes que entran al buzón de una mesa.
    ///
    /// Todo lo que quiera tocar el estado de la mesa pasa por aquí, incluidos
    /// los vencimientos de temporizador. Así el estado lo modifica un único
    /// hilo y no hace falta un solo lock en la lógica de juego.
    ///
    /// El identificador es el Guid de Identity, no un texto que venga del
    /// cliente: el hub lo saca del token ya validado.
    /// </summary>
    public abstract record TableMessage(Guid PlayerId);

    /// <summary>Entra a mirar la mesa. Recibe un snapshot; no ocupa asiento.</summary>
    public sealed record JoinMessage(Guid PlayerId, string PlayerName) : TableMessage(PlayerId);

    public sealed record LeaveMessage(Guid PlayerId) : TableMessage(PlayerId);

    public sealed record SitMessage(Guid PlayerId, string PlayerName, int SeatIndex) : TableMessage(PlayerId);

    /// <summary>
    /// Deja un asiento. Lleva índice porque un jugador puede ocupar varios y
    /// hay que saber de cuál se levanta; con -1 los deja todos.
    /// </summary>
    public sealed record StandUpMessage(Guid PlayerId, int SeatIndex = -1) : TableMessage(PlayerId);

    public sealed record PlaceBetMessage(Guid PlayerId, int SeatIndex, PlaceBetRequest Request)
        : TableMessage(PlayerId);

    /// <summary>
    /// El jugador no quiere esperar más. Si lo dicen todos los que apostaron,
    /// la mesa reparte sin agotar la cuenta atrás.
    /// </summary>
    public sealed record ReadyMessage(Guid PlayerId) : TableMessage(PlayerId);

    /// <summary>
    /// Respuesta al seguro para un asiento concreto. Con -1 responde lo mismo
    /// en todos los asientos del jugador.
    /// </summary>
    public sealed record InsuranceMessage(Guid PlayerId, int SeatIndex, InsuranceRequest Request)
        : TableMessage(PlayerId);

    public sealed record ActMessage(Guid PlayerId, PlayerAction Action) : TableMessage(PlayerId);

    /// <summary>
    /// Se cayó la conexión. No libera el asiento: arranca la ventana de
    /// reconexión.
    /// </summary>
    public sealed record DisconnectedMessage(Guid PlayerId) : TableMessage(PlayerId);
}
