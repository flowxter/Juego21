using System.Collections;
using Blackjack.Core.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace Blackjack.Client.UI
{
    /// <summary>
    /// Una carta sobre la mesa.
    ///
    /// El reparto sale del zapato con una desaceleración y un giro leve: en
    /// una mesa real ninguna carta cae perfectamente recta, pero pasarse de
    /// giro hace que el abanico parezca desordenado en vez de natural.
    /// </summary>
    public sealed class CardView : MonoBehaviour
    {
        private RectTransform _rect;
        private Image _background;

        private Text _cornerRank;
        private Text _cornerSuit;
        private RectTransform _cornerBottom;
        private Text _cornerRankBottom;
        private Text _cornerSuitBottom;

        private Text _centerSuit;
        private Text _centerRank;

        private bool _faceDown;
        private Coroutine _running;

        public RectTransform Rect => _rect;

        /// <summary>
        /// True mientras muestre el dorso. Lo consulta la mesa para saber si al
        /// llegar el valor hay que voltearla o simplemente pintarla.
        /// </summary>
        public bool IsFaceDown => _faceDown;

        public static CardView Create(Transform parent, string name = "Card")
        {
            RectTransform rect = UIFactory.Rect(name, parent);
            rect.sizeDelta = new Vector2(TableTheme.CardWidth, TableTheme.CardHeight);

            var view = rect.gameObject.AddComponent<CardView>();
            view.Build();
            return view;
        }

        private void Build()
        {
            _rect = (RectTransform)transform;

            _background = _rect.gameObject.AddComponent<Image>();
            _background.sprite = FaceSprite();
            _background.type = Image.Type.Sliced;
            _background.raycastTarget = false;

            // La sombra da la sensación de que la carta está apoyada y no
            // pegada al tapete.
            UIFactory.AddShadow(_background, new Vector2(3f, -4f), 0.42f);

            // Esquina superior izquierda: figura arriba, palo justo debajo, que
            // es la zona que queda visible cuando las cartas se solapan.
            //
            // El contenedor se mete bien hacia dentro y los textos se ajustan
            // en vez de desbordar: con "10" a cuerpo grande, un rect estrecho
            // deja el número asomando fuera de la carta.
            RectTransform topCorner = UIFactory.Rect("CornerTop", _rect);
            UIFactory.Place(topCorner, new Vector2(0f, 1f), new Vector2(21f, -36f), new Vector2(32f, 60f));

            _cornerRank = MakeCornerText(topCorner, "Rank", 20, -15f);
            _cornerSuit = MakeCornerText(topCorner, "Suit", 17, -41f);

            // Palo grande al centro.
            _centerSuit = UIFactory.Label("CenterSuit", _rect, string.Empty, 54,
                TableTheme.SuitBlack, TextAnchor.MiddleCenter);
            UIFactory.Place(_centerSuit.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -6f),
                new Vector2(80f, 80f));

            // Las figuras y el as llevan además su letra, que es lo que se
            // reconoce de un vistazo.
            _centerRank = UIFactory.Label("CenterRank", _rect, string.Empty, 46,
                TableTheme.SuitBlack, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(_centerRank.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 16f),
                new Vector2(80f, 60f));

            // Esquina inferior derecha: la misma marca girada, como una carta real.
            _cornerBottom = UIFactory.Rect("CornerBottom", _rect);
            UIFactory.Place(_cornerBottom, new Vector2(1f, 0f), new Vector2(-21f, 36f), new Vector2(32f, 60f));
            _cornerBottom.localRotation = Quaternion.Euler(0f, 0f, 180f);

            _cornerRankBottom = MakeCornerText(_cornerBottom, "Rank", 20, -15f);
            _cornerSuitBottom = MakeCornerText(_cornerBottom, "Suit", 17, -41f);
        }

        /// <summary>
        /// Texto de esquina que se encoge si no cabe, en vez de desbordar la
        /// carta. Es lo que hacía que el "10" se saliera por el borde.
        /// </summary>
        private static Text MakeCornerText(RectTransform parent, string name, int size, float y)
        {
            Text text = UIFactory.Label(name, parent, string.Empty, size,
                TableTheme.SuitBlack, TextAnchor.MiddleCenter, FontStyle.Bold);

            UIFactory.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y),
                new Vector2(32f, size + 6f));

            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = size;

            return text;
        }

        private static Sprite FaceSprite()
            => SpriteFactory.RoundedRect((int)TableTheme.CardWidth, (int)TableTheme.CardHeight, 10,
                TableTheme.CardFace, new Color(0f, 0f, 0f, 0.16f), 2);

        public void SetCard(Card card)
        {
            _faceDown = false;

            string rank = card.Rank.ShortName();
            string suit = card.Suit.Symbol();
            Color color = card.Color == CardColor.Red ? TableTheme.SuitRed : TableTheme.SuitBlack;

            _background.sprite = FaceSprite();

            _cornerRank.text = rank;
            _cornerSuit.text = suit;
            _cornerRankBottom.text = rank;
            _cornerSuitBottom.text = suit;

            bool isCourt = card.Rank == Rank.Jack || card.Rank == Rank.Queen
                        || card.Rank == Rank.King || card.Rank == Rank.Ace;

            if (isCourt)
            {
                // Letra grande arriba y palo más pequeño debajo.
                _centerRank.text = rank;
                _centerSuit.text = suit;
                _centerSuit.fontSize = 30;
                _centerSuit.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            }
            else
            {
                _centerRank.text = string.Empty;
                _centerSuit.text = suit;
                _centerSuit.fontSize = 54;
                _centerSuit.rectTransform.anchoredPosition = new Vector2(0f, -6f);
            }

            _cornerRank.color = color;
            _cornerSuit.color = color;
            _cornerRankBottom.color = color;
            _cornerSuitBottom.color = color;
            _centerSuit.color = color;
            _centerRank.color = color;

            SetFaceVisible(true);
        }

        public void SetFaceDown()
        {
            _faceDown = true;
            _background.sprite = SpriteFactory.CardBack((int)TableTheme.CardWidth, (int)TableTheme.CardHeight);
            SetFaceVisible(false);
        }

        private void SetFaceVisible(bool visible)
        {
            _cornerRank.enabled = visible;
            _cornerSuit.enabled = visible;
            _cornerRankBottom.enabled = visible;
            _cornerSuitBottom.enabled = visible;
            _centerSuit.enabled = visible;
            _centerRank.enabled = visible;
        }

        /// <summary>
        /// Recoloca la carta sin animar el vuelo. Se usa cuando el abanico se
        /// recentra al llegar una carta nueva: las que ya estaban se deslizan,
        /// no vuelven a repartirse.
        /// </summary>
        public void MoveTo(Vector2 target, float tilt)
        {
            if (_running != null) return; // no interrumpir un reparto en curso

            _rect.anchoredPosition = target;
            _rect.localRotation = Quaternion.Euler(0f, 0f, tilt);
            _rect.localScale = Vector3.one;
        }

        /// <summary>
        /// Vuela desde <paramref name="from"/> hasta su sitio. La curva arranca
        /// rápida y frena al final, que es como se desliza una carta lanzada
        /// sobre fieltro.
        /// </summary>
        public void DealFrom(Vector2 from, Vector2 to, float tilt, float delay)
        {
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(DealRoutine(from, to, tilt, delay));
        }

        private IEnumerator DealRoutine(Vector2 from, Vector2 to, float tilt, float delay)
        {
            _rect.anchoredPosition = from;
            _rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-12f, 12f));
            _rect.localScale = Vector3.one * 0.9f;

            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            if (delay > 0f) yield return new WaitForSeconds(delay);

            canvasGroup.alpha = 1f;

            // El roce suena al salir la carta, no al programarla: si se
            // dispara antes del retardo, todas las cartas suenan a la vez y se
            // pierde el ritmo del reparto.
            CasinoAudio.Instance.Deal();

            const float duration = 0.26f;
            float elapsed = 0f;
            Quaternion startRotation = _rect.localRotation;
            Quaternion endRotation = Quaternion.Euler(0f, 0f, tilt);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Desaceleración: rápida al salir del zapato, suave al posarse.
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                _rect.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
                _rect.localRotation = Quaternion.SlerpUnclamped(startRotation, endRotation, eased);
                _rect.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, eased);

                yield return null;
            }

            // Asentamiento: un pequeño rebote de escala, sin deformar la carta.
            const float settle = 0.11f;
            elapsed = 0f;
            _rect.anchoredPosition = to;
            _rect.localRotation = endRotation;

            while (elapsed < settle)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / settle);
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.05f;
                _rect.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            _rect.localScale = Vector3.one;
            _running = null;
        }

        /// <summary>
        /// Voltea la carta encogiéndola por el eje X y cambiando la cara a
        /// mitad del giro, que es cuando estaría de canto.
        /// </summary>
        public void FlipTo(Card card)
        {
            if (!_faceDown)
            {
                SetCard(card);
                return;
            }

            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(FlipRoutine(card));
        }

        private IEnumerator FlipRoutine(Card card)
        {
            const float half = 0.15f;
            float elapsed = 0f;

            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                _rect.localScale = new Vector3(1f - t, 1f, 1f);
                yield return null;
            }

            SetCard(card);

            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                _rect.localScale = new Vector3(t, 1f, 1f);
                yield return null;
            }

            _rect.localScale = Vector3.one;
            _running = null;
        }
    }
}
