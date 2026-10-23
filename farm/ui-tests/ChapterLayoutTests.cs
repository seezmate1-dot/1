using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using SunnyValley.Layout;

namespace SunnyValley.Ui.Tests
{
    /// <summary>
    /// The chapter 1 windows with the real texts of Resources/Meta/chapter1.json on every device: every replica and
    /// scene title in the dialogue (portrait left and right), every task title in the task list, every look's name on
    /// a picker card, every zone's name on the strip over «Играть» and in its celebration, with the zone's reward.
    /// </summary>
    public class ChapterLayoutTests
    {
        static JsonElement chapter;

        static JsonElement Chapter
        {
            get
            {
                if (chapter.ValueKind == JsonValueKind.Undefined)
                {
                    string path = Path.Combine(Dir(), "..", "unity", "SunnyValley", "Assets", "SunnyValley", "Resources", "Meta", "chapter1.json");
                    chapter = JsonDocument.Parse(File.ReadAllText(path)).RootElement;
                }
                return chapter;
            }
        }

        static string Dir([System.Runtime.CompilerServices.CallerFilePath] string here = "") => Path.GetDirectoryName(here);

        static IEnumerable<JsonElement> Arr(JsonElement e, string name) =>
            e.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray() : Enumerable.Empty<JsonElement>();

        static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        static Dictionary<string, string> Names() => Arr(Chapter, "characters").ToDictionary(c => Str(c, "id"), c => Str(c, "name"));

        static void AssertClean(UiLayout L, string what)
        {
            var problems = LayoutCheck.Problems(L);
            Assert.That(problems, Is.Empty, what + "\n" + string.Join("\n", problems));
        }

        [Test]
        public void EveryReplicaFitsTheDialogue()
        {
            var names = Names();
            var lines = Arr(Chapter, "tasks").SelectMany(t => Arr(t, "lines")).Select(l => (scene: (string)null, l))
                .Concat(Arr(Chapter, "scenes").SelectMany(s => Arr(s, "lines").Select(l => (scene: Str(s, "title"), l)))).ToList();
            Assert.That(lines.Count, Is.GreaterThan(100));
            foreach (var screen in Devices.All)
            {
                var fit = new CanvasFit(screen);
                foreach (var (scene, l) in lines)
                {
                    string who = Str(l, "who");
                    var L = Popups.Dialogue(fit, new DialogueInfo
                    {
                        Name = names[who], Portrait = "portrait." + who, Text = Str(l, "text"), SceneTitle = scene, CanSkip = scene != null,
                        Right = who != "anya" && who != "diary",
                    });
                    AssertClean(L, Str(l, "text"));
                }
            }
        }

        [Test]
        public void TheDialogueIsTappedAnywhereUnderTheTopRow()
        {
            foreach (var screen in Devices.All)
            {
                var fit = new CanvasFit(screen);
                var L = Popups.Dialogue(fit, new DialogueInfo { Text = "Привет", SceneTitle = "Приезд", CanSkip = true });
                var next = L.Find("dialogue.next").Rect;
                var panel = L.Find("dialogue.panel").Rect;
                var portrait = L.Find("dialogue.portrait").Rect;
                Assert.That(next.Contains(panel, 0.5f), screen.Name + ": a tap on the panel goes on");
                Assert.That(next.H, Is.GreaterThan(fit.Safe.H * 0.7f), screen.Name);
                Assert.That(portrait.Y, Is.LessThan(panel.YMax), screen.Name + ": the portrait rises from behind the panel");
                Assert.That(portrait.H, Is.GreaterThan(300), screen.Name);
            }
        }

