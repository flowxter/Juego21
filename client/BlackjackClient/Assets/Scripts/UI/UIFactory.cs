using System;
using UnityEngine;
using UnityEngine.UI;

namespace Blackjack.Client.UI
{
    /// <summary>
    /// Atajos para montar la interfaz por código.
    ///
    /// La mesa se construye en tiempo de ejecución en vez de con prefabs
    /// guardados: los prefabs y las escenas de Unity son YAML con referencias
    /// por GUID, y mantenerlos a mano fuera del editor es una fuente
    /// inagotable de referencias rotas.
    /// </summary>
    public static class UIFactory
    {
        private static Font _font;

        /// <summary>
        /// Fuente incorporada de Unity. Se usa la legacy porque está siempre
        /// disponible; TextMeshPro necesita importar sus recursos antes de
        /// poder dibujar una sola letra.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static Image Panel(string name, Transform parent, Sprite sprite, Color? tint = null)
        {
            RectTransform rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = tint ?? Color.white;
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        public static Text Label(
            string name,
            Transform parent,
            string text,
            int size,
            Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter,
            FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = Rect(name, parent);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.fontStyle = style;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;

            // Por defecto llena a su padre. Un RectTransform recién creado se
            // ancla abajo a la izquierda con 100x100, así que sin esto toda
            // etiqueta a la que no se le dé posición explícita acaba en una
            // esquina. Place() lo sobrescribe cuando hace falta colocarla.
            Stretch(rect);

            return label;
        }

        public static Button Button(
            string name,
            Transform parent,
            string caption,
            Action onClick,
            int fontSize = 20)
        {
            RectTransform rect = Rect(name, parent);

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = SpriteFactory.RoundedRect(160, 52, 10, TableTheme.ButtonFill,
                new Color(1f, 1f, 1f, 0.18f), 2);
            image.type = Image.Type.Sliced;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            button.colors = colors;

            if (onClick != null) button.onClick.AddListener(() => onClick());

            Text label = Label("Caption", rect, caption, fontSize, Color.white);
            Stretch(label.rectTransform);

            return button;
        }

        public static InputField Input(string name, Transform parent, string value, bool isPassword = false)
        {
            RectTransform rect = Rect(name, parent);

            var background = rect.gameObject.AddComponent<Image>();
            background.sprite = SpriteFactory.RoundedRect(240, 44, 8, new Color(1f, 1f, 1f, 0.10f),
                new Color(1f, 1f, 1f, 0.22f), 2);
            background.type = Image.Type.Sliced;

            RectTransform textRect = Rect("Text", rect);
            Stretch(textRect, 12f, 6f);
            var text = textRect.gameObject.AddComponent<Text>();
            text.font = Font;
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;

            var field = rect.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.text = value;
            field.contentType = isPassword ? InputField.ContentType.Password : InputField.ContentType.Standard;

            return field;
        }

        /// <summary>Estira el elemento para llenar a su padre.</summary>
        public static void Stretch(RectTransform rect, float paddingX = 0f, float paddingY = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(paddingX, paddingY);
            rect.offsetMax = new Vector2(-paddingX, -paddingY);
        }

        /// <summary>Coloca el elemento en un punto concreto con tamaño fijo.</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>Sombra proyectada, para dar peso a cartas y fichas.</summary>
        public static Shadow AddShadow(Graphic graphic, Vector2 offset, float alpha = 0.35f)
        {
            var shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, alpha);
            shadow.effectDistance = offset;
            return shadow;
        }
    }
}
