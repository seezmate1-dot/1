using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core.Combat;

namespace LastHearth.Core.Surge
{
    /// <summary>Numbers of the evening Surge (Resources/combat.json, "surge"); tuned by SurgeCalibrationTests.</summary>
    public sealed class SurgeBalance
    {
        public int Ticks;
        public int TicksPerFloor;
        public double BlazingSlow;
        public List<int[]> Waves = new List<int[]>();
        public int SpawnEvery;
        public double CreatureHp;
        public double CreatureGrowth;
        public double LastWaveHp;
        public double HeroDps;
        public double HeroLevelGrowth;
        public double GearGrowth;
        public double DwellerDps;
        public int FlashCoal;
        public int FlashCooldown;
        public double FlashDamage;
        public int FlashReach;
        public int HatchUses;
        public int HatchTicks;
    }

    public sealed class SurgeCreature
    {
        public int Id;
        public int Floor;
        public int Progress;
        public double Hp;
        public double MaxHp;
    }

    public sealed class SurgeDefender
    {
        public int Id;
        public string Name = "";
        public bool Hero;
        public double Dps;
        public int Floor;
    }

    public sealed class SurgeSetup
    {
        public int HearthFloor;
        public int HearthLevel = 1;
        /// <summary>Floors where creatures come out: the coldest rooms first.</summary>
        public List<int> SpawnFloors = new List<int>();
        public List<SurgeDefender> Defenders = new List<SurgeDefender>();
        public bool Blazing;
        public long Coal;
        /// <summary>Talents of the Defense branch of the Heart of the Hearth: +x to defenders.</summary>
        public double DefenseBonus;
    }

    public enum SurgeCommandKind
    {
        MoveHero,
        Flash,
        Hatch,
    }

    public struct SurgeCommand
    {
        public SurgeCommandKind Kind;
        public int HeroId;
        public int Floor;

        public static SurgeCommand Move(int heroId, int floor) => new SurgeCommand { Kind = SurgeCommandKind.MoveHero, HeroId = heroId, Floor = floor };
        public static SurgeCommand Flash() => new SurgeCommand { Kind = SurgeCommandKind.Flash };
        public static SurgeCommand Hatch(int floor) => new SurgeCommand { Kind = SurgeCommandKind.Hatch, Floor = floor };
    }

    /// <summary>
    /// ACTIVITIES.md, 2: two minutes of live defense in the cutaway. Creatures climb from the coldest
    /// rooms towards the hearth one floor at a time; whoever stands on a floor fights there. A creature
    /// that crosses the hearth floor dims the hearth: minus one star.
    /// </summary>
    public sealed class SurgeDefense
    {
        readonly SurgeBalance c;
        readonly SurgeSetup setup;
        readonly Queue<SurgeCommand> commands = new Queue<SurgeCommand>();
        readonly List<int> spawnTicks = new List<int>();
        readonly Dictionary<int, int> hatchUntil = new Dictionary<int, int>();
        int spawned;
        int nextId = 1;

        public readonly List<SurgeCreature> Creatures = new List<SurgeCreature>();
        public readonly List<SurgeDefender> Defenders;
        public readonly List<string> Log = new List<string>();
        public int Tick;
        public bool Over;
        public int Breaches;
        public int Killed;
        public int FlashReadyAt;
        public int HatchesLeft;
        public long CoalSpent;
        public Func<SurgeDefense, IEnumerable<SurgeCommand>> Brain;

        public SurgeBalance Numbers => c;
        public SurgeSetup Setup => setup;
        public int HearthFloor => setup.HearthFloor;
        public int Stars => Math.Max(0, 3 - Breaches);
        public int TotalCreatures => spawnTicks.Count;
        public bool HatchClosed(int floor) => hatchUntil.TryGetValue(floor, out int until) && Tick < until;

        public SurgeDefense(SurgeBalance numbers, SurgeSetup setup)
        {
            c = numbers;
            this.setup = setup;
            Defenders = setup.Defenders;
            HatchesLeft = c.HatchUses;
            foreach (var wave in c.Waves)
            {
                for (int i = 0; i < wave[1]; i++)
                    spawnTicks.Add(wave[0] + i * c.SpawnEvery);
            }
            spawnTicks.Sort();
        }

        public void Command(SurgeCommand cmd) => commands.Enqueue(cmd);

        public void RunToEnd()
        {
            while (!Over)
                Step();
        }

        public void Step()
        {
            if (Over)
                return;
            if (Brain != null)
            {
                foreach (var cmd in Brain(this))
                    commands.Enqueue(cmd);
            }
            while (commands.Count > 0)
                Apply(commands.Dequeue());
            Tick++;

            while (spawned < spawnTicks.Count && spawnTicks[spawned] <= Tick)
                Spawn();

            // defenders fight the first creature on their floor
            foreach (var floor in Defenders.Select(d => d.Floor).Distinct().ToList())
            {
                double dps = Defenders.Where(d => d.Floor == floor).Sum(d => d.Dps) * (1 + setup.DefenseBonus);
                double hit = dps * 0.1;
                foreach (var cr in Creatures.Where(x => x.Floor == floor).OrderBy(x => x.Id).ToList())
                {
                    double dealt = Math.Min(hit, cr.Hp);
                    cr.Hp -= dealt;
                    hit -= dealt;
                    if (hit <= 0)
                        break;
                }
            }
            RemoveDead();

            // creatures climb
            int ticksPerFloor = setup.Blazing ? (int)Math.Round(c.TicksPerFloor * (1 + c.BlazingSlow)) : c.TicksPerFloor;
            foreach (var cr in Creatures.ToList())
            {
                if (HatchClosed(cr.Floor))
                    continue;
                cr.Progress++;
                if (cr.Progress < ticksPerFloor)
                    continue;
                cr.Progress = 0;
                if (cr.Floor == setup.HearthFloor)
                {
                    Breaches++;
                    Creatures.Remove(cr);
                    Log.Add(Tick / 10 + " с: тварь дошла до очага");
                }
                else
                {
                    cr.Floor += Math.Sign(setup.HearthFloor - cr.Floor);
                }
            }

            if (Tick >= c.Ticks)
            {
                Breaches += Creatures.Count;
                Creatures.Clear();
                Over = true;
            }
            else if (spawned == spawnTicks.Count && Creatures.Count == 0)
            {
                Over = true;
            }
            if (Over)
                Log.Add(Tick / 10 + " с: Наплыв отбит, звёзд " + Stars);
        }