        [Test]
        public void EveryTaskTitleFitsTheList()
        {
            var zones = Arr(Chapter, "zones").ToDictionary(z => Str(z, "id"), z => Str(z, "name"));
            var tasks = Arr(Chapter, "tasks").Select(t => new TaskItem(Str(t, "title"), t.GetProperty("stars").GetInt32(), zones[Str(t, "zone")],
                true, Arr(t, "choices").Any())).ToList();
            foreach (var screen in Devices.All)
            {
                var fit = new CanvasFit(screen);
                // the whole chapter in one list (more than ever opens at once): every row fits and scrolls
                AssertClean(Popups.Tasks(fit, new TasksInfo { ZoneName = "Мельница", ZoneDone = 9, ZoneTotal = 9, Stars = 99_999, Tasks = tasks }), "all tasks");
            }
        }

        [Test]
        public void EveryLookFitsAPickerCard()
        {
            foreach (var screen in Devices.All)
            {
                var fit = new CanvasFit(screen);
                foreach (var t in Arr(Chapter, "tasks").Where(t => Arr(t, "choices").Any()))
                {
                    var cards = Arr(t, "choices").Select(c => new LookCard("farm/x", Str(c, "name"))).ToList();
                    Assert.That(cards.Count, Is.EqualTo(3), Str(t, "id"));
                    var L = Popups.Picker(fit, new PickerInfo { Caption = Str(t, "title"), Cards = cards, Selected = 1, Button = "Построить", Stars = 3 });
                    AssertClean(L, Str(t, "id"));
                    var r = Enumerable.Range(0, 3).Select(i => L.Find("picker.card" + i).Rect).ToList();
                    Assert.That(r.Select(x => x.W).Distinct().Count(), Is.EqualTo(1), "three equal cards");
                    Assert.That(r[0].XMax, Is.LessThan(r[1].X), "side by side");
                    Assert.That(L.Find("picker.card1.tick").Has(NodeFlags.Hidden), Is.False);
                    Assert.That(L.Find("picker.card0.tick").Has(NodeFlags.Hidden), Is.True);
                }
            }
        }

        [Test]
        public void EveryZoneFitsTheStripAndItsCelebration()
        {
            foreach (var screen in Devices.All)
            {
                var fit = new CanvasFit(screen);
                foreach (var z in Arr(Chapter, "zones"))
                {
                    string name = Str(z, "name");
                    AssertClean(Screens.Farm(fit, new FarmHud { ZoneName = name, ZoneDone = 19, ZoneTotal = 20, Coins = 99_999, Level = 999 }), name);
                    var rewards = new List<(string icon, string text)>();
                    if (z.TryGetProperty("reward", out var r))
                    {
                        foreach (var p in r.EnumerateObject())
                        {
                            if (p.Value.ValueKind == JsonValueKind.Number && p.Value.GetInt32() > 0)
                                rewards.Add((Icons.Coin, "+" + Format.Amount(p.Value.GetInt32())));
                            else if (p.Value.ValueKind == JsonValueKind.Object)
                                rewards.AddRange(p.Value.EnumerateObject().Select(b => (Icons.Bomb, "+" + b.Value.GetInt32())));
                        }
                    }
                    Assert.That(rewards, Is.Not.Empty, name + " has a reward");
                    AssertClean(Popups.Celebrate(fit, new CelebrateInfo
                    {
                        Title = "«" + name + "» восстановлен!", Text = "Все задания выполнены. Впереди — новая часть фермы!", Rewards = rewards,
                    }), name);
                }
            }
        }

        [Test]
        public void TheZoneStripSitsBetweenTheColumnsAndThePlayButton()
        {
            foreach (var screen in Devices.All)
            {
                var fit = new CanvasFit(screen);
                var L = Screens.Farm(fit, new FarmHud());
                var zone = L.Find("farm.zone").Rect;
                var play = L.Find("farm.play").Rect;
                Assert.That(zone.Y, Is.GreaterThanOrEqualTo(play.YMax + Tokens.Gap), screen.Name);
                Assert.That(zone.CenterX, Is.EqualTo(play.CenterX).Within(0.5f), screen.Name);
                foreach (string side in new[] { "farm.orders", "farm.events" })
                    Assert.That(L.Find(side).Rect.Y, Is.GreaterThan(zone.YMax), screen.Name + " " + side);
            }
        }
    }
}
