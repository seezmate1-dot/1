using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    /// <summary>MODES.md, 1: the power gate, the new kinds of battles, hard mode, the secrets, hints and story.</summary>
    public class CampaignModesTests
    {
        const long T0 = TestData.Start;

        /// <summary>Win rates of every kind of battle for the free player's squad at its rank (careful / auto).</summary>
        [Test, Explicit]
        public void KindSweep()
        {
            var b = TestData.Balance();
            var stats = new Dictionary<string, int[]>();
            for (int i = 0; i < b.Campaign.Missions.Count; i++)
            {
                var m = b.Campaign.Missions[i];
                var s = CampaignTests.Fortress(m.Rank);
                foreach (bool hard in new[] { false, true })
                {
                    string key = m.Kind + (hard ? " hard" : "");
                    if (!stats.ContainsKey(key))
                        stats[key] = new int[5];
                    for (int seed = 1; seed <= 3; seed++)
                    {
                        var setup = s.MissionSetup(i, seed, hard);
                        if (hard)
                        {
                            foreach (var h in setup.Heroes)
                            {
                                h.AttackMult *= s.Balance.Campaign.Hard.Power;
                                h.HpMult *= s.Balance.Campaign.Hard.Power;
                            }
                        }
                        var c = new Battle(s.Combat, setup) { Brain = Brains.Careful() };
                        c.RunToEnd();
                        var a = new Battle(s.Combat, hard ? setup : s.MissionSetup(i, seed)) { Brain = Brains.AutoBattle() };
                        a.RunToEnd();
                        var st = stats[key];
                        st[0]++;
                        st[1] += c.Won ? 1 : 0;
                        st[2] += a.Won ? 1 : 0;
                        st[3] += c.EscortLost ? 1 : 0;
                        st[4] += c.Stars;
                    }
                }
            }
            foreach (var kv in stats)
                TestContext.WriteLine(string.Format("{0}: careful {1}/{0b}, auto {2}, escort lost {3}, stars {4:0.0}".Replace("{0b}", kv.Value[0].ToString()),
                    kv.Key, kv.Value[1], kv.Value[2], kv.Value[3], (double)kv.Value[4] / kv.Value[0]));
        }

        /// <summary>Prints the bot's squad power against the recommended power of its next battle, day by day.</summary>
        [Test, Explicit]
        public void PowerAgainstTheBot()
        {
            var s = TestData.NewSession();
            s.Combat = TestData.Combat();
            var bot = new PlayerBot(s);
            bool gate = Environment.GetEnvironmentVariable("LH_GATE") != "0";
            s.AdminSetPowerGate(gate);
            var ratios = new List<double>();
            int seen = 0;
            bot.PlayDays(TestData.Start, 30, day =>
            {
                // the squad of the day against the reference of the battles cleared today
                for (; seen < s.MissionsCleared; seen++)
                    ratios.Add(s.SquadPower() / s.ReferencePower(s.Mission(seen).Rank));
                if (day == 30)
                    TestContext.WriteLine(string.Format("power / reference at clear: min {0:0.00} avg {1:0.00} max {2:0.00}, missions {3}",
                        ratios.Min(), ratios.Average(), ratios.Max(), s.MissionsCleared));
                int next = Math.Min(s.MissionsCleared, s.Balance.Campaign.Missions.Count - 1);
                int rec = s.RecommendedPower(next);
                TestContext.WriteLine(string.Format("day {0}: hearth {1} missions {2} power {3:0} rec {4} (rank {5}) share {6:0.00}, own hearth {7:0.00}, heroes {8}",
                    day, s.State.HearthLevel, s.MissionsCleared, s.SquadPower(), rec, s.Mission(next).Rank, s.PowerShare(rec),
                    s.SquadPower() / s.ReferencePower(s.State.HearthLevel), string.Join(",", s.Squad().Select(d => d.HeroLevel))));
            });
        }
    }
}
