using System;
using UnityEngine;
using UnityEngine.UI;

namespace Blackjack.Client.UI
{
    /// <summary>
    /// Tabla de estrategia básica.
    ///
    /// Los valores son los de ESTA mesa: 6 barajas, el croupier se planta con
    /// 17 blando y se puede doblar tras partir. Las tablas de un solo mazo que
    /// circulan por ahí cambian en varias casillas, así que copiarlas daría
    /// consejos ligeramente equivocados en la mesa que se está jugando.
    ///
    /// Seguirla al pie de la letra deja la ventaja de la casa en torno al
    /// 0.4%. No la elimina: no existe forma de jugar que lo haga.
    /// </summary>
    public sealed class StrategyGuideView : MonoBehaviour
    {
        private enum Play
        {
            Hit,          // Pedir
            Stand,        // Quedarse
            Double,       // Doblar; si no se permite, pedir
            DoubleStand,  // Doblar; si no se permite, quedarse
            Split,        // Partir
            Surrender     // Rendirse; si no se permite, pedir
        }

        private static readonly string[] DealerCards = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "A" };

        // H=Pedir  S=Quedarse  D=Doblar  d=Doblar/Quedarse  P=Partir  R=Rendirse
        private static readonly (string Label, string Row)[] HardHands =
        {
            ("8 o menos", "HHHHHHHHHH"),
            ("9",         "HDDDDHHHHH"),
            ("10",        "DDDDDDDDHH"),
            ("11",        "DDDDDDDDDH"),
            ("12",        "HHSSSHHHHH"),
            ("13",        "SSSSSHHHHH"),
            ("14",        "SSSSSHHHHH"),
            ("15",        "SSSSSHHHRH"),
            ("16",        "SSSSSHHRRR"),
            ("17 o más",  "SSSSSSSSSS")
        };

        private static readonly (string Label, string Row)[] SoftHands =
        {
            ("A,2", "HHHDDHHHHH"),
            ("A,3", "HHHDDHHHHH"),
            ("A,4", "HHDDDHHHHH"),
            ("A,5", "HHDDDHHHHH"),
            ("A,6", "HDDDDHHHHH"),
            ("A,7", "SddddSSHHH"),
            ("A,8", "SSSSSSSSSS"),
            ("A,9", "SSSSSSSSSS")
        };

        private static readonly (string Label, string Row)[] Pairs =
        {
            ("2,2",   "PPPPPPHHHH"),
            ("3,3",   "PPPPPPHHHH"),
            ("4,4",   "HHHPPHHHHH"),
            ("5,5",   "DDDDDDDDHH"),
            ("6,6",   "PPPPPHHHHH"),
            ("7,7",   "PPPPPPHHHH"),
            ("8,8",   "PPPPPPPPPP"),
            ("9,9",   "PPPPPSPPSS"),
            ("10,10", "SSSSSSSSSS"),
            ("A,A",   "PPPPPPPPPP")
        };

        private const float CellWidth = 44f;
        private const float CellHeight = 26f;
        private const float LabelWidth = 88f;

        public static StrategyGuideView Create(Transform canvas)
        {
            RectTransform rect = UIFactory.Rect("StrategyGuide", canvas);
            UIFactory.Stretch(rect);

            var view = rect.gameObject.AddComponent<StrategyGuideView>();
            view.Build();
            return view;
        }

        private void Build()
        {
            var root = (RectTransform)transform;

            // Velo oscuro: cierra al tocar fuera y aísla la tabla de la mesa.
            Image veil = UIFactory.Panel("Veil", root, SpriteFactory.Solid(new Color(0f, 0f, 0f, 0.72f)));
            UIFactory.Stretch(veil.rectTransform);
            veil.raycastTarget = true;

            var veilButton = veil.gameObject.AddComponent<Button>();
            veilButton.targetGraphic = veil;
            veilButton.onClick.AddListener(Close);

            float tableWidth = LabelWidth + DealerCards.Length * CellWidth;
            const float panelWidth = 660f;
            const float panelHeight = 980f;

            Image panel = UIFactory.Panel("Panel", root,
                SpriteFactory.RoundedRect(660, 980, 18, new Color(0.10f, 0.11f, 0.14f, 0.99f),
                    new Color(1f, 1f, 1f, 0.18f), 2));
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(panelWidth, panelHeight));
            UIFactory.AddShadow(panel, new Vector2(0f, -8f), 0.6f);
            panel.raycastTarget = true;

