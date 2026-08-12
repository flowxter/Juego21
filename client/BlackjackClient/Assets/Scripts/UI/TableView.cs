using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Blackjack.Core.Cards;
using Blackjack.Core.Rounds;
using Blackjack.Core.Rules;
using Blackjack.Protocol;
using Blackjack.Protocol.Dtos;
using UnityEngine;
using UnityEngine.UI;

namespace Blackjack.Client.UI
{
    /// <summary>
    /// La mesa.
    ///
    /// Construye el tapete y lo mantiene al día con los snapshots del
    /// servidor. No decide nada: los botones que ofrece salen de
    /// <see cref="TableSnapshot.LegalActions"/>, que calcula el servidor con el
    /// mismo código de reglas que comparte con el cliente.
    /// </summary>
    public sealed class TableView : MonoBehaviour
    {
        // Geometría de la mesa. Es una elipse que sobresale por arriba de la
        // pantalla, de modo que solo se ve su mitad inferior: la silueta
        // semicircular con el croupier en el lado recto.
        private const float TableCenterY = 520f;
        private const float TableRadiusX = 1200f;
        private const float TableRadiusY = 850f;

        /// <summary>Dónde cae el arco de apuestas dentro de la elipse.</summary>
        private const float ArcRadius = 0.85f;

        /// <summary>Punto del que salen las cartas: la boca del zapato.</summary>
        private static readonly Vector2 ShoeMouth = new Vector2(730f, 320f);

        /// <summary>
        /// Sitio de un asiento sobre el arco. Se usa la misma elipse con la que
        /// se dibuja la mesa, así que los huecos caen exactamente encima de la
        /// línea amarilla en lugar de aproximarse a ojo.
        /// </summary>
        private static Vector2 SeatPosition(int index, int seatCount)
        {
            float step = 17.5f * Mathf.Deg2Rad;
            float angle = (index - (seatCount - 1) * 0.5f) * step;

            return new Vector2(
                TableRadiusX * ArcRadius * Mathf.Sin(angle),
                TableCenterY - TableRadiusY * ArcRadius * Mathf.Cos(angle));
        }

        /// <summary>Fichas del rack. El tope es 100 por ficha; se acumulan.</summary>
        private static readonly decimal[] Denominations = { 1m, 5m, 25m, 100m };

        private RectTransform _root;
        private RectTransform _dealerHands;
        private Image _dealerBadge;
        private Text _dealerBadgeText;
        private readonly List<CardView> _dealerCards = new List<CardView>();

        /// <summary>Tope de asientos por jugador; coincide con el del servidor.</summary>
        private const int MaxSeats = 3;

        private readonly List<SeatView> _seats = new List<SeatView>();
        private readonly List<Button> _sitButtons = new List<Button>();
        private readonly List<Button> _selectButtons = new List<Button>();

        private Text _balanceLabel;
        private Text _phaseLabel;
        private Text _timerLabel;
        private Text _shoeLabel;
        private Text _rulesLabel;
        private Text _limitsLabel;
        private Text _messageLabel;
        private Image _messagePanel;

        private RectTransform _actionBar;

        /// <summary>A qué apuesta van las fichas que se toquen.</summary>
        private enum BetTarget
        {
            Main,
            PerfectPairs,
            TwentyOnePlus3
        }

        private readonly Image[] _betBoxes = new Image[3];
        private readonly Text[] _betAmounts = new Text[3];
        private BetTarget _target = BetTarget.Main;

        /// <summary>Escalona las cartas de cada lote para que no salgan a la vez.</summary>
        private readonly DealClock _dealClock = new DealClock();

        /// <summary>Asientos que ocupas. Puedes jugar más de uno a la vez.</summary>
        private readonly List<int> _mySeats = new List<int>();

        private int _activeSeat = -1;

        private TableSnapshot _snapshot;
        private decimal _pendingMain;
        private decimal _pendingPairs;
        private decimal _pendingTrio;
        private decimal _balance;
        private string _myName = string.Empty;
        private float _messageUntil;
        private string _actionsKey = string.Empty;
        private TablePhase _lastPhase = TablePhase.WaitingForPlayers;

        public event Action<int> SitRequested;

        /// <summary>Asiento, apuesta principal, Perfect Pairs y 21+3.</summary>
        public event Action<int, decimal, decimal, decimal> BetRequested;

        /// <summary>Asiento del que levantarse.</summary>
        public event Action<int> StandUpSeatRequested;
        public event Action StandUpRequested;
        public event Action<PlayerAction> ActionRequested;
        public event Action<bool> InsuranceAnswered;

        /// <summary>"Ya he apostado, podemos empezar."</summary>
        public event Action ReadyRequested;

        private StrategyGuideView _guide;

        private void ShowGuide()
        {
            if (_guide != null) return;
            _guide = StrategyGuideView.Create(transform.parent);
        }

        public static TableView Create(Transform canvas, string myName)
        {
            RectTransform rect = UIFactory.Rect("Table", canvas);
            UIFactory.Stretch(rect);

            var view = rect.gameObject.AddComponent<TableView>();
            view._myName = myName;
            view.Build();
            return view;
        }

