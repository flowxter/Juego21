using System;

namespace Blackjack.Core.Rules
{
    /// <summary>
    /// Configuración de una mesa. Inmutable: se fija al crear la sala y se
    /// muestra impresa en el fieltro, igual que en una mesa real.
    ///
    /// Cada opción mueve la ventaja de la casa. Los presets de abajo son
    /// combinaciones reales, no inventadas: si mezclas valores a ojo puedes
    /// acabar con una mesa matemáticamente favorable al jugador.
    /// </summary>
    public sealed class TableRules
    {
        public TableRules(
            int deckCount = 6,
            double penetration = 0.75,
            bool dealerHitsSoft17 = false,
            decimal blackjackPayout = 1.5m,
            decimal insurancePayout = 2.0m,
            int maxSplits = 3,
            bool doubleAfterSplit = true,
            DoubleRule doubleRule = DoubleRule.AnyTwoCards,
            bool resplitAces = false,
            bool hitSplitAces = false,
            bool lateSurrender = true,
            HoleCardRule holeCardRule = HoleCardRule.AmericanPeek,
            bool splitByExactRank = false,
            decimal minBet = 1m,
            decimal maxBet = 500m,
            bool sideBetsEnabled = true)
        {
            if (deckCount < 1 || deckCount > 8)
                throw new ArgumentOutOfRangeException(nameof(deckCount), deckCount, "Entre 1 y 8 barajas.");
            if (maxSplits < 0 || maxSplits > 3)
                throw new ArgumentOutOfRangeException(nameof(maxSplits), maxSplits, "Entre 0 y 3 splits (hasta 4 manos).");
            if (blackjackPayout <= 0m)
                throw new ArgumentOutOfRangeException(nameof(blackjackPayout), blackjackPayout, "El pago de blackjack debe ser positivo.");
            if (minBet <= 0m || maxBet < minBet)
                throw new ArgumentException("Los límites de apuesta no son coherentes.", nameof(minBet));

            DeckCount = deckCount;
            Penetration = penetration;
            DealerHitsSoft17 = dealerHitsSoft17;
            BlackjackPayout = blackjackPayout;
            InsurancePayout = insurancePayout;
            MaxSplits = maxSplits;
            DoubleAfterSplit = doubleAfterSplit;
            DoubleRule = doubleRule;
            ResplitAces = resplitAces;
            HitSplitAces = hitSplitAces;
            LateSurrender = lateSurrender;
            HoleCardRule = holeCardRule;
            SplitByExactRank = splitByExactRank;
            MinBet = minBet;
            MaxBet = maxBet;
            SideBetsEnabled = sideBetsEnabled;
        }

        public int DeckCount { get; }

        public double Penetration { get; }

        /// <summary>
        /// False = S17 (el dealer se planta con 17 blando). Es lo que anuncian
        /// las dos mesas de referencia: "se planta con 17" / "stand on all 17s".
        /// Pasar a H17 añade ~0.22% de ventaja a la casa.
        /// </summary>
        public bool DealerHitsSoft17 { get; }

        /// <summary>
        /// Multiplicador del blackjack sobre la apuesta. 1.5 = 3:2.
        ///
        /// No bajarlo a 1.2 (6:5): añade ~1.4% de ventaja a la casa, lo que
        /// multiplica por cuatro el margen de la mesa. Los jugadores que
        /// conocen el juego lo detectan al instante y mata la credibilidad.
        /// </summary>
        public decimal BlackjackPayout { get; }

        /// <summary>Multiplicador del seguro. 2.0 = 2:1.</summary>
        public decimal InsurancePayout { get; }

        /// <summary>Splits permitidos. 3 = hasta 4 manos simultáneas.</summary>
        public int MaxSplits { get; }

        /// <summary>DAS: permitir doblar después de partir.</summary>
        public bool DoubleAfterSplit { get; }

        public DoubleRule DoubleRule { get; }

        /// <summary>Permitir volver a partir ases. Poco común.</summary>
        public bool ResplitAces { get; }

        /// <summary>
        /// Permitir pedir carta sobre ases partidos. Casi ningún casino lo
        /// permite: los ases partidos reciben una sola carta cada uno.
        /// </summary>
        public bool HitSplitAces { get; }

        /// <summary>
        /// Rendirse tras ver la carta del dealer, recuperando la mitad de la
        /// apuesta. Solo antes de pedir, doblar o partir.
        /// </summary>
        public bool LateSurrender { get; }

        public HoleCardRule HoleCardRule { get; }

        /// <summary>
        /// Si es true, solo se parten figuras idénticas (K-K sí, K-Q no).
        /// Por defecto false: se parte por valor, como en la mayoría de mesas.
        /// </summary>
        public bool SplitByExactRank { get; }

        public decimal MinBet { get; }

        public decimal MaxBet { get; }

        public bool SideBetsEnabled { get; }

        /// <summary>
        /// Vegas Strip, 6 barajas: S17, BJ 3:2, DAS, doblar con cualquier 2,
        /// rendición tardía. Ventaja de la casa ≈ 0.40%.
        /// Es la variante de las dos mesas de referencia.
        /// </summary>
        public static TableRules VegasStrip => new TableRules();

        /// <summary>
        /// Europea: sin carta tapada, doblar solo 9-11, sin rendición.
        /// Ventaja de la casa ≈ 0.62%.
        /// </summary>
        public static TableRules European => new TableRules(
            deckCount: 6,
            doubleRule: DoubleRule.NineToEleven,
            lateSurrender: false,
            holeCardRule: HoleCardRule.EuropeanNoHoleCard);

        /// <summary>
        /// Atlantic City, 8 barajas: S17, DAS, rendición tardía.
        /// Ventaja de la casa ≈ 0.36%, de las mesas más blandas que existen.
        /// </summary>
        public static TableRules AtlanticCity => new TableRules(
            deckCount: 8,
            dealerHitsSoft17: false,
            lateSurrender: true);
    }
}
