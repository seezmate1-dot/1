using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SunnyValley.Layout;

namespace SunnyValley.Ui.Tests
{
    /// <summary>
    /// The Unity screens find their buttons and labels by id ("farm.play", "lose.give_up"). A typo there would
    /// silently leave a button dead, so every id written out in Game/Flow and Game/UI must exist in some layout.
    /// </summary>
    public class IdTests
    {
        static string GameDir([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
            Path.Combine(Path.GetDirectoryName(here), "..", "unity", "SunnyValley", "Assets", "SunnyValley", "Game");

        [Test]
        public void EveryIdTheScreensUseExists()
        {
            var fit = new CanvasFit(Devices.All[2]);
            var ids = new HashSet<string>();
            foreach (var (_, build) in LayoutCatalog.All())
            foreach (var n in build(fit).Root.All())
                ids.Add(n.Id);
            // popups built only with some flags: settings in a level, the tasks rows
            foreach (var n in Popups.Settings(fit, new SettingsInfo { InLevel = true }).Root.All())
                ids.Add(n.Id);

            var used = new List<(string file, string id)>();
            var pattern = new Regex("\"((?:farm|level|loading|start|win|lose|settings|tasks|toast)\\.[a-z_0-9.]+)\"");
            foreach (string file in Directory.GetFiles(Path.Combine(GameDir(), "Flow"), "*.cs")
                         .Concat(Directory.GetFiles(Path.Combine(GameDir(), "UI"), "*.cs")))
            {
                foreach (Match m in pattern.Matches(File.ReadAllText(file)))
                    used.Add((Path.GetFileName(file), m.Groups[1].Value));
            }
            Assert.That(used.Count, Is.GreaterThan(20), "the scan found the ids");
            // a prefix completed with a number in code ("level.booster" + i) must match a numbered id
            bool Known(string id) => ids.Contains(id) || ids.Any(x => x.Length > id.Length && x.StartsWith(id) && char.IsDigit(x[id.Length]));
            var missing = used.Where(u => !Known(u.id)).ToList();
            Assert.That(missing, Is.Empty, string.Join("\n", missing.Select(m => m.file + ": " + m.id)));
        }
    }
}