        void Spawn()
        {
            int floor = setup.SpawnFloors[spawned % setup.SpawnFloors.Count];
            bool last = spawned >= spawnTicks.Count - c.Waves[c.Waves.Count - 1][1];
            double hp = c.CreatureHp * Math.Pow(c.CreatureGrowth, setup.HearthLevel - 1) * (last ? c.LastWaveHp : 1);
            Creatures.Add(new SurgeCreature { Id = nextId++, Floor = floor, Hp = hp, MaxHp = hp });
            spawned++;
        }

        void RemoveDead()
        {
            int before = Creatures.Count;
            Creatures.RemoveAll(x => x.Hp <= 1e-9);
            Killed += before - Creatures.Count;
        }

        void Apply(SurgeCommand cmd)
        {
            switch (cmd.Kind)
            {
                case SurgeCommandKind.MoveHero:
                    var d = Defenders.Find(x => x.Id == cmd.HeroId && x.Hero);
                    if (d != null)
                        d.Floor = cmd.Floor;
                    break;
                case SurgeCommandKind.Flash:
                    if (Tick < FlashReadyAt || setup.Coal - CoalSpent < c.FlashCoal)
                        return;
                    CoalSpent += c.FlashCoal;
                    FlashReadyAt = Tick + c.FlashCooldown;
                    double burn = c.FlashDamage * c.CreatureHp * Math.Pow(c.CreatureGrowth, setup.HearthLevel - 1);
                    foreach (var cr in Creatures.Where(x => Math.Abs(x.Floor - setup.HearthFloor) <= c.FlashReach))
                        cr.Hp -= burn;
                    RemoveDead();
                    Log.Add(Tick / 10 + " с: вспышка очага");
                    break;
                case SurgeCommandKind.Hatch:
                    if (HatchesLeft <= 0)
                        return;
                    HatchesLeft--;
                    hatchUntil[cmd.Floor] = Tick + c.HatchTicks;
                    Log.Add(Tick / 10 + " с: люк закрыт на этаже " + (cmd.Floor + 1));
                    break;
            }
        }

        // ------------------------------------------------------------------ brains

        /// <summary>Nobody at the screen: heroes stay at the hearth, no flash, no hatches.</summary>
        public static Func<SurgeDefense, IEnumerable<SurgeCommand>> Passive() => s => Enumerable.Empty<SurgeCommand>();

        /// <summary>
        /// A player who reads the board: sends the heroes to the floors where creatures will pass and
        /// stack, flashes when creatures reach the hearth, closes a hatch in front of a crowd.
        /// </summary>
        public static Func<SurgeDefense, IEnumerable<SurgeCommand>> Careful()
        {
            return s =>
            {
                var cmds = new List<SurgeCommand>();
                var heroes = s.Defenders.Where(d => d.Hero).OrderByDescending(d => d.Dps).ToList();
                if (heroes.Count > 0)
                {
                    // threat per floor: creature health weighted by closeness to the hearth
                    var threat = new Dictionary<int, double>();
                    foreach (var cr in s.Creatures)
                    {
                        int dist = Math.Abs(cr.Floor - s.HearthFloor);
                        threat[cr.Floor] = (threat.TryGetValue(cr.Floor, out double t) ? t : 0) + cr.Hp * (4 - Math.Min(3, dist));
                    }
                    var targets = threat.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
                    if (targets.Count == 0)
                        targets.AddRange(s.Setup.SpawnFloors.Distinct());
                    // two thirds of the heroes go to the worst floor, the rest to the next one
                    int main = Math.Max(1, (heroes.Count * 2 + 2) / 3);
                    for (int i = 0; i < heroes.Count; i++)
                    {
                        int floor = i < main || targets.Count == 1 ? targets[0] : targets[1];
                        if (heroes[i].Floor != floor)
                            cmds.Add(SurgeCommand.Move(heroes[i].Id, floor));
                    }
                }
                double near = s.Creatures.Where(x => Math.Abs(x.Floor - s.HearthFloor) <= s.Numbers.FlashReach).Sum(x => x.Hp);
                double unit = s.Numbers.CreatureHp * Math.Pow(s.Numbers.CreatureGrowth, s.Setup.HearthLevel - 1);
                if (near >= unit * 1.5 && s.Tick >= s.FlashReadyAt)
                    cmds.Add(SurgeCommand.Flash());
                if (s.HatchesLeft > 0)
                {
                    var crowd = s.Creatures.GroupBy(x => x.Floor).Where(g => g.Key != s.HearthFloor)
                        .OrderBy(g => Math.Abs(g.Key - s.HearthFloor)).FirstOrDefault(g => g.Count() >= 3);
                    if (crowd != null && !s.HatchClosed(crowd.Key))
                        cmds.Add(SurgeCommand.Hatch(crowd.Key));
                }
                return cmds;
            };
        }
    }
}
