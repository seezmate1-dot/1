using System.Linq;
using NUnit.Framework;
using SunnyValley.Core.Meta;
using static SunnyValley.Core.Tests.MetaTestKit;

namespace SunnyValley.Core.Tests
{
    public class MetaDataTests
    {
        [Test]
        public void RealDataIsValid()
        {
            var problems = DataValidator.Validate(Data);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void ChapterHasEightZonesOfEightToTwentyTasks()
        {
            var zones = Data.Chapter.Zones.Select(z => z.Id).ToArray();
            Assert.That(zones, Is.EqualTo(new[] { "yard", "house", "garden", "orchard", "pond", "barn", "coop", "mill" }));
            foreach (var z in Data.Chapter.Zones)
                Assert.That(Data.ZoneTasks[z.Id].Count, Is.InRange(8, 20), z.Id);
            Assert.That(Data.Chapter.Tasks.All(t => t.Stars >= 1 && t.Stars <= 3));
            Assert.That(Data.Chapter.Tasks.Count(t => t.Choices.Count == 3), Is.GreaterThanOrEqualTo(8), "many tasks offer 3 looks");
        }

        [Test]
        public void StoryHasTheCastAndScenes()
        {
            foreach (var who in new[] { "anya", "matvey", "vera", "grachev", "sharik" })
                Assert.That(Data.Characters.ContainsKey(who), who);
            Assert.That(Data.Characters["anya"].Name, Is.EqualTo("Аня"));
            Assert.That(Data.Characters["grachev"].Name, Is.EqualTo("Грачёв"));
            Assert.That(Data.Chapter.Scenes.Count(s => s.Lines.Count >= 4), Is.GreaterThanOrEqualTo(5), "a few multi-line scenes");
            Assert.That(Data.Scenes.ContainsKey(Data.Chapter.IntroScene));
            Assert.That(Data.Scenes.ContainsKey(Data.Chapter.FinaleScene));
            // Every character speaks somewhere.
            var speakers = Data.Chapter.Tasks.SelectMany(t => t.Lines).Concat(Data.Chapter.Scenes.SelectMany(s => s.Lines)).Select(l => l.Who).ToHashSet();
            foreach (var c in Data.Chapter.Characters)
                Assert.That(speakers.Contains(c.Id), c.Id + " never speaks");
        }

        [Test]
        public void EconomyMatchesTheGdd()
        {
            var b = Data.Balance;
            Assert.That(b.Crops.Min(c => c.GrowSeconds), Is.EqualTo(120), "2 minutes");
            Assert.That(b.Crops.Max(c => c.GrowSeconds), Is.EqualTo(8 * 3600), "8 hours");
            Assert.That(b.Match3.ExtraMovesPrices, Is.EqualTo(new[] { 900, 1900, 2900 }));
            Assert.That(b.Lives.Max, Is.EqualTo(5));
            Assert.That(b.Lives.RegenSeconds, Is.EqualTo(1800));
            Assert.That(b.Orders.Slots, Is.EqualTo(9));
            foreach (var id in new[] { "chicken", "cow", "sheep", "bee", "pig" })
                Assert.That(Data.Animals.ContainsKey(id), id);
            foreach (var id in new[] { "mill", "bakery", "dairy", "sugar_mill", "textile", "jam", "toolshed" })
                Assert.That(Data.Workshops.ContainsKey(id), id);
            foreach (var id in new[] { "wheat", "corn", "carrot", "sunflower", "strawberry", "cotton", "apple", "pear" })
                Assert.That(Data.Items.ContainsKey(id), id);
            // The tool shed makes boosters out of farm goods (GDD 6).
            Assert.That(Data.Workshops["toolshed"].Recipes.Select(r => r.Output).Distinct().Count(), Is.EqualTo(7));
            Assert.That(b.Pacing.Chapter1Day, Is.InRange(b.Pacing.Chapter1MinDays, b.Pacing.Chapter1MaxDays));
        }

        [Test]
        public void ValidatorCatchesBrokenData()
        {
            var d = FreshData();
            d.Chapter.Tasks[3].Requires.Add("no_such_task");
            d.Chapter.Tasks[4].Stars = 4;
            d.Chapter.Tasks[5].Lines.Clear();
            d.Chapter.Tasks[6].Choices.Add(new ChoiceDef { Id = "x", Name = "Икс" });
            d.Balance.Crops[0].GrowSeconds = 30;
            d.Balance.Workshops[0].Recipes[0].Inputs["unobtainium"] = 1;
            d.Chapter.Tasks[7].Lines.Add(new DialogueLine { Who = "nobody", Text = "hello" });
            var d2 = new MetaData(d.Balance, d.Chapter);
            var p = DataValidator.Validate(d2);
            string all = string.Join("\n", p);
            Assert.That(all, Does.Contain("no_such_task"));
            Assert.That(all, Does.Contain("звёзд 4"));
            Assert.That(all, Does.Contain("нет реплики"));
            Assert.That(all, Does.Contain("вариантов вида должно быть 3"));
            Assert.That(all, Does.Contain("2 мин … 8 ч"));
            Assert.That(all, Does.Contain("unobtainium"));
            Assert.That(all, Does.Contain("неизвестный персонаж nobody"));
            Assert.That(all, Does.Contain("не по-русски"));
        }

        [Test]
        public void ValidatorCatchesPrerequisiteCycles()
        {
            var d = FreshData();
            var a = d.Chapter.Tasks.First(t => t.Id == "yard_gate");
            a.Requires.Add("yard_weeds");   // weeds already requires gate
            var p = DataValidator.Validate(new MetaData(d.Balance, d.Chapter));
            Assert.That(string.Join("\n", p), Does.Contain("никогда не откроются"));
        }

        [Test]
        public void NamesAreRussianForEverything()
        {
            Assert.That(Data.NameOf("wheat"), Is.EqualTo("Пшеница"));
            Assert.That(Data.NameOf("hammer"), Is.EqualTo("Молоток"));
            Assert.That(Data.NameOf(Boosters.Shovel), Is.EqualTo("Лопата"));
            Assert.That(Data.NameOf("mill"), Is.EqualTo("Мельница"));
            Assert.That(Data.NameOf("make_shovel"), Is.EqualTo("Лопата"));
        }

        [Test]
        public void ItemValuesFollowTheChain()
        {
            // A product is worth more than its inputs in an order (making it pays).
            foreach (var w in Data.Balance.Workshops)
                foreach (var r in w.Recipes)
                {
                    if (!Data.Items.TryGetValue(r.Output, out var outIt)) continue;
                    double inputs = r.Inputs.Sum(kv => Data.Items[kv.Key].OrderCoins * kv.Value);
                    Assert.That(outIt.OrderCoins * r.Count, Is.GreaterThan(inputs), r.Id);
                }
        }

        [Test]
        public void UnlocksAreListedPerLevel()
        {
            Assert.That(Data.UnlocksAt(2), Does.Contain("bakery"));
            Assert.That(Data.UnlocksAt(Data.Balance.Truck.Level), Does.Contain("truck"));
        }

        [Test]
        public void TextHelpers()
        {
            Assert.That(Texts.Plural(1, "монета", "монеты", "монет"), Is.EqualTo("1 монета"));
            Assert.That(Texts.Plural(3, "монета", "монеты", "монет"), Is.EqualTo("3 монеты"));
            Assert.That(Texts.Plural(11, "монета", "монеты", "монет"), Is.EqualTo("11 монет"));
            Assert.That(Texts.Plural(21, "монета", "монеты", "монет"), Is.EqualTo("21 монета"));
            Assert.That(Texts.Duration(45), Is.EqualTo("45 с"));
            Assert.That(Texts.Duration(125 * 60), Is.EqualTo("2 ч 05 мин"));
            Assert.That(Texts.Duration(27 * 3600), Is.EqualTo("1 д 3 ч"));
            foreach (MetaError e in System.Enum.GetValues(typeof(MetaError)))
                if (e != MetaError.None)
                    Assert.That(Texts.Error(e), Does.Match("[А-Яа-яЁё]"), e.ToString());
        }
    }
}
