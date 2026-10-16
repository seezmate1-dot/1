using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core.Combat;

namespace LastHearth.Core
{
    /// <summary>
    /// GDD 8.2: companions of heroes. Each hero takes one companion (a creature or a sidekick) who
    /// fights next to them with a simple skill (Battle.cs: Guard, Bite, Stagger, Heal). The bond level
    /// belongs to the hero, is raised with treats up to 1 + hero level / 4, and stays when the hero
    /// changes the companion. A kind, once found, can walk with any number of heroes.
    /// </summary>
    public sealed partial class GameSession
    {
        CompanionState Companions => State.Companions ?? (State.Companions = new CompanionState());

        public CompanionKindData CompanionKind(string id) => Balance.Companions.Kinds.Find(k => k.Id == id);

        public bool CompanionUnlocked(string id) => id != null && Companions.Unlocked.Contains(id);

        /// <summary>The companion of a hero; null when the hero walks alone.</summary>
        public HeroCompanion CompanionOf(int heroId)
        {
            var d = FindDweller(heroId);
            if (d == null || !d.IsHero || !Companions.Heroes.TryGetValue(heroId, out var c) || c == null)
                return null;
            return CompanionKind(c.Kind) != null ? c : null;
        }

        public int BondCap(Dweller d) =>
            Math.Min(Balance.Companions.MaxLevel, 1 + d.HeroLevel / Math.Max(1, Balance.Companions.HeroLevelsPerLevel));

        public double CompanionPower(CompanionKindData kind, int level) => kind.Power + kind.PerLevel * (Math.Max(1, level) - 1);

        /// <summary>Gives a hero a companion of a found kind ("" or null: the hero walks alone). The bond stays.</summary>
        public Fail SetCompanion(int heroId, string kind)
        {
            var d = FindDweller(heroId);
            if (d == null)
                return Fail.NotFound;
            if (!d.IsHero)
                return Fail.NotAHero;
            Companions.Heroes.TryGetValue(heroId, out var current);
            if (string.IsNullOrEmpty(kind))
            {
                if (current != null)
                    current.Kind = "";
                return Fail.None;
            }
            if (CompanionKind(kind) == null)
                return Fail.NotFound;
            if (!CompanionUnlocked(kind))
                return Fail.Locked;
            if (current == null)
                Companions.Heroes[heroId] = current = new HeroCompanion();
            current.Kind = kind;
            return Fail.None;
        }

        /// <summary>The bond level of a hero (1 when the hero has never had a companion).</summary>
        public int BondLevel(int heroId) => Companions.Heroes.TryGetValue(heroId, out var c) && c != null ? Math.Max(1, c.Level) : 1;

        /// <summary>Treats for the next bond level, or -1 at the top.</summary>
        public long BondCost(int heroId)
        {
            int level = BondLevel(heroId);
            var costs = Balance.Companions.LevelCost;
            return level >= Balance.Companions.MaxLevel || level > costs.Length ? -1 : costs[level - 1];
        }

        public Fail RaiseBond(int heroId)
        {
            var d = FindDweller(heroId);
            if (d == null)
                return Fail.NotFound;
            if (!d.IsHero)
                return Fail.NotAHero;
            if (CompanionOf(heroId) == null)
                return Fail.NotFound;
            long cost = BondCost(heroId);
            if (cost < 0)
                return Fail.LimitReached;
            if (BondLevel(heroId) >= BondCap(d))
                return Fail.HeroCap;
            if (Pets.Treats < cost)
                return Fail.NotEnough;
            Pets.Treats -= cost;
            Companions.Heroes[heroId].Level = BondLevel(heroId) + 1;
            Count(DailyTask.HeroGrowth);
            return Fail.None;
        }

        /// <summary>The companion of a hero as the battle sees it.</summary>
        public CompanionSetup CompanionSetupFor(Dweller d)
        {
            var c = CompanionOf(d.Id);
            if (c == null)
                return null;
            var kind = CompanionKind(c.Kind);
            return new CompanionSetup
            {
                Name = kind.Name,
                Skill = kind.Skill,
                Every = kind.Every,
                Power = CompanionPower(kind, Math.Min(c.Level, BondCap(d))),
            };
        }

        /// <summary>Pets and companions join a battle of the squad (SquadSetup).</summary>
        void ApplyKennel(BattleSetup setup)
        {
            CheckKennel();
            foreach (var h in setup.Heroes)
            {
                var d = FindDweller(h.Id);
                if (d != null)
                    h.Companion = CompanionSetupFor(d);
            }
            ApplyPet(setup);
        }

        /// <summary>The free player's kennel at this rank, built into the campaign's enemies (1 without data).</summary>
        static double KennelExpected(double[] table, int rank) =>
            table.Length == 0 ? 1 : table[Math.Max(0, Math.Min(table.Length - 1, rank - 1))];

        // ------------------------------------------------------------------ the hero screen

        /// <summary>
        /// A hero as the battle makes them now: the hero level, the main stat, the equipment (items, sets and the
        /// forge's issued pieces), the active pet and the companion. Null without the combat numbers or for a dweller.
        /// </summary>
        public Fighter HeroPreview(int heroId)
        {
            var d = FindDweller(heroId);
            if (d == null || !d.IsHero || Combat == null)
                return null;
            var setup = new BattleSetup();
            ApplyPet(setup);
            var f = Battle.HeroFighter(Combat, new HeroSetup
            {
                Id = d.Id,
                Name = d.Name,
                Class = d.HeroClass,
                Level = d.HeroLevel,
                SkillLevel = d.SkillLevel,
                Stats = (int[])d.Stats.Clone(),
                Gear = HeroGear(d),
            });
            f.Attack *= setup.PetAttack;
            f.MaxHp *= setup.PetHp;
            f.Hp = f.MaxHp;
            f.Companion = CompanionSetupFor(d);
            return f;
        }

        public string CompanionSkillText(CompanionKindData kind, int level)
        {
            double p = CompanionPower(kind, level);
            switch (kind.Skill)
            {
                case CompanionSkill.Guard: return string.Format("принимает удары: герой получает на {0:0}% меньше урона", p * 100);
                case CompanionSkill.Bite: return string.Format("кусает раз в {0:0} с: {1:0}% атаки героя", kind.Every / 10.0, p * 100);
                case CompanionSkill.Stagger: return string.Format("раз в {0:0} с сбивает врага: +{1:0.#} к оглушению", kind.Every / 10.0, p);
                default: return string.Format("раз в {0:0} с лечит героя на {1:0.#}% здоровья", kind.Every / 10.0, p * 100);
            }
        }
    }
}
