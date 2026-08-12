using System;
using UnityEngine;
using UnityEngine.UI;

namespace Blackjack.Client.UI
{
    /// <summary>
    /// Pantalla de acceso sobre el tapete.
    /// </summary>
    public sealed class LoginView : MonoBehaviour
    {
        private InputField _email;
        private InputField _password;
        private InputField _displayName;
        private InputField _server;
        private InputField _table;
        private Text _status;
        private Button _login;
        private Button _register;

        /// <summary>Se dispara con (correo, contraseña, nombre, esRegistro).</summary>
        public event Action<string, string, string, bool> Submitted;

        /// <summary>Servidor y mesa elegidos, para poder jugar contra otro equipo.</summary>
        public string ServerUrl => _server != null ? _server.text : string.Empty;

        public string TableId => _table != null ? _table.text : string.Empty;

        public static LoginView Create(Transform canvas, string email, string password,
            string displayName, string serverUrl, string tableId)
        {
            RectTransform rect = UIFactory.Rect("Login", canvas);
            UIFactory.Stretch(rect);

            var view = rect.gameObject.AddComponent<LoginView>();
            view.Build(email, password, displayName, serverUrl, tableId);
            return view;
        }

        private void Build(string email, string password, string displayName,
            string serverUrl, string tableId)
        {
            var root = (RectTransform)transform;

            Image backdrop = UIFactory.Panel("Backdrop", root, SpriteFactory.Felt());
            UIFactory.Stretch(backdrop.rectTransform);

            Image card = UIFactory.Panel("Card", root,
                SpriteFactory.RoundedRect(480, 620, 18, new Color(0.05f, 0.06f, 0.08f, 0.94f),
                    new Color(1f, 1f, 1f, 0.12f), 2));
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480f, 620f));
            UIFactory.AddShadow(card, new Vector2(0f, -6f), 0.5f);

            Text title = UIFactory.Label("Title", card.rectTransform, "BLACKJACK", 34,
                TableTheme.GoldText, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(400f, 44f));

            Text subtitle = UIFactory.Label("Subtitle", card.rectTransform, "Mesa multijugador", 16,
                new Color(1f, 1f, 1f, 0.6f));
            UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(400f, 24f));

            _email = AddField(card.rectTransform, "Correo", email, -140f, false);
            _password = AddField(card.rectTransform, "Contraseña", password, -212f, true);
            _displayName = AddField(card.rectTransform, "Nombre en la mesa", displayName, -284f, false);

            // Servidor y mesa configurables: es lo que permite que varias
            // personas jueguen juntas sin recompilar el cliente.
            _server = AddField(card.rectTransform, "Servidor", serverUrl, -368f, false);
            _table = AddField(card.rectTransform, "Mesa", tableId, -440f, false);

            _login = UIFactory.Button("Login", card.rectTransform, "Entrar", () => Submit(false));
            UIFactory.Place(_login.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                new Vector2(-105f, -524f), new Vector2(190f, 52f));

            _register = UIFactory.Button("Register", card.rectTransform, "Crear cuenta", () => Submit(true), 18);
            UIFactory.Place(_register.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                new Vector2(105f, -524f), new Vector2(190f, 52f));

            _status = UIFactory.Label("Status", card.rectTransform, string.Empty, 15,
                TableTheme.LoseRed, TextAnchor.MiddleCenter);
            UIFactory.Place(_status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -578f),
                new Vector2(440f, 40f));
        }

        private static InputField AddField(Transform parent, string caption, string value, float y, bool secret)
        {
            Text label = UIFactory.Label(caption + "Label", parent, caption, 14,
                new Color(1f, 1f, 1f, 0.65f), TextAnchor.MiddleLeft);
            UIFactory.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y + 26f),
                new Vector2(360f, 20f));

            InputField field = UIFactory.Input(caption, parent, value, secret);
            UIFactory.Place(field.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                new Vector2(0f, y), new Vector2(360f, 44f));

            return field;
        }

        private void Submit(bool register)
        {
            if (string.IsNullOrWhiteSpace(_email.text) || string.IsNullOrWhiteSpace(_password.text))
            {
                SetStatus("Hacen falta correo y contraseña.");
                return;
            }

            SetBusy(true);
            SetStatus(register ? "Creando cuenta..." : "Entrando...");

            Submitted?.Invoke(_email.text.Trim(), _password.text,
                string.IsNullOrWhiteSpace(_displayName.text) ? "Jugador" : _displayName.text.Trim(), register);
        }

        public void SetStatus(string message) => _status.text = message;

        public void SetBusy(bool busy)
        {
            _login.interactable = !busy;
            _register.interactable = !busy;
        }
    }
}
