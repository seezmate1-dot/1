using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SunnyValley.Core.Meta;

namespace SunnyValley.Core.Tests
{
    /// <summary>
    /// The farm scene of chapter 1 (chapter1_layout.json) matches the chapter: every zone has its place, every task
    /// changes something you can see, the three looks are the task's choices, every speaker has a portrait,
    /// and the art manifest (farm/tools/art/chapter1_art.json) lists every picture the layout asks for.
    /// </summary>
    public class MetaLayoutTests
    {
        static FarmLayoutDef layout;

        static FarmLayoutDef Layout => layout ??= FarmLayoutDef.Parse(File.ReadAllText(Path.Combine(MetaTestKit.MetaDir, "chapter1_layout.json")));

        static string ManifestPath => Path.GetFullPath(Path.Combine(MetaTestKit.MetaDir, "..", "..", "..", "..", "..", "..", "tools", "art", "chapter1_art.json"));

        [Test]
        public void EveryZoneIsOnTheMapWithoutOverlapping()
        {
            var data = MetaTestKit.Data;
            foreach (var z in data.Chapter.Zones)
                Assert.That(Layout.Zone(z.Id), Is.Not.Null, z.Id);
            foreach (var a in Layout.Zones)
            {
                Assert.That(a.X >= 0 && a.Y >= 0 && a.X + a.W <= Layout.Map.W && a.Y + a.H <= Layout.Map.H, a.Id);
                foreach (var b in Layout.Zones.Where(b => b != a))
                    Assert.That(a.X + a.W <= b.X || b.X + b.W <= a.X || a.Y + a.H <= b.Y || b.Y + b.H <= a.Y, a.Id + " / " + b.Id);
            }
        }

        [Test]
        public void EveryTaskChangesTheFarm()
        {
            var data = MetaTestKit.Data;
            foreach (var t in data.Chapter.Tasks)
            {
                var sprites = Layout.SpritesOf(t.Id);
                Assert.That(sprites, Is.Not.Empty, t.Id + " shows nothing");
                Assert.That(sprites.All(s => s.zone.Id == t.Zone), t.Id + " has a sprite in another zone");
                // done or not, the picture is different
                Assert.That(sprites.Any(s => s.sprite.Visible(id => false) != s.sprite.Visible(id => id == t.Id)), t.Id);
            }
        }

        [Test]
        public void LooksAreTheTasksChoices()
        {
            foreach (var t in MetaTestKit.Data.Chapter.Tasks.Where(t => t.Choices.Count > 0))
            {
                var withLooks = Layout.SpritesOf(t.Id).Where(s => s.sprite.Looks != null).ToList();
                Assert.That(withLooks, Is.Not.Empty, t.Id);
                foreach (var (_, s) in withLooks)
                {
                    Assert.That(s.Looks.Keys, Is.EquivalentTo(t.Choices.Select(c => c.Id)), t.Id);
                    Assert.That(s.Tap, t.Id + ": a tap on it changes the look");
                    Assert.That(s.Looks.Values.Distinct().Count(), Is.EqualTo(3), t.Id);
                }
            }
        }

        [Test]
        public void EverySpeakerHasAPortrait()
        {
            var data = MetaTestKit.Data;
            var speakers = data.Chapter.Tasks.SelectMany(t => t.Lines).Concat(data.Chapter.Scenes.SelectMany(s => s.Lines)).Select(l => l.Who).Distinct();
            foreach (string who in speakers)
                Assert.That(Layout.Portraits.ContainsKey(who), who);
        }

        [Test]
        public void TheManifestListsEveryPicture()
        {
            var manifest = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(ManifestPath))["pieces"];
            var names = new HashSet<string>(((Newtonsoft.Json.Linq.JObject)manifest).Properties().Select(p => p.Name));
            var used = new List<string>();
            foreach (var z in Layout.Zones)
            {
                used.Add(z.Background);
                foreach (var s in z.Sprites)
                {
                    used.Add(s.Art);
                    if (s.Looks != null) used.AddRange(s.Looks.Values);
                }
            }
            used.AddRange(Layout.Fog);
            used.Add(Layout.Dust);
            used.AddRange(Layout.Portraits.Values);
            foreach (string art in used.Distinct())
                Assert.That(names.Contains("Farm/" + art), art);
            foreach (var z in Layout.Zones)
                Assert.That((string)manifest["Farm/" + z.Background]["kind"], Is.EqualTo("background"), z.Background);
        }

        [Test]
        public void ATreeGoesThroughItsStates()
        {
            var trees = Layout.Zone("orchard").Sprites.Where(s => s.X == 420 && s.Y == 480).ToList();
            var done = new HashSet<string>();
            bool Done(string id) => done.Contains(id);
            string Shown() => string.Join(",", trees.Where(s => s.Visible(Done)).Select(s => s.Art));
            Assert.That(Shown(), Does.Contain("AppleTreeDry"));
            done.Add("orchard_prune");
            Assert.That(Shown(), Is.EqualTo("Ch1/Objects/AppleTree"));
            done.Add("orchard_whitewash");
            Assert.That(Shown(), Is.EqualTo("Ch1/Objects/AppleTreeWhite"));
            done.Add("orchard_bloom");
            Assert.That(Shown(), Is.EqualTo("Ch1/Objects/AppleTreeBloom"));
        }
    }
}