            Text title = UIFactory.Label("Title", panel.rectTransform, "ESTRATEGIA BÁSICA", 26,
                TableTheme.GoldText, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -34f),
                new Vector2(600f, 32f));

            Text subtitle = UIFactory.Label("Subtitle", panel.rectTransform,
                "6 barajas · el croupier se planta con 17 blando · se puede doblar tras partir", 13,
                new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter);
            UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f),
                new Vector2(620f, 20f));

            BuildLegend(panel.rectTransform, -88f);

            float y = -132f;
            y = BuildSection(panel.rectTransform, "Carta del croupier", null, y, true);
            y = BuildSection(panel.rectTransform, "MANOS DURAS", HardHands, y, false);
            y = BuildSection(panel.rectTransform, "MANOS BLANDAS", SoftHands, y, false);
            y = BuildSection(panel.rectTransform, "PAREJAS", Pairs, y, false);

            Text warning = UIFactory.Label("Warning", panel.rectTransform,
                "El seguro es siempre mala apuesta. No lo tomes.", 15,
                new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(warning.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y - 18f),
                new Vector2(620f, 24f));

            Button close = UIFactory.Button("Close", panel.rectTransform, "Cerrar", Close, 18);
            UIFactory.Place(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0f, 36f), new Vector2(180f, 50f));
        }

        private static void BuildLegend(Transform parent, float y)
        {
            var entries = new (Play Play, string Text)[]
            {
                (Play.Hit, "Pedir"),
                (Play.Stand, "Quedarse"),
                (Play.Double, "Doblar"),
                (Play.Split, "Partir"),
                (Play.Surrender, "Rendirse")
            };

            float x = -260f;

            foreach ((Play play, string text) in entries)
            {
                Image swatch = UIFactory.Panel("Swatch", parent,
                    SpriteFactory.RoundedRect(18, 18, 4, ColorFor(play)));
                UIFactory.Place(swatch.rectTransform, new Vector2(0.5f, 1f), new Vector2(x, y),
                    new Vector2(18f, 18f));

                Text label = UIFactory.Label("Text", parent, text, 13,
                    new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleLeft);
                UIFactory.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(x + 52f, y),
                    new Vector2(80f, 18f));

                x += 118f;
            }
        }

        /// <summary>
        /// Dibuja un bloque de la tabla y devuelve la altura donde continuar.
        /// </summary>
        private static float BuildSection(Transform parent, string caption,
            (string Label, string Row)[] rows, float y, bool headerOnly)
        {
            Text heading = UIFactory.Label("Heading", parent, caption, 14,
                TableTheme.GoldText, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place(heading.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(-230f, y), new Vector2(300f, 20f));

            y -= 22f;

            if (headerOnly)
            {
                // Fila con las cartas descubiertas del croupier.
                for (int c = 0; c < DealerCards.Length; c++)
                {
                    float cx = ColumnX(c);

                    Image cell = UIFactory.Panel("Head" + c, parent,
                        SpriteFactory.RoundedRect((int)CellWidth - 3, (int)CellHeight - 3, 4,
                            new Color(0.20f, 0.22f, 0.27f)));
                    UIFactory.Place(cell.rectTransform, new Vector2(0.5f, 1f), new Vector2(cx, y),
                        new Vector2(CellWidth - 3f, CellHeight - 3f));

                    Text text = UIFactory.Label("Text", cell.rectTransform, DealerCards[c], 15,
                        Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                    UIFactory.Stretch(text.rectTransform);
                }

                return y - CellHeight - 6f;
            }

            foreach ((string label, string row) in rows)
            {
                Text rowLabel = UIFactory.Label("Label", parent, label, 13,
                    new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleRight, FontStyle.Bold);
                UIFactory.Place(rowLabel.rectTransform, new Vector2(0.5f, 1f),
                    new Vector2(-LabelWidth * 0.5f - 176f, y), new Vector2(LabelWidth, CellHeight));

                for (int c = 0; c < row.Length && c < DealerCards.Length; c++)
                {
                    Play play = Parse(row[c]);
                    float cx = ColumnX(c);

                    Image cell = UIFactory.Panel("Cell", parent,
                        SpriteFactory.RoundedRect((int)CellWidth - 3, (int)CellHeight - 3, 4, ColorFor(play)));
                    UIFactory.Place(cell.rectTransform, new Vector2(0.5f, 1f), new Vector2(cx, y),
                        new Vector2(CellWidth - 3f, CellHeight - 3f));

                    Text text = UIFactory.Label("Text", cell.rectTransform, Symbol(play), 13,
                        TextColorFor(play), TextAnchor.MiddleCenter, FontStyle.Bold);
                    UIFactory.Stretch(text.rectTransform);
                }

                y -= CellHeight;
            }

            return y - 10f;
        }

        private static float ColumnX(int column)
            => -132f + column * CellWidth;

        private static Play Parse(char code)
        {
            switch (code)
            {
                case 'H': return Play.Hit;
                case 'S': return Play.Stand;
                case 'D': return Play.Double;
                case 'd': return Play.DoubleStand;
                case 'P': return Play.Split;
                case 'R': return Play.Surrender;
                default: return Play.Hit;
            }
        }

        private static string Symbol(Play play)
        {
            switch (play)
            {
                case Play.Hit: return "P";
                case Play.Stand: return "Q";
                case Play.Double: return "D";
                case Play.DoubleStand: return "D/Q";
                case Play.Split: return "S";
                case Play.Surrender: return "R";
                default: return "?";
            }
        }

        private static Color ColorFor(Play play)
        {
            switch (play)
            {
                case Play.Hit: return new Color(0.16f, 0.66f, 0.30f);
                case Play.Stand: return new Color(0.82f, 0.20f, 0.22f);
                case Play.Double: return new Color(0.20f, 0.52f, 0.86f);
                case Play.DoubleStand: return new Color(0.12f, 0.30f, 0.72f);
                case Play.Split: return new Color(0.55f, 0.32f, 0.80f);
                case Play.Surrender: return new Color(0.92f, 0.78f, 0.20f);
                default: return Color.gray;
            }
        }

        private static Color TextColorFor(Play play)
            => play == Play.Surrender ? new Color(0.15f, 0.13f, 0.05f) : Color.white;

        public void Close() => Destroy(gameObject);
    }
}
