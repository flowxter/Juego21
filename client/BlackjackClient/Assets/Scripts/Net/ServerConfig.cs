using System;
using UnityEngine;

namespace Blackjack.Client.Net
{
    /// <summary>
    /// Dónde vive el servidor. Se edita desde el inspector para poder apuntar
    /// a local o a un despliegue sin recompilar.
    /// </summary>
    [Serializable]
    public sealed class ServerConfig
    {
        [Tooltip("Raíz del servidor, sin barra final. Ej: http://localhost:5199")]
        [SerializeField] private string baseUrl = "http://localhost:5199";

        [Tooltip("Mesa a la que entrar al conectar.")]
        [SerializeField] private string tableId = "mesa-1";

        public string BaseUrl => baseUrl.TrimEnd('/');

        public string TableId => tableId;

        /// <summary>
        /// Cambia el servidor en caliente. Existe para poder apuntar a otro
        /// equipo desde la pantalla de acceso: en un ejecutable ya compilado no
        /// hay inspector donde tocarlo.
        /// </summary>
        public void SetBaseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            url = url.Trim().TrimEnd('/');

            // Sin esquema no se puede resolver; se asume http, que es lo que
            // sirve el servidor en desarrollo.
            if (!url.StartsWith("http://") && !url.StartsWith("https://")) url = "http://" + url;

            baseUrl = url;
        }

        public void SetTableId(string id)
        {
            if (!string.IsNullOrWhiteSpace(id)) tableId = id.Trim();
        }

        public string HubUrl => BaseUrl + "/hub/game";

        public string RegisterUrl => BaseUrl + "/api/auth/register";

        public string LoginUrl => BaseUrl + "/api/auth/login";

        public string ProfileUrl => BaseUrl + "/api/me";

        public string HistoryUrl => BaseUrl + "/api/me/history";
    }
}
