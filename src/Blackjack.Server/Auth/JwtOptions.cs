namespace Blackjack.Server.Auth
{
    /// <summary>
    /// Configuración de los tokens de sesión.
    ///
    /// La clave NO debe vivir en appsettings.json fuera de desarrollo: va por
    /// variable de entorno o gestor de secretos. Quien tenga esta clave puede
    /// firmar tokens de cualquier jugador.
    /// </summary>
    public sealed class JwtOptions
    {
        public const string SectionName = "Jwt";

        public string Issuer { get; set; } = "blackjack-server";

        public string Audience { get; set; } = "blackjack-client";

        /// <summary>Mínimo 32 caracteres: HMAC-SHA256 rechaza claves cortas.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Duración del token. Larga a propósito: una sesión que caduca a
        /// media partida y desconecta al jugador de la mesa es peor que el
        /// riesgo que evita.
        /// </summary>
        public int ExpiryMinutes { get; set; } = 720;
    }
}
