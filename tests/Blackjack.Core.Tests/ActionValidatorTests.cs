using System.Linq;
using Blackjack.Core.Hands;
using Blackjack.Core.Rules;
using Xunit;
using static Blackjack.Core.Tests.TestCards;

namespace Blackjack.Core.Tests
{
    public class ActionValidatorTests
    {
        private static readonly TableRules Vegas = TableRules.VegasStrip;

        [Fact]
        public void ManoNormalDeDosCartas_PermiteTodo()
        {
            PlayerHand mano = MakePlayerHand(100m, "8s", "8h");

            var acciones = ActionValidator.LegalActions(mano, Vegas, availableBalance: 1000m, handCountInSeat: 1);

            Assert.Contains(PlayerAction.Hit, acciones);
            Assert.Contains(PlayerAction.Stand, acciones);
            Assert.Contains(PlayerAction.Double, acciones);
            Assert.Contains(PlayerAction.Split, acciones);
            Assert.Contains(PlayerAction.Surrender, acciones);
        }

        [Fact]
        public void ConTresCartas_YaNoSePuedeDoblarNiPartirNiRendirse()
        {
            PlayerHand mano = MakePlayerHand(100m, "5s", "4h", "3c"); // 12

            var acciones = ActionValidator.LegalActions(mano, Vegas, 1000m, 1);

            Assert.Contains(PlayerAction.Hit, acciones);
            Assert.Contains(PlayerAction.Stand, acciones);
            Assert.DoesNotContain(PlayerAction.Double, acciones);
            Assert.DoesNotContain(PlayerAction.Split, acciones);
            Assert.DoesNotContain(PlayerAction.Surrender, acciones);
        }

        [Fact]
        public void SinSaldoSuficiente_NoSePuedeDoblarNiPartir()
        {
            PlayerHand mano = MakePlayerHand(100m, "8s", "8h");

            // Solo quedan 50: no cubre la apuesta adicional de 100.
            var acciones = ActionValidator.LegalActions(mano, Vegas, availableBalance: 50m, handCountInSeat: 1);

            Assert.DoesNotContain(PlayerAction.Double, acciones);
            Assert.DoesNotContain(PlayerAction.Split, acciones);
            Assert.Contains(PlayerAction.Hit, acciones);
            Assert.Contains(PlayerAction.Stand, acciones);
        }

        [Fact]
        public void AsesPartidos_RecibenUnaSolaCartaYSePlantanSolos()
        {
            var mano = new PlayerHand(100m, splitDepth: 1, isFromSplit: true, isSplitAces: true);
            mano.Deal(C("As"));

            Assert.False(mano.IsFinished);

            mano.Deal(C("9h")); // 20

            Assert.True(mano.IsFinished);
            Assert.Empty(ActionValidator.LegalActions(mano, Vegas, 1000m, 2));
        }

        [Fact]
        public void AsesPartidos_NoPermitenPedirCarta()
        {
            var mano = new PlayerHand(100m, splitDepth: 1, isFromSplit: true, isSplitAces: true);
            mano.Deal(C("As"));

            var acciones = ActionValidator.LegalActions(mano, Vegas, 1000m, 2);

            Assert.DoesNotContain(PlayerAction.Hit, acciones);
            Assert.DoesNotContain(PlayerAction.Double, acciones);
        }

        [Fact]
        public void RepartirAses_SePermiteSoloSiLaMesaLoAutoriza()
        {
            var mano = new PlayerHand(100m, splitDepth: 1, isFromSplit: true, isSplitAces: false);
            mano.Deal(C("As"));
            mano.Deal(C("Ah"));

            var sinResplit = ActionValidator.LegalActions(mano, Vegas, 1000m, 2);
            Assert.DoesNotContain(PlayerAction.Split, sinResplit);

            var conResplit = new TableRules(resplitAces: true);
            var permitido = ActionValidator.LegalActions(mano, conResplit, 1000m, 2);
            Assert.Contains(PlayerAction.Split, permitido);
        }

        [Fact]
        public void ElTopeDeSplits_SeRespeta()
        {
            // MaxSplits = 3 significa como mucho 4 manos.
            var mano = new PlayerHand(100m, splitDepth: 3, isFromSplit: true);
            mano.Deal(C("8s"));
            mano.Deal(C("8h"));

            var acciones = ActionValidator.LegalActions(mano, Vegas, 1000m, handCountInSeat: 4);

            Assert.DoesNotContain(PlayerAction.Split, acciones);
        }

        [Fact]
        public void SinDAS_NoSePuedeDoblarTrasPartir()
        {
            var sinDas = new TableRules(doubleAfterSplit: false);
            PlayerHand mano = MakeSplitHand(100m, splitAces: false, "5s", "6h"); // 11

            Assert.DoesNotContain(PlayerAction.Double, ActionValidator.LegalActions(mano, sinDas, 1000m, 2));
            Assert.Contains(PlayerAction.Double, ActionValidator.LegalActions(mano, Vegas, 1000m, 2));
        }

        [Fact]
        public void ManoPartida_NoSePuedeRendir()
        {
            PlayerHand mano = MakeSplitHand(100m, splitAces: false, "Ts", "6h");

            Assert.DoesNotContain(PlayerAction.Surrender, ActionValidator.LegalActions(mano, Vegas, 1000m, 2));
        }

        [Theory]
        [InlineData("5s", "4h", true)]   // 9  → sí
        [InlineData("5s", "6h", true)]   // 11 → sí
        [InlineData("5s", "3h", false)]  // 8  → no
        [InlineData("Ts", "2h", false)]  // 12 → no
        public void ReglaNueveAOnce_LimitaElDoblado(string a, string b, bool permitido)
        {
            var europea = new TableRules(doubleRule: DoubleRule.NineToEleven);
            PlayerHand mano = MakePlayerHand(100m, a, b);

            var acciones = ActionValidator.LegalActions(mano, europea, 1000m, 1);

            Assert.Equal(permitido, acciones.Contains(PlayerAction.Double));
        }

        [Fact]
        public void Con21_NoSeOfrecePedirCarta()
        {
            PlayerHand mano = MakePlayerHand(100m, "As", "Kh");

            // La mano se planta sola al llegar a 21.
            Assert.True(mano.IsFinished);
            Assert.Empty(ActionValidator.LegalActions(mano, Vegas, 1000m, 1));
        }

        [Fact]
        public void PartirPorFigura_EsMasEstrictoQuePorValor()
        {
            PlayerHand kq = MakePlayerHand(100m, "Kh", "Qs");

            Assert.Contains(PlayerAction.Split, ActionValidator.LegalActions(kq, Vegas, 1000m, 1));

            var porFigura = new TableRules(splitByExactRank: true);
            Assert.DoesNotContain(PlayerAction.Split, ActionValidator.LegalActions(kq, porFigura, 1000m, 1));
        }

        [Fact]
        public void IsLegal_CoincideConLegalActions()
        {
            PlayerHand mano = MakePlayerHand(100m, "8s", "8h");
            var acciones = ActionValidator.LegalActions(mano, Vegas, 1000m, 1);

            foreach (PlayerAction accion in System.Enum.GetValues(typeof(PlayerAction)))
            {
                bool esperado = acciones.Contains(accion);
                bool real = ActionValidator.IsLegal(accion, mano, Vegas, 1000m, 1);
                Assert.Equal(esperado, real);
            }
        }
    }
}
