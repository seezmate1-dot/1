using System.Linq;
using NUnit.Framework;
using SunnyValley.Layout;

namespace SunnyValley.Ui.Tests
{
    /// <summary>The pieces of the model on their own: canvas scaling, anchor math, text measuring and fitting, formats, easing.</summary>
    public class ModelTests
    {
        [Test]
        public void TallPhonesMatchWidthTabletsMatchHeight()
        {
            var phone = new CanvasFit(ScreenSpec.WithInsets("p", 1080, 2400, 400));
            Assert.That(phone.Match, Is.EqualTo(0));
            Assert.That(phone.Scale, Is.EqualTo(1).Within(1e-4));
            Assert.That(phone.Width, Is.EqualTo(1080).Within(0.01));
            Assert.That(phone.Height, Is.EqualTo(2400).Within(0.01));

            var hd = new CanvasFit(ScreenSpec.WithInsets("hd", 720, 1280, 320));
            Assert.That(hd.Match, Is.EqualTo(0));
            Assert.That(hd.Width, Is.EqualTo(1080).Within(0.01));
            Assert.That(hd.Height, Is.EqualTo(1920).Within(0.01));

            var tablet = new CanvasFit(ScreenSpec.WithInsets("t", 1536, 2048, 264));
            Assert.That(tablet.Match, Is.EqualTo(1));
            Assert.That(tablet.Height, Is.EqualTo(1920).Within(0.01));
            Assert.That(tablet.Width, Is.EqualTo(1440).Within(0.01));
        }

        [Test]
        public void TheSafeAreaIsInCanvasUnits()
        {
            var fit = new CanvasFit(ScreenSpec.WithInsets("n", 1440, 3200, 515, top: 140, bottom: 84));
            Assert.That(fit.Scale, Is.EqualTo(1440f / 1080).Within(1e-4));
            Assert.That(fit.Safe.Y, Is.EqualTo(84 / fit.Scale).Within(0.01));
            Assert.That(fit.Height - fit.Safe.YMax, Is.EqualTo(140 / fit.Scale).Within(0.01));
        }

        [Test]
        public void AGarbageSafeAreaFallsBackToTheWholeScreen()
        {
            var s = new ScreenSpec("bad", 1080, 1920, 400, new Box(0, 0, 0, 0));
            Assert.That(s.SafeArea.W, Is.EqualTo(1080));
            Assert.That(s.SafeArea.H, Is.EqualTo(1920));
        }

        [Test]
        public void AnchorsResolveLikeRectTransform()
        {
            var root = new UiNode("r", NodeKind.Root);
            var a = root.Add(new UiNode("a", NodeKind.Group)).TopLeft(10, 20, 100, 50);
            var b = root.Add(new UiNode("b", NodeKind.Group)).TopRight(10, 20, 100, 50);
            var c = root.Add(new UiNode("c", NodeKind.Group)).BottomCenter(0, 30, 200, 60);
            var d = root.Add(new UiNode("d", NodeKind.Group)).Stretch(5, 6, 7, 8);
            var e = root.Add(new UiNode("e", NodeKind.Group)).StretchTop(1, 2, 3, 40);
            var f = root.Add(new UiNode("f", NodeKind.Group)).Center(10, -10, 20, 20);
            root.Resolve(new Box(0, 0, 1000, 2000));
            Assert.That(a.Rect.ToString(), Is.EqualTo(new Box(10, 1930, 100, 50).ToString()));
            Assert.That(b.Rect.ToString(), Is.EqualTo(new Box(890, 1930, 100, 50).ToString()));
            Assert.That(c.Rect.ToString(), Is.EqualTo(new Box(400, 30, 200, 60).ToString()));
            Assert.That(d.Rect.ToString(), Is.EqualTo(new Box(5, 8, 989, 1985).ToString()));
            Assert.That(e.Rect.ToString(), Is.EqualTo(new Box(1, 1957, 997, 40).ToString()));
            Assert.That(f.Rect.ToString(), Is.EqualTo(new Box(500, 980, 20, 20).ToString()));
        }

        [Test]
        public void TextIsMeasuredFromTheFont()
        {
            Assert.That(FontMetrics.Has('Щ') && FontMetrics.Has('ё') && FontMetrics.Has(' '));
            // Nunito's digits are 600 units: «0000» at 100 is 240 wide
            Assert.That(FontMetrics.LineWidth("0000", 100), Is.EqualTo(240).Within(0.01));
            Assert.That(FontMetrics.LineWidth("Играть", 50), Is.GreaterThan(FontMetrics.LineWidth("Игра", 50)));
            Assert.That(FontMetrics.LineHeight(100), Is.EqualTo(137.7).Within(0.01));
        }

        [Test]
        public void TextShrinksToFitThenGivesUp()
        {
            var t = new TextSpec { Value = "Солнечная долина", Size = 80, MinSize = 30 };
            TextFit.Fit(t, 500, 200, 1);
            Assert.That(t.Fits);
            Assert.That(t.FittedSize, Is.LessThan(80).And.GreaterThanOrEqualTo(30));
            Assert.That(FontMetrics.LineWidth(t.Value, t.FittedSize), Is.LessThanOrEqualTo(500));

            var big = new TextSpec { Value = "Солнечная долина", Size = 80, MinSize = 30 };
            TextFit.Fit(big, 2000, 200, 1);
            Assert.That(big.FittedSize, Is.EqualTo(80));

            var no = new TextSpec { Value = "Солнечная долина", Size = 80, MinSize = 60 };
            TextFit.Fit(no, 300, 200, 1);
            Assert.That(no.Fits, Is.False);
        }

        [Test]
        public void TextWrapsWhenItMay()
        {
            var t = new TextSpec { Value = "Поставить новый забор вдоль дороги к дому", Size = 42, MinSize = 30, MaxLines = 2 };
            TextFit.Fit(t, 560, 130, 1);
            Assert.That(t.Fits);
            Assert.That(t.Lines.Count, Is.EqualTo(2));
            Assert.That(string.Join(" ", t.Lines), Is.EqualTo(t.Value));
        }

        [Test]
        public void AmountsAndTimersStayShort()
        {
            Assert.That(Format.Amount(0), Is.EqualTo("0"));
            Assert.That(Format.Amount(1250), Is.EqualTo("1 250"));
            Assert.That(Format.Amount(99_999), Is.EqualTo("99 999"));
            Assert.That(Format.Amount(123_456), Is.EqualTo("123К"));
            Assert.That(Format.Amount(1_250_000), Is.EqualTo("1,2М"));
            Assert.That(Format.Amount(long.MaxValue).Length, Is.LessThanOrEqualTo(6));
            Assert.That(Format.Timer(29 * 60 + 59), Is.EqualTo("29:59"));
            Assert.That(Format.Timer(3900), Is.EqualTo("1:05:00"));
        }

        [Test]
        public void EasingStartsAtZeroEndsAtOne()
        {
            foreach (var kind in System.Enum.GetValues(typeof(Easing.Kind)).Cast<Easing.Kind>())
            {
                Assert.That(Easing.Apply(kind, 0), Is.EqualTo(0).Within(1e-4), kind.ToString());
                Assert.That(Easing.Apply(kind, 1), Is.EqualTo(1).Within(1e-4), kind.ToString());
            }
            Assert.That(Easing.Apply(Easing.Kind.OutBack, 0.7f), Is.GreaterThan(1), "overshoots");
        }
    }
}
