using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Blackjack.Protocol.Dtos;
using UnityEngine.Networking;

namespace Blackjack.Client.Net
{
    /// <summary>Resultado de una llamada a la API, sin excepciones de por medio.</summary>
    public readonly struct ApiResult<T>
    {
        private ApiResult(bool ok, T value, string error)
        {
            Ok = ok;
            Value = value;
            Error = error;
        }

        public bool Ok { get; }

        public T Value { get; }

        public string Error { get; }

        public static ApiResult<T> Success(T value) => new ApiResult<T>(true, value, null);

        public static ApiResult<T> Failure(string error) => new ApiResult<T>(false, default, error);
    }

    /// <summary>
    /// Registro e inicio de sesión contra la API REST.
    ///
    /// Usa System.Text.Json y no JsonUtility de Unity: los DTOs compartidos
    /// exponen propiedades, y JsonUtility solo serializa campos públicos, así
    /// que devolvería objetos vacíos sin avisar de nada.
    /// </summary>
    public static class AuthApi
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public static Task<ApiResult<AuthResponse>> RegisterAsync(
            ServerConfig config, string email, string password, string displayName)
        {
            var request = new RegisterRequest
            {
                Email = email,
                Password = password,
                DisplayName = displayName
            };

            return PostAsync<RegisterRequest, AuthResponse>(config.RegisterUrl, request, null);
        }

        public static Task<ApiResult<AuthResponse>> LoginAsync(
            ServerConfig config, string email, string password)
        {
            var request = new LoginRequest { Email = email, Password = password };

            return PostAsync<LoginRequest, AuthResponse>(config.LoginUrl, request, null);
        }

        public static Task<ApiResult<ProfileResponse>> GetProfileAsync(ServerConfig config, string token)
            => GetAsync<ProfileResponse>(config.ProfileUrl, token);

        private static async Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(
            string url, TRequest body, string token)
        {
            byte[] payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body, JsonOptions));

            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(payload);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);

            return await SendAsync<TResponse>(request);
        }

        private static async Task<ApiResult<TResponse>> GetAsync<TResponse>(string url, string token)
        {
            using var request = UnityWebRequest.Get(url);
            if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);

            return await SendAsync<TResponse>(request);
        }

        private static async Task<ApiResult<TResponse>> SendAsync<TResponse>(UnityWebRequest request)
        {
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            // UnityWebRequest no es awaitable de serie; se espera cediendo el
            // turno para no bloquear el hilo principal y congelar el juego.
            while (!operation.isDone) await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string detail = request.downloadHandler?.text;
                string message = string.IsNullOrWhiteSpace(detail) ? request.error : detail;

                if (request.responseCode == 401)
                    message = "Correo o contraseña incorrectos.";

                return ApiResult<TResponse>.Failure(message);
            }

            try
            {
                var value = JsonSerializer.Deserialize<TResponse>(request.downloadHandler.text, JsonOptions);
                return ApiResult<TResponse>.Success(value);
            }
            catch (Exception ex)
            {
                return ApiResult<TResponse>.Failure("Respuesta ilegible del servidor: " + ex.Message);
            }
        }
    }
}
