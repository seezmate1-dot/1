using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace LastHearth.Core.Combat
{
    public enum Line
    {
        Front,
        Middle,
        Back,
    }

    public enum MonsterFamily
    {
        Fiend,     // твари: быстрые, слабы к огню
        Possessed, // одержимые: кровоточат
        Knight,    // рыцари Двора: броня, слабы к оглушению
        Root,      // корневые: медленные, огромные
    }

    /// <summary>ACTIVITIES.md, 1: the heavy strikes a boss telegraphs.</summary>
    public enum Mechanic
    {
        Smash, // по передней линии: отход или Стена
        Wave,  // по всему отряду: Стена
        Grab,  // по одному герою сзади: Стена или сбить
    }

    /// <summary>
    /// Numbers of the battle (Resources/combat.json). Tuned by hand and checked by the calibration
    /// tests (CombatCalibrationTests): a careful player wins with three stars, auto-battle wins less often.
    /// </summary>
    public sealed class CombatBalance
    {
        public double TickSeconds;
        public int MaxTicks;
        public int StarTicks;
        public double CritBase;
        public double CritPerAgility;
        public double CritMult;
        public double LevelGrowth;
        public double GearGrowth;
        public double StatBonus;
        public double SkillPerLevel;
        public double AutoAnswerChance;
        public int RetreatTicks;
        public int RetreatCooldown;
        /// <summary>Share of the reward for 0, 1, 2 and 3 stars.</summary>
        public double[] StarReward = new double[4];
        public List<ClassData> Classes = new List<ClassData>();
        public SkillNumbers Skills = new SkillNumbers();
        public BossData Boss = new BossData();
        public List<FamilyData> Families = new List<FamilyData>();

        public static CombatBalance FromJson(string json)
        {
            var b = JsonConvert.DeserializeObject<CombatBalance>(json);
            if (b == null || b.Classes.Count != 6 || b.Families.Count != 4)
                throw new FormatException("combat.json is broken");
            return b;
        }

        public ClassData Class(HeroClass c)
        {
            foreach (var x in Classes)
            {
                if (x.Class == c)
                    return x;
            }
            throw new KeyNotFoundException(c.ToString());
        }

        public FamilyData Family(MonsterFamily f)
        {
            foreach (var x in Families)
            {
                if (x.Family == f)
                    return x;
            }
            throw new KeyNotFoundException(f.ToString());
        }
    }

    public sealed class ClassData
    {
        public HeroClass Class;
        public Line Line;
        public double Hp;
        public double Attack;
        public double Armor;
        public int Interval;
        public int SkillCharge;
        public Stat MainStat;
    }

    public sealed class SkillNumbers
    {
        public double WallReduction;
        public int WallTicks;
        public double CounterMult;
        public double CrushStagger;
        public double VolleyMult;
        public double ExplosionMult;
        public double HealShare;
        public double PriestBasicHeal;
        public int HasteTicks;
        public double Haste;
        public double FireballMult;
        public int BurnTicks;
        public double BurnShare;
        public double StunCritMult;
    }

    public sealed class BossData
    {
        public double Hp;
        public double Attack;
        public double Armor;
        public double HpGrowth;
        public double AttackGrowth;
        public int Interval;
        public int FirstTelegraph;
        public int TelegraphEvery;
        public int Windup;
        public int StunTicks;
        public double StunDamageBonus;
        public double StaggerPerHit;
        public double Smash;
        public double Wave;
        public double Grab;
    }

    public sealed class FamilyData
    {
        public MonsterFamily Family;
        public string Name = "";
        public double Hp;
        public double Attack;
        public double Armor;
        public double Interval;
        public double StaggerMult;
        public HeroClass Weak;
        public double WeakMult;
        public List<Mechanic> Mechanics = new List<Mechanic>();
    }
}