        // ------------------------------------------------------------------
        // Construcción
        // ------------------------------------------------------------------

        private void Build()
        {
            _root = (RectTransform)transform;

            BuildFelt();
            BuildFurniture();
            BuildBanner();
            BuildDealer();
            BuildSeats();
            BuildTopBar();
            BuildActionBar();

            // El aviso va sobre su propia placa oscura y en la franja libre
            // entre las cartas de los jugadores y la banda de reglas: sin fondo
            // y a la altura de la banda, el texto se perdía sobre el rojo.
            _messagePanel = UIFactory.Panel("MessagePanel", _root,
                SpriteFactory.RoundedRect(760, 62, 16, new Color(0.04f, 0.05f, 0.07f, 0.90f),
                    new Color(1f, 0.87f, 0.32f, 0.45f), 2));
            UIFactory.Place(_messagePanel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 115f),
                new Vector2(760f, 62f));
            UIFactory.AddShadow(_messagePanel, new Vector2(0f, -4f), 0.5f);
            _messagePanel.gameObject.SetActive(false);

            _messageLabel = UIFactory.Label("Message", _messagePanel.rectTransform, string.Empty, 32,
                Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(_messageLabel.rectTransform);
        }

        private void BuildFelt()
        {
            // Fondo de la sala: moqueta oscura alrededor de la mesa.
            Image backdrop = UIFactory.Panel("Backdrop", _root,
                SpriteFactory.Solid(new Color(0.09f, 0.06f, 0.07f)));
            UIFactory.Stretch(backdrop.rectTransform);

            Image table = UIFactory.Panel("Table", _root,
                SpriteFactory.TableSurface(820, 580, ArcRadius));
            UIFactory.Place(table.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0f, TableCenterY), new Vector2(TableRadiusX * 2f, TableRadiusY * 2f));
        }

        private void BuildBanner()
        {
            // Banda con las condiciones de la mesa, como el arco rojo de las
            // mesas reales. Recta en vez de curva: el texto curvo exige malla
            // propia y aporta poco frente a lo que cuesta.
            Image banner = UIFactory.Panel("Banner", _root,
                SpriteFactory.RoundedRect(1360, 70, 22, TableTheme.BannerRed));
            UIFactory.Place(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 205f),
                new Vector2(1300f, 68f));
            UIFactory.AddShadow(banner, new Vector2(0f, -5f), 0.4f);

            _rulesLabel = UIFactory.Label("Rules", banner.rectTransform, string.Empty, 21,
                TableTheme.BannerText, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(_rulesLabel.rectTransform);
        }

        private void BuildFurniture()
        {
            BuildShoe();
            BuildDiscardTray();

            // Cartel de límites, como el "Mín 1 € / Máx 50 €" de la referencia.
            Image limits = UIFactory.Panel("Limits", _root,
                SpriteFactory.RoundedRect(158, 70, 10, new Color(0.95f, 0.92f, 0.80f)));
            UIFactory.Place(limits.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-545f, 415f),
                new Vector2(158f, 70f));
            UIFactory.AddShadow(limits, new Vector2(4f, -4f), 0.45f);

            _limitsLabel = UIFactory.Label("LimitsText", limits.rectTransform, string.Empty, 18,
                new Color(0.2f, 0.15f, 0.10f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(_limitsLabel.rectTransform);
        }

        /// <summary>
        /// El zapato: caja inclinada con el mazo asomando por la boca.
        ///
        /// Se compone con varias piezas en vez de una imagen plana porque lo
        /// que lo hace reconocible es el volumen — la tapa, el mazo dentro y la
        /// carta que asoma por donde salen.
        /// </summary>
        private void BuildShoe()
        {
            RectTransform shoe = UIFactory.Rect("Shoe", _root);
            UIFactory.Place(shoe, new Vector2(0.5f, 0.5f), new Vector2(775f, 330f), new Vector2(200f, 160f));
            shoe.localRotation = Quaternion.Euler(0f, 0f, -14f);

            // Cuerpo, en madera oscura barnizada.
            Image body = UIFactory.Panel("Body", shoe,
                SpriteFactory.RoundedRect(170, 130, 12, new Color(0.30f, 0.17f, 0.10f),
                    new Color(0f, 0f, 0f, 0.35f), 3));
            UIFactory.Place(body.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(170f, 130f));
            UIFactory.AddShadow(body, new Vector2(6f, -8f), 0.55f);

            // Mazo dentro, visto desde arriba.
            Image deck = UIFactory.Panel("Deck", shoe, SpriteFactory.CardBack(126, 84));
            UIFactory.Place(deck.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f),
                new Vector2(126f, 84f));

            // Boca por donde sale cada carta: una franja clara al frente.
            Image mouth = UIFactory.Panel("Mouth", shoe,
                SpriteFactory.RoundedRect(150, 26, 6, new Color(0.86f, 0.84f, 0.79f)));
            UIFactory.Place(mouth.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -42f),
                new Vector2(150f, 26f));

