using System;
using System.IO;
using LastHearth.Core;

namespace LastHearth.Core.Tests
{
    /// <summary>Finds Resources/balance.json both in the Unity editor and in `dotnet test`.</summary>
    public static class TestData
    {
        const string Relative = "Assets/LastHearth/Resources/balance.json";
        const string FromRepo = "hearth/unity/LastHearth/" + Relative;

        static Balance cached;

        public const long Start = 1_800_000_000; // any fixed moment: the core never reads the clock

        public static Balance Balance()
        {
            if (cached != null)
                return cached;
            foreach (string root in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(root);
                while (dir != null)
                {
                    foreach (string rel in new[] { Relative, FromRepo })
                    {
                        string path = Path.Combine(dir.FullName, rel);
                        if (File.Exists(path))
                            return cached = Core.Balance.FromJson(File.ReadAllText(path));
                    }
                    dir = dir.Parent;
                }
            }
            throw new FileNotFoundException("balance.json not found; run hearth/tools/economy.py");
        }

        public static GameSession NewSession() => NewGame.Create(Balance(), Start);
    }
}
