using System.Collections;
using System.Collections.Generic;
using Blackjack.Core.Cards;
using Blackjack.Protocol.Dtos;
using UnityEngine;
using UnityEngine.UI;

namespace Blackjack.Client.UI
{
    /// <summary>
    /// Un asiento de la mesa: su hueco de apuesta, las fichas puestas, las
    /// cartas y el badge con el total.
    ///
    /// Solo pinta. Las manos, los totales y qué está permitido vienen ya
    /// resueltos del servidor.
    /// </summary>
    public sealed class SeatView : MonoBehaviour
    {
        public const float Width = 260f;
        public const float Height = 340f;

        private RectTransform _rect;
        private Image _spot;
        private Image _turnGlow;
        private Image _ownerRing;
        private Text _nameLabel;
        private Text _betLabel;
        private Text _resultLabel;
        private RectTransform _chips;
        private RectTransform _handsRoot;

        private readonly List<HandRow> _hands = new List<HandRow>();
        private readonly List<Image> _chipImages = new List<Image>();
        private int _chipsShown;
        private bool _isTurn;

        /// <summary>Una mano del asiento: sus cartas y su badge de total.</summary>
        private sealed class HandRow
        {
            public RectTransform Root;
            public RectTransform CardsRoot;
            public Image Highlight;
            public Image Badge;
            public Text BadgeText;
            public string LastBadge = string.Empty;
            public readonly List<CardView> Cards = new List<CardView>();
        }

        public int SeatIndex { get; private set; }

        public static SeatView Create(Transform parent, int seatIndex, Vector2 position)
        {
            RectTransform rect = UIFactory.Rect("Seat" + seatIndex, parent);
            UIFactory.Place(rect, new Vector2(0.5f, 0.5f), position, new Vector2(Width, Height));

            var view = rect.gameObject.AddComponent<SeatView>();
            view.SeatIndex = seatIndex;
            view.Build();
            return view;
        }

        private void Build()
        {
            _rect = (RectTransform)transform;

            // Resplandor de turno, detrás del hueco de apuesta.
            _turnGlow = UIFactory.Panel("TurnGlow", _rect,
                SpriteFactory.Circle((int)TableTheme.SpotSize + 40, new Color(1f, 0.87f, 0.32f, 0.28f)));
            UIFactory.Place(_turnGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -72f),
                new Vector2(TableTheme.SpotSize + 40f, TableTheme.SpotSize + 40f));
            _turnGlow.enabled = false;

            // Aro verde que señala tus asientos.
            _ownerRing = UIFactory.Panel("OwnerRing", _rect,
                SpriteFactory.Circle((int)TableTheme.SpotSize + 16, new Color(0f, 0f, 0f, 0f),
                    Color.white, 4));
            UIFactory.Place(_ownerRing.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -72f),
                new Vector2(TableTheme.SpotSize + 16f, TableTheme.SpotSize + 16f));
            _ownerRing.enabled = false;

