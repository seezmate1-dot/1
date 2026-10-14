using System.Collections.Generic;

namespace LastHearth.Core.Combat
{
    /// <summary>
    /// Who presses the buttons. AutoBattle is the in-game auto mode: skills on cooldown, answers a
    /// telegraph about half of the time. Careful is a player who reads every telegraph and plays the
    /// class combos; the calibration tests use both to tune the bosses.
    /// </summary>
    public static class Brains
    {
        public static System.Func<Battle, IEnumerable<BattleCommand>> AutoBattle()
        {
            var answered = new Dictionary<int, bool>();
            return b =>
            {
                var cmds = new List<BattleCommand>();
                bool answer = false;
                if (b.Pending != null)
                {
                    if (!answered.TryGetValue(b.Pending.StartTick, out answer))
                    {
                        answer = b.Rand() < b.Numbers.AutoAnswerChance;
                        answered[b.Pending.StartTick] = answer;
                    }
                }
                foreach (var h in b.Heroes)
                {
                    if (!h.Ready)
                        continue;
                    if (h.Class == HeroClass.Guardian)
                    {
                        if (b.Pending != null && answer)
                            cmds.Add(BattleCommand.Skill(h.Id));
                        continue;
                    }
                    cmds.Add(BattleCommand.Skill(h.Id));
                }
                if (b.Pending != null && answer && b.Pending.Mechanic == Mechanic.Smash && RetreatMoment(b))
                    cmds.Add(BattleCommand.Retreat());
                return cmds;
            };
        }

        public static System.Func<Battle, IEnumerable<BattleCommand>> Careful()
        {
            var waited = new Dictionary<int, int>();
            return b =>
            {
                var cmds = new List<BattleCommand>();
                var cleaver = Find(b, HeroClass.Cleaver);
                var guardian = Find(b, HeroClass.Guardian);
                var mage = Find(b, HeroClass.AshMage);
                bool stunning = false;

                if (b.Pending != null)
                {
                    // 1) break the strike with a stun if the Crush fills the bar
                    if (cleaver != null && cleaver.Ready &&
                        b.Stagger + b.Numbers.Skills.CrushStagger * cleaver.SkillMult * b.Family.StaggerMult >= 100)
                    {
                        cmds.Add(BattleCommand.Skill(cleaver.Id));
                        stunning = true;
                    }
                    else if (b.Pending.Mechanic == Mechanic.Smash && b.Tick >= b.RetreatReadyAt)
                    {
                        if (RetreatMoment(b))
                            cmds.Add(BattleCommand.Retreat());
                    }
                    else if (guardian != null && guardian.Ready && !b.WallUp)
                    {
                        cmds.Add(BattleCommand.Skill(guardian.Id));
                    }
                }

                foreach (var h in b.Heroes)
                {
                    if (!h.Ready || h == guardian || (stunning && h == cleaver))
                        continue;
                    waited[h.Id] = waited.TryGetValue(h.Id, out int w) ? w + 1 : 1;
                    bool longWait = waited[h.Id] > 40;
                    switch (h.Class)
                    {
                        case HeroClass.Cleaver:
                            // keep the Crush for the next telegraph unless the bar is far from full
                            if (b.Pending == null && b.Stagger < 40)
                                Use(cmds, h, waited);
                            break;
                        case HeroClass.Swordsman:
                            if (b.WallUp || b.BossStunned || longWait)
                                Use(cmds, h, waited);
                            break;
                        case HeroClass.Archer:
                            if (b.Burning || mage == null || longWait)
                                Use(cmds, h, waited);
                            break;
                        case HeroClass.CoalPriest:
                            if (b.Heroes.Exists(x => x.Alive && x.Hp < x.MaxHp * 0.6) || longWait)
                                Use(cmds, h, waited);
                            break;
                        default:
                            Use(cmds, h, waited);
                            break;
                    }
                }
                return cmds;
            };
        }

        static void Use(List<BattleCommand> cmds, Fighter h, Dictionary<int, int> waited)
        {
            cmds.Add(BattleCommand.Skill(h.Id));
            waited[h.Id] = 0;
        }

        /// <summary>A retreat lasts less than the wind-up: step back just before the blow.</summary>
        static bool RetreatMoment(Battle b) => b.Pending.ImpactTick - b.Tick <= b.Numbers.RetreatTicks - 3;

        static Fighter Find(Battle b, HeroClass c) => b.Heroes.Find(h => h.Alive && h.Class == c);
    }
}