            _shoeLabel = UIFactory.Label("ShoeCount", mouth.rectTransform, string.Empty, 13,
                new Color(0.24f, 0.20f, 0.16f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(_shoeLabel.rectTransform);
        }

        /// <summary>Bandeja de descartes: las cartas ya jugadas, boca abajo.</summary>
        private void BuildDiscardTray()
        {
            RectTransform tray = UIFactory.Rect("Discard", _root);
            UIFactory.Place(tray, new Vector2(0.5f, 0.5f), new Vector2(-775f, 330f), new Vector2(190f, 150f));
            tray.localRotation = Quaternion.Euler(0f, 0f, 12f);

            Image body = UIFactory.Panel("Body", tray,
                SpriteFactory.RoundedRect(160, 120, 12, new Color(0.26f, 0.15f, 0.09f),
                    new Color(0f, 0f, 0f, 0.35f), 3));
            UIFactory.Place(body.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(160f, 120f));
            UIFactory.AddShadow(body, new Vector2(6f, -8f), 0.5f);

            // Pila de descartes, ligeramente desalineada como en una mesa real.
            for (int i = 0; i < 3; i++)
            {
                Image card = UIFactory.Panel("Discarded" + i, tray, SpriteFactory.CardBack(112, 76));
                UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f),
                    new Vector2(i * 2f - 2f, i * 3f), new Vector2(112f, 76f));
                card.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (i - 1) * 2.5f);
            }

            Text caption = UIFactory.Label("Caption", tray, "descartes", 12,
                new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleCenter);
            UIFactory.Place(caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 8f),
                new Vector2(160f, 18f));
        }

        private void BuildDealer()
        {
            _dealerHands = UIFactory.Rect("DealerHand", _root);
            UIFactory.Place(_dealerHands, new Vector2(0.5f, 0.5f), new Vector2(0f, 355f),
                new Vector2(1f, 1f));

            _dealerBadge = UIFactory.Panel("DealerBadge", _root,
                SpriteFactory.RoundedRect(64, 38, 8, TableTheme.BadgeFill));
            UIFactory.Place(_dealerBadge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 462f),
                new Vector2(64f, 38f));
            UIFactory.AddShadow(_dealerBadge, new Vector2(2f, -3f), 0.45f);

            _dealerBadgeText = UIFactory.Label("Total", _dealerBadge.rectTransform, string.Empty, 22,
                TableTheme.BadgeText, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(_dealerBadgeText.rectTransform);

            _dealerBadge.enabled = false;
            _dealerBadgeText.enabled = false;

            Text caption = UIFactory.Label("DealerCaption", _root, "CROUPIER", 16,
                new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(caption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 505f),
                new Vector2(300f, 24f));
        }

        private void BuildSeats()
        {
            for (int i = 0; i < 5; i++)
            {
                // Sobre el arco de la mesa: los extremos quedan más altos, como
                // en una mesa semicircular vista desde el sitio del croupier.
                Vector2 position = SeatPosition(i, 5);

                SeatView seat = SeatView.Create(_root, i, position);
                _seats.Add(seat);

                int index = i;
                Button sit = UIFactory.Button("Sit" + i, _root, "Sentarse", () =>
                {
                    CasinoAudio.Instance.Click();
                    SitRequested?.Invoke(index);
                }, 16);
                UIFactory.Place(sit.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                    position + new Vector2(0f, -72f), new Vector2(130f, 42f));

                _sitButtons.Add(sit);

                // Zona invisible sobre el hueco: al jugar varios asientos, se
                // toca el que quieres para dirigir ahí las fichas.
                Button select = UIFactory.Button("Select" + i, _root, string.Empty, () =>
                {
                    _activeSeat = index;
                    CasinoAudio.Instance.Click();
                    _actionsKey = string.Empty;
                    if (_snapshot != null) ApplySnapshot(_snapshot);
                }, 1);
                var selectImage = select.GetComponent<Image>();
                selectImage.sprite = SpriteFactory.RoundedRect(150, 150, 75, new Color(1f, 1f, 1f, 0.001f));
                UIFactory.Place(select.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                    position + new Vector2(0f, -72f), new Vector2(150f, 150f));
                select.gameObject.SetActive(false);

                _selectButtons.Add(select);
            }
        }

        private void BuildTopBar()
        {
            Image bar = UIFactory.Panel("TopBar", _root,
                SpriteFactory.RoundedRect(1240, 60, 14, TableTheme.PanelFill));
            UIFactory.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -46f),
                new Vector2(1240f, 60f));

            _balanceLabel = UIFactory.Label("Balance", bar.rectTransform, string.Empty, 24,
                TableTheme.GoldText, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place(_balanceLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(300f, 0f),
                new Vector2(320f, 40f));

            _phaseLabel = UIFactory.Label("Phase", bar.rectTransform, string.Empty, 24, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(_phaseLabel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(560f, 40f));

            _timerLabel = UIFactory.Label("Timer", bar.rectTransform, string.Empty, 28, Color.white,
                TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.Place(_timerLabel.rectTransform, new Vector2(1f, 0.5f), new Vector2(-180f, 0f),
                new Vector2(200f, 40f));

            Button music = UIFactory.Button("Music", bar.rectTransform, "♪", () =>
            {
                CasinoAudio.Instance.MusicEnabled = !CasinoAudio.Instance.MusicEnabled;
                CasinoAudio.Instance.Click();
            }, 22);
            UIFactory.Place(music.GetComponent<RectTransform>(), new Vector2(1f, 0.5f),
                new Vector2(-52f, 0f), new Vector2(46f, 44f));

            // Chuleta de estrategia. Es solo consulta: no toca el juego ni
            // decide nada, únicamente muestra qué conviene hacer en cada mano.
            Button guide = UIFactory.Button("Guide", bar.rectTransform, "GUÍA", () =>
            {
                CasinoAudio.Instance.Click();
                ShowGuide();
            }, 16);
            UIFactory.Place(guide.GetComponent<RectTransform>(), new Vector2(0f, 0.5f),
                new Vector2(70f, 0f), new Vector2(96f, 44f));
        }

        private void BuildActionBar()
        {
            Image bar = UIFactory.Panel("ActionBar", _root,
                SpriteFactory.RoundedRect(1400, 110, 16, TableTheme.PanelFill));
            UIFactory.Place(bar.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 80f),
                new Vector2(1400f, 110f));

            _actionBar = UIFactory.Rect("Actions", bar.rectTransform);
            UIFactory.Stretch(_actionBar);
        }

        // ------------------------------------------------------------------
        // Actualización
        // ------------------------------------------------------------------

        public void SetBalance(decimal balance)
        {
            _balance = balance;
            _balanceLabel.text = "Saldo: " + balance.ToString("0.##");
        }

        public void ShowMessage(string message, float seconds = 3f)
        {
            _messageLabel.text = message;
            _messagePanel.gameObject.SetActive(true);
            _messageUntil = Time.time + seconds;
            StartCoroutine(PopMessage());
        }

        private IEnumerator PopMessage()
        {
            RectTransform rect = _messagePanel.rectTransform;
            const float duration = 0.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = Mathf.Lerp(0.6f, 1f, 1f - Mathf.Pow(1f - t, 3f));
                rect.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            rect.localScale = Vector3.one;
        }

        public void ApplySnapshot(TableSnapshot snapshot)
        {
            _snapshot = snapshot;

            // Cada snapshot abre una tanda de reparto: las cartas que lleguen
            // se escalonan entre sí en vez de aparecer todas de golpe.
            _dealClock.Reset();

            TableRulesDto rules = snapshot.Rules;
            _rulesLabel.text =
                $"EL BLACKJACK PAGA 3 A 2   ·   EL CROUPIER {(rules.DealerHitsSoft17 ? "PIDE" : "SE PLANTA")} CON 17   ·   " +
                $"SEGURO PAGA 2 A 1";

            _limitsLabel.text = $"Mín: {rules.MinBet:0.##}\nMáx: {rules.MaxBet:0.##}";
            _phaseLabel.text = PhaseName(snapshot.Phase);
            _shoeLabel.text = $"{snapshot.ShoeTotalCards - snapshot.ShoeCardsDealt} cartas";

            int mySeat = ActiveSeat;
            bool showResult = snapshot.Phase == TablePhase.Payout;

            // Primero los jugadores y el croupier al final, que es el orden en
            // que reparte un croupier de verdad.
            for (int i = 0; i < _seats.Count && i < snapshot.Seats.Count; i++)
            {
                _seats[i].Render(snapshot.Seats[i], snapshot.CurrentSeat == i, snapshot.CurrentHand,
                    ShoeMouth, showResult, _dealClock);

                // Marca cuál de tus asientos recibe las fichas que pongas.
                _seats[i].SetOwnership(_mySeats.Contains(i), _mySeats.Count > 1 && i == mySeat);
            }

            RenderDealer(snapshot);

            // Se puede ocupar más de un asiento, así que el botón sigue
            // apareciendo en los libres mientras quede cupo.
            bool canSitMore = _mySeats.Count < MaxSeats;

            for (int i = 0; i < _sitButtons.Count; i++)
            {
                bool free = i < snapshot.Seats.Count && snapshot.Seats[i].PlayerName == null;
                _sitButtons[i].gameObject.SetActive(free && canSitMore);

                // Tus asientos son clicables para elegir dónde apuestas.
                _selectButtons[i].gameObject.SetActive(
                    _mySeats.Count > 1 && _mySeats.Contains(i) && snapshot.Phase == TablePhase.Betting);
            }

            AnnouncePhaseChange(snapshot, mySeat);

            // La botonera se rehace solo cuando cambia lo que ofrece: hacerlo en
            // cada snapshot destruiría los botones bajo el cursor del jugador.
            string key = BuildActionsKey(snapshot, mySeat);
            if (key != _actionsKey)
            {
                _actionsKey = key;
                RebuildActions(snapshot, mySeat);
            }
            else
            {
                RefreshBetBoxes();
            }
        }

        private void AnnouncePhaseChange(TableSnapshot snapshot, int mySeat)
        {
            if (snapshot.Phase == _lastPhase) return;

            TablePhase previous = _lastPhase;
            _lastPhase = snapshot.Phase;

            switch (snapshot.Phase)
            {
                case TablePhase.Betting:
                    if (previous != TablePhase.WaitingForPlayers)
                    {
                        _pendingMain = 0m;
                        _pendingPairs = 0m;
                        _pendingTrio = 0m;
                    }
                    ShowMessage("¡Hagan sus apuestas!", 2f);
                    break;

                case TablePhase.Dealing:
                    CasinoAudio.Instance.Shuffle();
                    break;

                case TablePhase.Payout:
                    AnnounceResult(snapshot, mySeat);
                    break;
            }
        }

        private void AnnounceResult(TableSnapshot snapshot, int mySeat)
        {
            if (mySeat < 0 || mySeat >= snapshot.Seats.Count) return;

            SeatDto seat = snapshot.Seats[mySeat];
            decimal staked = seat.MainBet;

            if (seat.LastRoundReturned > staked)
            {
                bool blackjack = seat.Hands.Count > 0 && seat.Hands[0].IsBlackjack;

                if (blackjack)
                {
                    CasinoAudio.Instance.Blackjack();
                    ShowMessage("¡BLACKJACK!  +" + seat.LastRoundReturned.ToString("0.##"), 4f);
                }
                else
                {
                    CasinoAudio.Instance.Win();
                    ShowMessage("¡Ganas " + seat.LastRoundReturned.ToString("0.##") + "!", 3f);
                }
            }
            else if (seat.LastRoundReturned == staked && staked > 0m)
            {
                ShowMessage("Empate", 2.5f);
            }
            else if (staked > 0m)
            {
                CasinoAudio.Instance.Lose();
                ShowMessage("Esta va para la casa", 2.5f);
            }
        }

        private void RenderDealer(TableSnapshot snapshot)
        {
            int visible = snapshot.DealerCards.Count + (snapshot.DealerHasHoleCard ? 1 : 0);

            while (_dealerCards.Count < visible)
            {
                _dealerCards.Add(CardView.Create(_dealerHands, "DealerCard" + _dealerCards.Count));
            }

            float span = (visible - 1) * TableTheme.CardOverlapX;
            float startX = -span * 0.5f;

            for (int i = 0; i < _dealerCards.Count; i++)
            {
                CardView card = _dealerCards[i];

                if (i >= visible)
                {
                    card.gameObject.SetActive(false);
                    continue;
                }

                bool isNew = !card.gameObject.activeSelf;
                bool wasFaceDown = card.IsFaceDown;
                card.gameObject.SetActive(true);

                if (i < snapshot.DealerCards.Count)
                {
                    Card value = Card.FromId(snapshot.DealerCards[i]);

                    if (wasFaceDown && !isNew)
                    {
                        card.FlipTo(value);
                        CasinoAudio.Instance.Flip();
                    }
                    else
                    {
                        card.SetCard(value);
                    }
                }
                else
                {
                    card.SetFaceDown();
                }

                var target = new Vector2(startX + i * TableTheme.CardOverlapX, 0f);
                float tilt = (i - (visible - 1) * 0.5f) * 1.8f;

                if (isNew) card.DealFrom(ShoeMouth, target, tilt, _dealClock.Next());
                else card.MoveTo(target, tilt);
            }

            bool hasCards = snapshot.DealerCards.Count > 0;
            _dealerBadge.enabled = hasCards;
            _dealerBadgeText.enabled = hasCards;

            if (!hasCards) return;

            _dealerBadgeText.text = snapshot.DealerVisibleSoft && snapshot.DealerVisibleTotal <= 21
                ? (snapshot.DealerVisibleTotal - 10) + "/" + snapshot.DealerVisibleTotal
                : snapshot.DealerVisibleTotal.ToString();
        }

        private static string BuildActionsKey(TableSnapshot snapshot, int mySeat)
        {
            var key = new StringBuilder();
            key.Append(snapshot.Phase).Append('|').Append(mySeat).Append('|').Append(snapshot.CurrentSeat);

            if (snapshot.LegalActions != null)
            {
                foreach (PlayerAction action in snapshot.LegalActions) key.Append(action).Append(',');
            }

            if (mySeat >= 0 && mySeat < snapshot.Seats.Count)
            {
                key.Append('|').Append(snapshot.Seats[mySeat].MainBet);
            }

            return key.ToString();
        }

        /// <summary>
        /// Rehace la botonera según la fase. Se reconstruye entera en vez de
        /// ocultar botones: son pocos y así no hay estados intermedios raros.
        /// </summary>
        private void RebuildActions(TableSnapshot snapshot, int mySeat)
        {
            for (int i = _actionBar.childCount - 1; i >= 0; i--)
            {
                Destroy(_actionBar.GetChild(i).gameObject);
            }

            // Los botones se destruyeron: las referencias apuntan a objetos
            // muertos y hay que soltarlas antes de volver a poblarlas.
            for (int i = 0; i < _betBoxes.Length; i++)
            {
                _betBoxes[i] = null;
                _betAmounts[i] = null;
            }

            if (mySeat < 0)
            {
                UIFactory.Label("Hint", _actionBar, "Siéntate en un asiento libre para jugar", 22,
                    new Color(1f, 1f, 1f, 0.75f));
                return;
            }

            switch (snapshot.Phase)
            {
                case TablePhase.Betting:
                    BuildBettingControls(snapshot, mySeat);
                    break;

                case TablePhase.Insurance:
                    BuildInsuranceControls();
                    break;

                case TablePhase.PlayerTurns:
                    BuildTurnControls(snapshot, mySeat);
                    break;

                default:
                    UIFactory.Label("Hint", _actionBar, PhaseName(snapshot.Phase), 22,
                        new Color(1f, 1f, 1f, 0.75f));
                    break;
            }
        }

        private void BuildBettingControls(TableSnapshot snapshot, int mySeat)
        {
            decimal placed = snapshot.Seats[mySeat].MainBet;

            // Con varios asientos hay que dejar claro en cuál se está apostando.
            if (_mySeats.Count > 1)
            {
                Text which = UIFactory.Label("Which", _actionBar,
                    $"Asiento {mySeat}  ·  toca otro para cambiar", 15,
                    new Color(0.45f, 0.90f, 0.55f), TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.Place(which.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f),
                    new Vector2(600f, 20f));
            }

            // Rack de fichas.
            float x = 58f;

            foreach (decimal denomination in Denominations)
            {
                decimal value = denomination;

                RectTransform chip = UIFactory.Rect("Chip" + denomination, _actionBar);
                UIFactory.Place(chip, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(64f, 64f));

                var image = chip.gameObject.AddComponent<Image>();
                image.sprite = SpriteFactory.Chip(64, TableTheme.ChipColor(value));

                var button = chip.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => AddChip(value));

                Text label = UIFactory.Label("Value", chip, value.ToString("0"), 19,
                    TableTheme.ChipTextColor(value), TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.Stretch(label.rectTransform);

                x += 68f;
            }

            // Tres destinos: la apuesta principal y las dos laterales. Se elige
            // uno y las fichas que se toquen van ahí.
            BuildBetBox(0, BetTarget.Main, "APUESTA", string.Empty, 380f);
            BuildBetBox(1, BetTarget.PerfectPairs, "PAREJA", "hasta 25:1", 540f);
            BuildBetBox(2, BetTarget.TwentyOnePlus3, "21+3", "hasta 100:1", 700f);

            if (!snapshot.Rules.SideBetsEnabled)
            {
                _betBoxes[1].GetComponent<Button>().interactable = false;
                _betBoxes[2].GetComponent<Button>().interactable = false;
            }

            Button clear = UIFactory.Button("Clear", _actionBar, "Limpiar", () =>
            {
                _pendingMain = 0m;
                _pendingPairs = 0m;
                _pendingTrio = 0m;
                CasinoAudio.Instance.Click();
                RefreshBetBoxes();
            }, 18);
            UIFactory.Place(clear.GetComponent<RectTransform>(), new Vector2(1f, 0.5f),
                new Vector2(-545f, 0f), new Vector2(120f, 54f));

            Button bet = UIFactory.Button("PlaceBet", _actionBar,
                placed > 0m ? "Cambiar" : "Apostar", () =>
                {
                    if (_pendingMain <= 0m)
                    {
                        ShowMessage("La apuesta principal es obligatoria", 2.5f);
                        return;
                    }

                    CasinoAudio.Instance.Chip();
                    BetRequested?.Invoke(mySeat, _pendingMain, _pendingPairs, _pendingTrio);
                });
            UIFactory.Place(bet.GetComponent<RectTransform>(), new Vector2(1f, 0.5f),
                new Vector2(-395f, 0f), new Vector2(160f, 58f));
            Tint(bet, TableTheme.ButtonAccent);

            // "Listo" salta la espera si todos los que apostaron lo pulsan.
            bool ready = snapshot.Seats[mySeat].IsReady;

            Button readyButton = UIFactory.Button("Ready", _actionBar,
                ready ? "Listo ✓" : "Listo", () =>
                {
                    if (placed <= 0m)
                    {
                        ShowMessage("Apuesta antes de marcarte listo", 2f);
                        return;
                    }

                    CasinoAudio.Instance.Click();
                    ReadyRequested?.Invoke();
                }, 17);
            UIFactory.Place(readyButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f),
                new Vector2(-215f, 0f), new Vector2(130f, 54f));

            if (ready)
            {
                Tint(readyButton, TableTheme.ButtonAccent);
                readyButton.interactable = false;
            }

            // Con varios asientos, se deja solo el activo; con uno, se sale.
            Button leave = UIFactory.Button("StandUp", _actionBar,
                _mySeats.Count > 1 ? "Dejar" : "Salir", () =>
                {
                    CasinoAudio.Instance.Click();

                    if (_mySeats.Count > 1) StandUpSeatRequested?.Invoke(mySeat);
                    else StandUpRequested?.Invoke();
                }, 17);
            UIFactory.Place(leave.GetComponent<RectTransform>(), new Vector2(1f, 0.5f),
                new Vector2(-70f, 0f), new Vector2(100f, 54f));

            RefreshBetBoxes();
        }

        /// <summary>
        /// Caja de destino de apuesta: título, pago máximo e importe puesto.
        /// </summary>
        private void BuildBetBox(int index, BetTarget target, string caption, string payout, float x)
        {
            RectTransform box = UIFactory.Rect("Bet" + target, _actionBar);
            UIFactory.Place(box, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(150f, 78f));

            var image = box.gameObject.AddComponent<Image>();
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                _target = target;
                CasinoAudio.Instance.Click();
                RefreshBetBoxes();
            });

            Text title = UIFactory.Label("Caption", box, caption, 15,
                new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f),
                new Vector2(160f, 20f));

            if (!string.IsNullOrEmpty(payout))
            {
                Text pays = UIFactory.Label("Payout", box, payout, 12,
                    TableTheme.GoldText, TextAnchor.MiddleCenter);
                UIFactory.Place(pays.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f),
                    new Vector2(160f, 16f));
            }

            Text amount = UIFactory.Label("Amount", box, "—", 24,
                Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(amount.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 22f),
                new Vector2(160f, 30f));

            _betBoxes[index] = image;
            _betAmounts[index] = amount;
        }

        private void RefreshBetBoxes()
        {
            decimal[] values = { _pendingMain, _pendingPairs, _pendingTrio };
            var targets = new[] { BetTarget.Main, BetTarget.PerfectPairs, BetTarget.TwentyOnePlus3 };

            for (int i = 0; i < _betBoxes.Length; i++)
            {
                if (_betBoxes[i] == null) continue;

                bool active = _target == targets[i];

                _betBoxes[i].sprite = SpriteFactory.RoundedRect(168, 78, 12,
                    active ? new Color(0.16f, 0.24f, 0.20f) : new Color(0.12f, 0.13f, 0.16f),
                    active ? TableTheme.SpotLine : new Color(1f, 1f, 1f, 0.16f),
                    active ? 3 : 2);

                _betAmounts[i].text = values[i] > 0m ? values[i].ToString("0.##") : "—";
                _betAmounts[i].color = values[i] > 0m ? TableTheme.GoldText : new Color(1f, 1f, 1f, 0.35f);
            }
        }

        /// <summary>
        /// Suma una ficha al destino elegido. Es como se apuesta en una mesa:
        /// se van dejando fichas hasta la cantidad que se quiere jugar.
        /// </summary>
        private void AddChip(decimal denomination)
        {
            decimal max = _snapshot?.Rules?.MaxBet ?? 500m;
            decimal committed = _pendingMain + _pendingPairs + _pendingTrio;

            if (committed + denomination > _balance)
            {
                ShowMessage("No te llega el saldo", 2f);
                return;
            }

            switch (_target)
            {
                case BetTarget.Main:
                    if (_pendingMain + denomination > max)
                    {
                        ShowMessage($"El máximo de la mesa es {max:0.##}", 2f);
                        return;
                    }
                    _pendingMain += denomination;
                    break;

                case BetTarget.PerfectPairs:
                    _pendingPairs += denomination;
                    break;

                case BetTarget.TwentyOnePlus3:
                    _pendingTrio += denomination;
                    break;
            }

            CasinoAudio.Instance.Chip();
            RefreshBetBoxes();
        }

        private void BuildInsuranceControls()
        {
            Text hint = UIFactory.Label("Hint", _actionBar,
                "El croupier enseña un As. ¿Quieres seguro?", 22, Color.white, TextAnchor.MiddleLeft);
            UIFactory.Place(hint.rectTransform, new Vector2(0f, 0.5f), new Vector2(400f, 0f),
                new Vector2(560f, 30f));

            Button yes = UIFactory.Button("Insure", _actionBar, "Asegurar", () =>
            {
                CasinoAudio.Instance.Chip();
                InsuranceAnswered?.Invoke(true);
            });
            UIFactory.Place(yes.GetComponent<RectTransform>(), new Vector2(1f, 0.5f),
                new Vector2(-420f, 0f), new Vector2(190f, 56f));
            Tint(yes, TableTheme.ButtonAccent);

            Button no = UIFactory.Button("Decline", _actionBar, "No, gracias", () =>
            {
                CasinoAudio.Instance.Click();
                InsuranceAnswered?.Invoke(false);
            });
            UIFactory.Place(no.GetComponent<RectTransform>(), new Vector2(1f, 0.5f),
                new Vector2(-200f, 0f), new Vector2(190f, 56f));
        }

        private void BuildTurnControls(TableSnapshot snapshot, int mySeat)
        {
            // El turno puede caer en cualquiera de tus asientos, no solo en el
            // que tengas seleccionado para apostar.
            if (!_mySeats.Contains(snapshot.CurrentSeat))
            {
                UIFactory.Label("Hint", _actionBar,
                    $"Juega el asiento {snapshot.CurrentSeat}...", 22,
                    new Color(1f, 1f, 1f, 0.75f));
                return;
            }

            if (_mySeats.Count > 1)
            {
                Text which = UIFactory.Label("Which", _actionBar,
                    $"Tu asiento {snapshot.CurrentSeat}", 15,
                    new Color(0.45f, 0.90f, 0.55f), TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.Place(which.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f),
                    new Vector2(400f, 20f));
            }

            if (snapshot.LegalActions == null || snapshot.LegalActions.Count == 0) return;

            float total = snapshot.LegalActions.Count * 190f;
            float x = -total * 0.5f + 95f;

            foreach (PlayerAction action in snapshot.LegalActions)
            {
                PlayerAction chosen = action;

                Button button = UIFactory.Button("Action" + action, _actionBar, ActionName(action), () =>
                {
                    CasinoAudio.Instance.Click();
                    ActionRequested?.Invoke(chosen);
                });
                UIFactory.Place(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                    new Vector2(x, 0f), new Vector2(178f, 62f));

                if (action == PlayerAction.Hit) Tint(button, TableTheme.ButtonAccent);
                else if (action == PlayerAction.Surrender) Tint(button, TableTheme.ButtonDanger);

                x += 190f;
            }
        }

        private static void Tint(Button button, Color color)
        {
            var image = button.GetComponent<Image>();
            image.sprite = SpriteFactory.RoundedRect(180, 60, 12, color, new Color(1f, 1f, 1f, 0.25f), 2);
        }

        private void Update()
        {
            if (_snapshot?.DeadlineUtc != null)
            {
                double remaining = (_snapshot.DeadlineUtc.Value - DateTime.UtcNow).TotalSeconds;
                if (remaining < 0) remaining = 0;

                _timerLabel.text = remaining.ToString("0") + "s";

                // Los últimos segundos parpadean: presión sin necesidad de aviso.
                if (remaining <= 5)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
                    _timerLabel.color = Color.Lerp(Color.white, TableTheme.LoseRed, pulse);
                }
                else
                {
                    _timerLabel.color = Color.white;
                }
            }
            else
            {
                _timerLabel.text = string.Empty;
            }

            if (_messageUntil > 0f && Time.time > _messageUntil)
            {
                _messagePanel.gameObject.SetActive(false);
                _messageLabel.text = string.Empty;
                _messageUntil = 0f;
            }
        }

        /// <summary>
        /// Actualiza qué asientos ocupas. Lo dice el servidor por un canal
        /// propio: antes se deducía del nombre visible, y eso se rompía en
        /// cuanto dos jugadores coincidían de nombre.
        /// </summary>
        public void SetMySeats(List<int> seats)
        {
            _mySeats.Clear();
            if (seats != null) _mySeats.AddRange(seats);

            // Si el asiento activo dejó de ser tuyo, se pasa al primero que sí.
            if (!_mySeats.Contains(_activeSeat))
            {
                _activeSeat = _mySeats.Count > 0 ? _mySeats[0] : -1;
            }

            _actionsKey = string.Empty; // fuerza rehacer la botonera
            if (_snapshot != null) ApplySnapshot(_snapshot);
        }

        /// <summary>Asiento sobre el que actúan los botones de apuesta.</summary>
        private int ActiveSeat => _activeSeat >= 0 ? _activeSeat
            : _mySeats.Count > 0 ? _mySeats[0] : -1;

        /// <summary>Asientos que ocupas, para quien necesite recorrerlos.</summary>
        public IReadOnlyList<int> MySeats => _mySeats;

        public void OnRoundEvents(List<RoundEventDto> events)
        {
            foreach (RoundEventDto e in events)
            {
                switch (e.Type)
                {
                    case RoundEventType.DealerBlackjack:
                        ShowMessage("¡Blackjack del croupier!", 3f);
                        break;

                    case RoundEventType.SideBetResolved:
                        ShowMessage($"{e.Label}  +{e.Amount:0.##}", 3f);
                        break;

                    case RoundEventType.HandSplit:
                        ShowMessage("Mano partida", 2f);
                        break;
                }
            }
        }

        private static string ActionName(PlayerAction action)
        {
            switch (action)
            {
                case PlayerAction.Hit: return "Pedir";
                case PlayerAction.Stand: return "Plantarse";
                case PlayerAction.Double: return "Doblar";
                case PlayerAction.Split: return "Partir";
                case PlayerAction.Surrender: return "Rendirse";
                default: return action.ToString();
            }
        }

        private static string PhaseName(TablePhase phase)
        {
            switch (phase)
            {
                case TablePhase.WaitingForPlayers: return "Esperando jugadores";
                case TablePhase.Betting: return "Hagan sus apuestas";
                case TablePhase.Dealing: return "Repartiendo";
                case TablePhase.Insurance: return "Seguro";
                case TablePhase.PlayerTurns: return "Turnos";
                case TablePhase.DealerPlay: return "Juega el croupier";
                case TablePhase.Payout: return "Pagos";
                default: return phase.ToString();
            }
        }
    }
}
