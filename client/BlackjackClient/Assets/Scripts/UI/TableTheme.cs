using UnityEngine;

namespace Blackjack.Client.UI
{
    /// <summary>
    /// Paleta y medidas de la mesa, en un solo sitio.
    ///
    /// El tono es deliberadamente vivo y algo caricaturesco: colores saturados,
    /// contornos marcados y piezas grandes. Un casino real tira a sobrio, pero
    /// aquí se busca que apetezca jugar, no simular una sala.
    /// </summary>
    public static class TableTheme
    {
        // Fieltro
        public static readonly Color FeltCenter = new Color(0.13f, 0.55f, 0.29f);
        public static readonly Color FeltEdge = new Color(0.04f, 0.21f, 0.12f);

        // Borde acolchado de la mesa
        public static readonly Color RailOuter = new Color(0.26f, 0.13f, 0.07f);
        public static readonly Color RailInner = new Color(0.44f, 0.23f, 0.12f);

        // Arco de apuestas y rótulos
        public static readonly Color SpotLine = new Color(1f, 0.87f, 0.25f);
        public static readonly Color BannerRed = new Color(0.80f, 0.10f, 0.15f);
        public static readonly Color BannerText = new Color(1f, 0.98f, 0.92f);
        public static readonly Color GoldText = new Color(1f, 0.84f, 0.30f);

        // Cartas
        public static readonly Color CardFace = new Color(0.99f, 0.99f, 0.97f);
        public static readonly Color CardBackA = new Color(0.78f, 0.12f, 0.17f);
        public static readonly Color CardBackB = new Color(0.50f, 0.06f, 0.11f);
        public static readonly Color SuitRed = new Color(0.86f, 0.11f, 0.14f);
        public static readonly Color SuitBlack = new Color(0.11f, 0.11f, 0.14f);

        // Badge de total, como los cuadros blancos de las mesas de referencia
        public static readonly Color BadgeFill = new Color(1f, 1f, 1f);
        public static readonly Color BadgeText = new Color(0.10f, 0.10f, 0.13f);
        public static readonly Color BadgeBlackjack = new Color(0.12f, 0.12f, 0.15f);

        // Panel inferior y botones
        public static readonly Color PanelFill = new Color(0.05f, 0.06f, 0.09f, 0.88f);
        public static readonly Color ButtonFill = new Color(0.18f, 0.20f, 0.25f);
        public static readonly Color ButtonAccent = new Color(0.16f, 0.55f, 0.32f);
        public static readonly Color ButtonDanger = new Color(0.55f, 0.18f, 0.20f);

        public static readonly Color TurnGlow = new Color(1f, 0.87f, 0.32f);
        public static readonly Color WinGreen = new Color(0.40f, 0.90f, 0.48f);
        public static readonly Color LoseRed = new Color(0.93f, 0.36f, 0.33f);

        /// <summary>Colores de ficha por valor, como en un rack de casino.</summary>
        public static Color ChipColor(decimal denomination)
        {
            if (denomination >= 100m) return new Color(0.42f, 0.20f, 0.62f);   // morado
            if (denomination >= 25m) return new Color(0.11f, 0.58f, 0.30f);    // verde
            if (denomination >= 5m) return new Color(0.85f, 0.15f, 0.19f);     // rojo
            return new Color(0.16f, 0.38f, 0.72f);                             // azul
        }

        /// <summary>
        /// Color del valor impreso. Siempre oscuro: la ficha tiene el centro
        /// blanco, así que el número va sobre blanco pase lo que pase.
        /// </summary>
        public static Color ChipTextColor(decimal denomination)
            => new Color(0.13f, 0.13f, 0.16f);

        // Medidas base, pensadas para un lienzo de 1920x1080.
        public const float CardWidth = 104f;
        public const float CardHeight = 146f;
        public const float CardOverlapX = 36f;
        public const float CardOverlapY = 7f;
        public const float ChipSize = 58f;
        public const float SpotSize = 132f;

        /// <summary>Ficha de mayor valor que admite el rack.</summary>
        public const decimal MaxChip = 100m;
    }
}
