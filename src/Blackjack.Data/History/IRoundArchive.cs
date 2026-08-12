using System;
using System.Threading;
using System.Threading.Tasks;
using CoreHand = Blackjack.Core.Hands.Hand;
using CoreSeat = Blackjack.Core.Rounds.Seat;

namespace Blackjack.Data.History
{
    /// <summary>
    /// Guarda lo jugado.
    ///
    /// Recibe los tipos del motor directamente en vez de un DTO intermedio:
    /// Blackjack.Data ya referencia a Blackjack.Core, y una capa de traducción
    /// extra solo sería sitio donde perder un campo al copiarlo.
    /// </summary>
    public interface IRoundArchive
    {
        /// <summary>
        /// Registra la ronda de un jugador y actualiza sus estadísticas.
        ///
        /// Se llama después de liquidar. Si falla no debe tumbar la mesa: el
        /// historial es importante, pero menos que seguir jugando.
        /// </summary>
        Task ArchiveAsync(
            string roundId,
            string tableId,
            Guid userId,
            CoreSeat seat,
            CoreHand dealerHand,
            CancellationToken ct = default);
    }
}
