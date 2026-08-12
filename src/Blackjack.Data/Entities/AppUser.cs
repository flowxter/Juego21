using System;
using Microsoft.AspNetCore.Identity;

namespace Blackjack.Data.Entities
{
    /// <summary>
    /// Cuenta de jugador. Hereda de IdentityUser para no reinventar el hash
    /// de contraseñas, el bloqueo por intentos fallidos ni la confirmación de
    /// correo: escribir eso a mano es la forma más rápida de tener un agujero.
    /// </summary>
    public sealed class AppUser : IdentityUser<Guid>
    {
        /// <summary>Nombre visible en la mesa. Puede repetirse; el correo no.</summary>
        public string DisplayName { get; set; } = string.Empty;

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;

        public Account? Account { get; set; }

        public PlayerStats? Stats { get; set; }
    }
}