            _spot = UIFactory.Panel("Spot", _rect,
                SpriteFactory.DashedCircle((int)TableTheme.SpotSize, TableTheme.SpotLine, 4));
            UIFactory.Place(_spot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -72f),
                new Vector2(TableTheme.SpotSize, TableTheme.SpotSize));

            _chips = UIFactory.Rect("Chips", _rect);
            UIFactory.Place(_chips, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(120f, 120f));

            _handsRoot = UIFactory.Rect("Hands", _rect);
            UIFactory.Place(_handsRoot, new Vector2(0.5f, 0.5f), new Vector2(0f, 82f), new Vector2(Width, 180f));

            _resultLabel = UIFactory.Label("Result", _rect, string.Empty, 24, TableTheme.WinGreen,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(_resultLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -8f),
                new Vector2(Width, 30f));

            _nameLabel = UIFactory.Label("Name", _rect, string.Empty, 18, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Place(_nameLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -152f),
                new Vector2(Width, 24f));

            _betLabel = UIFactory.Label("Bet", _rect, string.Empty, 17, TableTheme.GoldText,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(_betLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -174f),
                new Vector2(Width, 22f));
        }

        /// <summary>
        /// Marca si el asiento es tuyo y si es el que recibe las fichas.
        ///
        /// Al jugar varios asientos hace falta distinguir de un vistazo cuáles
        /// son propios y en cuál se está apostando ahora mismo.
        /// </summary>
        public void SetOwnership(bool isMine, bool isActive)
        {
            _ownerRing.enabled = isMine;
            _ownerRing.color = isActive
                ? new Color(0.35f, 0.85f, 0.45f, 0.95f)
                : new Color(0.35f, 0.85f, 0.45f, 0.35f);
        }

        private void Update()
        {
            // Latido del resplandor mientras es tu turno: llama la atención sin
            // necesidad de un cartel.
            if (!_isTurn) return;

            float pulse = 0.22f + 0.16f * Mathf.Sin(Time.time * 4.5f);
            Color color = _turnGlow.color;
            color.a = pulse;
            _turnGlow.color = color;
        }

        /// <summary>
        /// Vuelca el estado del asiento. <paramref name="dealOrigin"/> es de
        /// donde salen volando las cartas nuevas: el zapato.
        /// </summary>
        public void Render(SeatDto seat, bool isTurn, int currentHand, Vector2 dealOrigin,
            bool showResult, DealClock clock)
        {
            _isTurn = isTurn;
            _turnGlow.enabled = isTurn;

            bool occupied = seat.PlayerName != null;

            _nameLabel.text = occupied ? seat.PlayerName : "libre";
            _nameLabel.color = occupied
                ? (seat.IsConnected ? Color.white : new Color(1f, 1f, 1f, 0.45f))
                : new Color(1f, 1f, 1f, 0.30f);

            // Las apuestas laterales se anuncian junto a la principal.
            var bet = new System.Text.StringBuilder();
            if (seat.MainBet > 0m) bet.Append(seat.MainBet.ToString("0.##"));
            if (seat.PerfectPairsBet > 0m) bet.Append("  ·  PP ").Append(seat.PerfectPairsBet.ToString("0.##"));
            if (seat.TwentyOnePlus3Bet > 0m) bet.Append("  ·  21+3 ").Append(seat.TwentyOnePlus3Bet.ToString("0.##"));
            _betLabel.text = bet.ToString();

            RenderChips(seat.MainBet);
            RenderHands(seat, currentHand, isTurn, dealOrigin, clock);

            if (showResult && seat.LastRoundReturned > 0m)
            {
                _resultLabel.text = "+" + seat.LastRoundReturned.ToString("0.##");
                _resultLabel.color = TableTheme.WinGreen;
            }
            else
            {
                _resultLabel.text = string.Empty;
            }
        }

        /// <summary>
        /// Apila fichas hasta cubrir la apuesta, de mayor a menor valor. Una
        /// pila real se ve como fichas superpuestas, no como un número suelto.
        /// </summary>
        private void RenderChips(decimal amount)
        {
            var denominations = new[] { 100m, 25m, 5m, 1m };
            var stack = new List<decimal>();
            decimal remaining = amount;

            foreach (decimal denomination in denominations)
            {
                while (remaining >= denomination && stack.Count < 9)
                {
                    stack.Add(denomination);
                    remaining -= denomination;
                }
            }

            while (_chipImages.Count < stack.Count)
            {
                Image chip = UIFactory.Panel("Chip" + _chipImages.Count, _chips, null);
                UIFactory.AddShadow(chip, new Vector2(1f, -2f), 0.45f);

                Text value = UIFactory.Label("Value", chip.rectTransform, string.Empty, 15, Color.white,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.Stretch(value.rectTransform);

                _chipImages.Add(chip);
            }

            for (int i = 0; i < _chipImages.Count; i++)
            {
                Image chip = _chipImages[i];

                if (i >= stack.Count)
                {
                    chip.gameObject.SetActive(false);
                    continue;
                }

                decimal denomination = stack[i];
                bool isNew = !chip.gameObject.activeSelf;

                chip.gameObject.SetActive(true);
                chip.sprite = SpriteFactory.Chip((int)TableTheme.ChipSize, TableTheme.ChipColor(denomination));

                // Cada ficha se apoya sobre la anterior con un ligero desfase.
                UIFactory.Place(chip.rectTransform, new Vector2(0.5f, 0.5f),
                    new Vector2(0f, i * 6f), new Vector2(TableTheme.ChipSize, TableTheme.ChipSize));

                var label = chip.GetComponentInChildren<Text>();
                label.text = denomination.ToString("0");
                label.color = TableTheme.ChipTextColor(denomination);
                label.enabled = i == stack.Count - 1; // solo la de arriba muestra su valor

                if (isNew) StartCoroutine(DropChip(chip.rectTransform, i * 6f));
            }

            _chipsShown = stack.Count;
        }

        /// <summary>La ficha cae desde arriba y rebota al posarse.</summary>
        private static IEnumerator DropChip(RectTransform chip, float restY)
        {
            const float duration = 0.22f;
            float elapsed = 0f;
            float startY = restY + 90f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                // Pequeño rebote al final del recorrido.
                float bounce = Mathf.Sin(t * Mathf.PI) * 6f * (1f - t);

                chip.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, restY, eased) + bounce);
                yield return null;
            }

            chip.anchoredPosition = new Vector2(0f, restY);
        }

        private void RenderHands(SeatDto seat, int currentHand, bool isTurn, Vector2 dealOrigin,
            DealClock clock)
        {
            while (_hands.Count < seat.Hands.Count) _hands.Add(CreateHandRow(_hands.Count));

            int count = seat.Hands.Count;

            for (int h = 0; h < _hands.Count; h++)
            {
                HandRow row = _hands[h];

                if (h >= count)
                {
                    row.Root.gameObject.SetActive(false);
                    continue;
                }

                row.Root.gameObject.SetActive(true);
                HandDto hand = seat.Hands[h];

                // Las manos partidas se separan a los lados y se encogen para
                // que quepan sin invadir al vecino. Cuantas más manos, más
                // apretadas: con cuatro no caben a tamaño completo.
                float spacing = count == 2 ? 118f : 88f;
                float scale = count == 1 ? 1f : count == 2 ? 0.74f : 0.55f;

                row.Root.anchoredPosition = new Vector2((h - (count - 1) * 0.5f) * spacing, 0f);
                row.Root.localScale = Vector3.one * scale;

                // Al partir hay que ver de un vistazo cuál se está jugando: la
                // activa queda iluminada y las demás atenuadas.
                bool active = isTurn && h == currentHand;
                row.Highlight.enabled = count > 1 && active;

                var group = row.Root.GetComponent<CanvasGroup>();
                group.alpha = count > 1 && isTurn && !active ? 0.55f : 1f;

                RenderCards(row, hand, dealOrigin, clock);
                RenderBadge(row, hand);
            }
        }

        private HandRow CreateHandRow(int index)
        {
            var row = new HandRow { Root = UIFactory.Rect("Hand" + index, _handsRoot) };
            UIFactory.Place(row.Root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 180f));
            row.Root.gameObject.AddComponent<CanvasGroup>();

            // Marco que señala qué mano se está jugando tras un split.
            row.Highlight = UIFactory.Panel("Highlight", row.Root,
                SpriteFactory.RoundedRect(210, 190, 16, new Color(1f, 0.87f, 0.32f, 0.16f),
                    TableTheme.TurnGlow, 3));
            UIFactory.Place(row.Highlight.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f),
                new Vector2(210f, 190f));
            row.Highlight.enabled = false;

            // Las cartas van en su propio contenedor para poder centrar el
            // abanico sin descolocar el badge.
            row.CardsRoot = UIFactory.Rect("Cards", row.Root);
            UIFactory.Place(row.CardsRoot, new Vector2(0.5f, 0.5f), new Vector2(0f, -14f), new Vector2(1f, 1f));

            row.Badge = UIFactory.Panel("Badge", row.Root,
                SpriteFactory.RoundedRect(64, 38, 8, TableTheme.BadgeFill));
            UIFactory.Place(row.Badge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 88f),
                new Vector2(64f, 38f));
            UIFactory.AddShadow(row.Badge, new Vector2(2f, -3f), 0.45f);

            row.BadgeText = UIFactory.Label("Total", row.Badge.rectTransform, string.Empty, 22,
                TableTheme.BadgeText, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(row.BadgeText.rectTransform);

            return row;
        }

        private void RenderCards(HandRow row, HandDto hand, Vector2 dealOrigin, DealClock clock)
        {
            while (row.Cards.Count < hand.Cards.Count)
            {
                row.Cards.Add(CardView.Create(row.CardsRoot, "Card" + row.Cards.Count));
            }

            // El abanico se centra: sin esto, cada carta nueva desplaza la mano
            // hacia la derecha y el badge deja de cuadrar con ella.
            //
            // A partir de cuatro cartas se aprietan: con el solape normal, una
            // mano larga se saldría del asiento e invadiría al vecino.
            float overlap = hand.Cards.Count > 4
                ? TableTheme.CardOverlapX * 0.68f
                : TableTheme.CardOverlapX;

            float span = (hand.Cards.Count - 1) * overlap;
            float startX = -span * 0.5f;

            for (int i = 0; i < row.Cards.Count; i++)
            {
                CardView card = row.Cards[i];

                if (i >= hand.Cards.Count)
                {
                    card.gameObject.SetActive(false);
                    continue;
                }

                bool isNew = !card.gameObject.activeSelf;
                card.gameObject.SetActive(true);
                card.SetCard(Card.FromId(hand.Cards[i]));

                var target = new Vector2(startX + i * overlap, -i * TableTheme.CardOverlapY);
                float tilt = (i - (hand.Cards.Count - 1) * 0.5f) * 2f;

                if (isNew) card.DealFrom(dealOrigin, target, tilt, clock.Next());
                else card.MoveTo(target, tilt);
            }
        }

        private void RenderBadge(HandRow row, HandDto hand)
        {
            if (hand.Cards.Count == 0)
            {
                row.Badge.enabled = false;
                row.BadgeText.enabled = false;
                return;
            }

            row.Badge.enabled = true;
            row.BadgeText.enabled = true;

            string text;
            Color fill;
            Color textColor;
            int size;
            Vector2 badgeSize;

            if (hand.IsBlackjack)
            {
                text = "BLACKJACK";
                fill = TableTheme.BadgeBlackjack;
                textColor = TableTheme.GoldText;
                size = 13;
                badgeSize = new Vector2(104f, 38f);
            }
            else if (hand.IsBust)
            {
                text = hand.Total.ToString();
                fill = TableTheme.LoseRed;
                textColor = Color.white;
                size = 22;
                badgeSize = new Vector2(64f, 38f);
            }
            else
            {
                // Un total blando se anuncia como "7/17": es la información que
                // necesita el jugador para decidir, y las mesas reales la dan.
                text = hand.IsSoft && hand.Total <= 21
                    ? (hand.Total - 10) + "/" + hand.Total
                    : hand.Total.ToString();
                fill = TableTheme.BadgeFill;
                textColor = TableTheme.BadgeText;
                size = 22;
                badgeSize = new Vector2(hand.IsSoft ? 78f : 64f, 38f);
            }

            row.Badge.sprite = SpriteFactory.RoundedRect((int)badgeSize.x, (int)badgeSize.y, 8, fill);
            row.Badge.rectTransform.sizeDelta = badgeSize;
            row.BadgeText.text = text;
            row.BadgeText.fontSize = size;
            row.BadgeText.color = textColor;

            // Golpe de escala cuando el total cambia, para que el ojo lo pille.
            if (row.LastBadge != text)
            {
                row.LastBadge = text;
                StartCoroutine(PopBadge(row.Badge.rectTransform));
            }
        }

        private static IEnumerator PopBadge(RectTransform badge)
        {
            const float duration = 0.24f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.30f;
                badge.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            badge.localScale = Vector3.one;
        }
    }
}
