using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core.Combat;
using Newtonsoft.Json;

namespace LastHearth.Core
{
    public sealed class RouteCard
    {
        public string Title = "";
        public string OptionA = "";
        public string OptionB = "";
    }

    /// <summary>ACTIVITIES.md 4: what waits at a point of the path, and which stat decides it.</summary>
    public enum RoutePoint
    {
        /// <summary>The fortress gate the path starts from (not a point of the walk).</summary>
        Gate,
        /// <summary>Strength: a skirmish; the player can fight it by hand (30 seconds).</summary>
        Fight,
        /// <summary>Agility (scouting) or Endurance: something to carry home.</summary>
        Resource,
        /// <summary>Spirit: people on the road; a decision card waits at home.</summary>
        Event,
        /// <summary>Endurance: a good rest keeps the illness from the fog away.</summary>
        Rest,
        /// <summary>Intellect (traps) or Agility (a fall): a failed check injures.</summary>
        Danger,
    }

    /// <summary>A point of a route map; X 0..1 along the way, Y −1 (safe) .. 1 (long) across.</summary>
    public sealed class RouteNode
    {
        public int Id;
        public RoutePoint Type;
        public Stat Stat;
        public int Need;
        public string Name = "";
        /// <summary>-1 for the points every path goes through; 0 or 1 for a fork's branch.</summary>
        public int Fork = -1;
        public RoutePath Path;
        /// <summary>The check of the risky branch (the watchtower scouts it, ForkCheck).</summary>
        public bool Key;
        /// <summary>The ruins: a note about the world can be found there.</summary>
        public bool Lore;
        public float X, Y;
        /// <summary>Minutes of the way to this point from the one before.</summary>
        public int Minutes;
    }

    /// <summary>The map of a route: the same for the same seed and hearth level (the planner shows it before leaving).</summary>
    public sealed class RouteMap
    {
        public readonly List<RouteNode> Nodes = new List<RouteNode>();
        public readonly List<(int from, int to)> Edges = new List<(int, int)>();

        public RouteNode Node(int id) => Nodes.Find(n => n.Id == id);

        /// <summary>The points the team walks through for the two choices at the forks (6 to 8 of them).</summary>
        public List<int> Walk(RoutePath first, RoutePath second)
        {
            var walk = new List<int> { GameSession.RouteCommon(0) };
            walk.AddRange(Branch(0, first));
            walk.Add(GameSession.RouteCommon(1));
            walk.AddRange(Branch(1, second));
            walk.Add(GameSession.RouteCommon(2));
            walk.Add(GameSession.RouteCommon(3));
            return walk;
        }

        public IEnumerable<int> Branch(int fork, RoutePath path) =>
            Nodes.Where(n => n.Fork == fork && n.Path == path).OrderBy(n => n.X).Select(n => n.Id);
    }

    /// <summary>
    /// ACTIVITIES.md 4, MODES.md 6: expedition routes for Blueprints. A map of points with two forks: the safe way
    /// (one point), the risky one (more Blueprints, a hard check) or the long one (more materials); 6 to 8 points
    /// in all, 4 to 8 hours. Points are passed one by one as the time goes; each is decided by a stat of the team,
    /// and a fight can be played by hand for 30 seconds. On the way the team finds notes about the world and pieces
    /// of maps for the Treasure hunters; at home 1–2 decision cards wait. Items drop on return; the illness from
    /// the fog may come back with the team (GameSession.Rooms.cs).
    /// </summary>
    public sealed partial class GameSession
    {
        public static readonly RouteCard[] RouteCards =
        {
            new RouteCard { Title = "Беженцы просят взять их с собой", OptionA = "Взять (−20 еды, +житель)", OptionB = "Указать путь (+1 чертёж)" },
            new RouteCard { Title = "Старый тайник Стражей", OptionA = "Вскрыть (проверка Ума: +3 чертежа или ранение)", OptionB = "Оставить" },
            new RouteCard { Title = "Раненый следопыт у дороги", OptionA = "Вылечить (−30 еды, +2 чертежа)", OptionB = "Расспросить (+час материалов)" },
            new RouteCard { Title = "Свежие следы чудовища", OptionA = "Проследить (+1 след Охоты)", OptionB = "Обойти (+1 чертёж)" },
            new RouteCard { Title = "Гнилой мост", OptionA = "Перейти (проверка Ловкости: +2 чертежа или ранение)", OptionB = "Обойти (+полчаса материалов)" },
            new RouteCard { Title = "Заброшенная часовня", OptionA = "Помолиться (+эссенция)", OptionB = "Обыскать (+записка о мире)" },
        };

        /// <summary>Notes about the world (ACTIVITIES.md 4, «записки о мире»): found in order, kept in the journal.</summary>
        public static readonly (string title, string text)[] RouteNotes =
        {
            ("Последняя запись дозорного", "«Туман пришёл с севера без ветра. Огни на перевале гасли один за другим, будто их задували изнутри.»"),
            ("Список на двери склада", "«Соль — 3 мешка. Уголь — до весны. Надежда — по списку не значится». Чья-то рука дописала: «значится».") ,
            ("Письмо без адресата", "«Если найдёшь это — иди на свет в горе. Там очаг, который не гаснет. Не оборачивайся на голоса в тумане.»"),
            ("Устав Стражей, лист третий", "«Страж не уходит от огня, пока у огня есть хоть один, кто не может уйти сам.»"),
            ("Детский рисунок", "Гора, в ней огонёк, вокруг человечки. Подпись кривыми буквами: «наш дом».") ,
            ("Записка торговца", "«Бледный Двор платит серебром, но серебро это холодное. Кто взял — тот потом не спит.»"),
            ("Обрывок карты перевалов", "Тропы отмечены углём. Одна обведена дважды и подписана: «здесь туман тоньше».") ,
            ("Молитва угольщиков", "«Тлей, но не гасни. Грей, но не жги. Свети тем, кто в пути.»"),
            ("Дневник рудокопа", "«Глубже двухсот саженей камень тёплый. Старики говорят — это сердце горы. Я думаю — это кто-то спит.»"),
            ("Приказ коменданта", "«Ворота закрыть на закате. Открывать только на три удара в колокол и на голос, который узнаете.»"),
            ("Стих на стене таверны", "«Пока горит очаг — мы дома. Пока мы дома — горит очаг.»"),
            ("Отчёт разведчика", "«Корневые чудища идут за теплом. Чем ярче огонь, тем ближе они подходят — и тем лучше их видно.»"),
            ("Записка лекаря", "«Хворь из тумана не любит тепла и сна. Отдохнувший отряд приносит её вдвое реже.»"),
            ("Песня перевозчика", "«За рекой туман, за туманом лес, за лесом Стражи жгли огни до небес.»"),
            ("Печать Бледного Двора", "Сургуч холодный на ощупь даже у огня. Под печатью одно слово: «скоро».") ,
            ("Последняя страница летописи", "«Стражей было сто. Осталось семеро. Мы унесли огонь в гору и назвали его Последним очагом.»"),
        };

        static readonly Stat[] ForkStats = { Stat.Agility, Stat.Strength, Stat.Intellect };

        // the minutes of each leg: the common points 24, the safe branch 60, the risky 2 × 30, the long 2 × 90, home 24
        const int CommonMinutes = 24;
        const int HomeMinutes = 24;

        public static int RouteCommon(int k) => k;

        static int BranchId(int fork, RoutePath path, int k) => 4 + fork * 6 + (int)path * 2 + k;

        public const int RouteGateId = 99;

        public int RoutesLeft(long now)
        {
            if (State.RoutesDay != LocalDay(now))
            {
                State.RoutesDay = LocalDay(now);
                State.RoutesStarted = 0;
            }
            int squads = Balance.Activities.Routes.SquadsByLevel[State.HearthLevel - 1];
            return Math.Max(0, squads - State.RoutesStarted);
        }

        public int ActiveRoutes => State.Expeditions.Count(e => !e.Back);

        static int RouteNeed(int hearthLevel) => (int)Math.Round(3 * (5 + 2 * hearthLevel) * 0.9);

        /// <summary>The stat the risky branch of a fork tests and the team sum it needs (about 90% of a typical team of three).</summary>
        public (Stat stat, int need) ForkCheck(int seed, int fork, int hearthLevel)
        {
            var stat = ForkStats[(seed + fork) % ForkStats.Length];
            return (stat, RouteNeed(hearthLevel));
        }

        // ------------------------------------------------------------------ the map

        static readonly string[] FightNames = { "Засада тварей", "Волки тумана", "Одержимые у брода" };
        static readonly string[] DangerNames = { "Ловушки Бледного Двора", "Обвал на перевале", "Топь" };
        static readonly string[] CacheNames = { "Брошенный обоз", "Тайник контрабандистов", "Пустой хутор" };

        /// <summary>The map of a route; the planner shows the next one (seed State.NextBattleSeed).</summary>
        public RouteMap RouteMapOf(int seed, int hearthLevel)
        {
            var map = new RouteMap();
            var rng = new Rng(seed * 7919 + 17);
            int need = RouteNeed(hearthLevel), soft = (int)Math.Round(need * 0.7);
            string Pick(string[] names) => names[(int)(rng.Next() * names.Length) % names.Length];

            map.Nodes.Add(new RouteNode { Id = RouteGateId, Type = RoutePoint.Gate, Name = "Ворота очага", X = 0, Y = 0 });
            // the first point: a cart to scout or a skirmish
            bool fight = rng.Next() < 0.5;
            map.Nodes.Add(fight
                ? new RouteNode { Id = 0, Type = RoutePoint.Fight, Stat = Stat.Strength, Need = soft, Name = Pick(FightNames), X = 0.12f, Minutes = CommonMinutes }
                : new RouteNode { Id = 0, Type = RoutePoint.Resource, Stat = Stat.Agility, Need = soft, Name = Pick(CacheNames), X = 0.12f, Minutes = CommonMinutes });
            map.Nodes.Add(new RouteNode { Id = 1, Type = RoutePoint.Event, Stat = Stat.Spirit, Need = soft, Name = "Встреча на дороге", X = 0.48f, Minutes = CommonMinutes });
            map.Nodes.Add(new RouteNode { Id = 2, Type = RoutePoint.Rest, Stat = Stat.Endurance, Need = soft, Name = "Стоянка у скалы", X = 0.84f, Minutes = CommonMinutes });
            map.Nodes.Add(new RouteNode { Id = 3, Type = RoutePoint.Resource, Stat = Stat.Intellect, Need = soft, Name = "Руины Стражей", Lore = true, X = 0.96f, Minutes = CommonMinutes });
            for (int f = 0; f < 2; f++)
            {
                float a = f == 0 ? 0.24f : 0.60f, b = a + 0.12f, mid = (a + b) / 2;
                var (stat, keyNeed) = ForkCheck(seed, f, hearthLevel);
                map.Nodes.Add(new RouteNode { Id = BranchId(f, RoutePath.Safe, 0), Fork = f, Path = RoutePath.Safe, Type = RoutePoint.Event, Stat = Stat.Spirit, Need = soft,
                                              Name = "Деревня беженцев", X = mid, Y = -1, Minutes = 60 });
                map.Nodes.Add(stat == Stat.Strength
                    ? new RouteNode { Id = BranchId(f, RoutePath.Risky, 0), Fork = f, Path = RoutePath.Risky, Type = RoutePoint.Fight, Stat = stat, Need = keyNeed, Key = true,
                                      Name = Pick(FightNames), X = a, Y = 0, Minutes = 30 }
                    : new RouteNode { Id = BranchId(f, RoutePath.Risky, 0), Fork = f, Path = RoutePath.Risky, Type = RoutePoint.Danger, Stat = stat, Need = keyNeed, Key = true,
                                      Name = stat == Stat.Intellect ? DangerNames[0] : DangerNames[1 + (int)(rng.Next() * 2) % 2], X = a, Y = 0, Minutes = 30 });
                map.Nodes.Add(new RouteNode { Id = BranchId(f, RoutePath.Risky, 1), Fork = f, Path = RoutePath.Risky, Type = RoutePoint.Resource, Stat = Stat.Intellect, Need = soft,
                                              Name = "Тайник Стражей", X = b, Y = 0, Minutes = 30 });
                map.Nodes.Add(new RouteNode { Id = BranchId(f, RoutePath.Long, 0), Fork = f, Path = RoutePath.Long, Type = RoutePoint.Resource, Stat = Stat.Endurance, Need = soft,
                                              Name = "Дальние склады", X = a, Y = 1, Minutes = 90 });
                map.Nodes.Add(new RouteNode { Id = BranchId(f, RoutePath.Long, 1), Fork = f, Path = RoutePath.Long, Type = RoutePoint.Rest, Stat = Stat.Endurance, Need = soft,
                                              Name = "Родник в ущелье", X = b, Y = 1, Minutes = 90 });
            }
            // the lines: the gate, the first point, the three branches of each fork, the points between, the ruins
            map.Edges.Add((RouteGateId, 0));
            for (int f = 0; f < 2; f++)
            {
                int before = f == 0 ? 0 : 1, after = f == 0 ? 1 : 2;
                foreach (RoutePath p in Enum.GetValues(typeof(RoutePath)))
                {
                    var branch = map.Branch(f, p).ToList();
                    map.Edges.Add((before, branch[0]));
                    for (int k = 1; k < branch.Count; k++)
                        map.Edges.Add((branch[k - 1], branch[k]));
                    map.Edges.Add((branch[branch.Count - 1], after));
                }
            }
            map.Edges.Add((2, 3));
            return map;
        }

        public RouteMap RouteMapOf(Expedition e) => RouteMapOf(e.Seed, Math.Max(1, e.HearthLevel));

        /// <summary>The next route's map, as the planner shows it before the team leaves.</summary>
        public RouteMap NextRouteMap() => RouteMapOf(State.NextBattleSeed, State.HearthLevel);

        public List<int> RouteWalk(Expedition e)
        {
            var forks = e.Forks != null && e.Forks.Length == 2 ? e.Forks : new[] { RoutePath.Safe, RoutePath.Safe };
            return RouteMapOf(e).Walk(forks[0], forks[1]);
        }

        /// <summary>Real seconds a route takes: 4 to 8 hours, less with the specializations that shorten the way.</summary>
        long RouteSeconds(RouteMap map, List<int> walk)
        {
            long minutes = walk.Sum(id => map.Node(id).Minutes) + HomeMinutes;
            return (long)Math.Round(minutes * 60 * (1 - Math.Min(0.5, SpecSum(SpecEffect.RouteSpeed))));
        }

        /// <summary>When the team reaches each point of its walk (the times stretch to the route's start and end).</summary>
        public List<long> RouteTimes(Expedition e)
        {
            var map = RouteMapOf(e);
            var walk = RouteWalk(e);
            double total = walk.Sum(id => map.Node(id).Minutes) + HomeMinutes, at = 0;
            var times = new List<long>();
            foreach (int id in walk)
            {
                at += map.Node(id).Minutes;
                times.Add(e.Start + (long)Math.Round((e.End - e.Start) * at / total));
            }
            return times;
        }

        /// <summary>The point the team walks to now (its walk index), or the walk's length when all are passed.</summary>
        public int RouteNextPoint(Expedition e) => Math.Min(e.Passed, RouteWalk(e).Count);

        // ------------------------------------------------------------------ leaving

        public Fail StartRoute(IList<int> team, RoutePath first, RoutePath second, long now, out Expedition route)
        {
            route = null;
            Advance(now);
            var r = Balance.Activities.Routes;
            if (State.HearthLevel < r.Unlock)
                return Fail.Locked;
            if (team.Count < 1 || team.Count > 3)
                return Fail.NoHeroes;
            foreach (int id in team)
            {
                var d = FindDweller(id);
                if (d == null || d.RouteId != 0 || d.InjuredUntil > now)
                    return Fail.NotFound;
            }
            if (RoutesLeft(now) <= 0)
                return Fail.NoTracks;
            State.RoutesStarted++;
            int seed = State.NextBattleSeed++;
            var map = RouteMapOf(seed, State.HearthLevel);
            route = new Expedition
            {
                Id = State.NextExpeditionId++,
                Team = team.ToList(),
                Forks = new[] { first, second },
                Start = now,
                End = now + RouteSeconds(map, map.Walk(first, second)),
                Seed = seed,
                HearthLevel = State.HearthLevel,
            };
            foreach (int id in team)
            {
                Unassign(id);
                FindDweller(id).RouteId = route.Id;
            }
            State.Expeditions.Add(route);
            return Fail.None;
        }

        /// <summary>A fork can be chosen again while the team has not reached it; the way gets longer or shorter.</summary>
        public Fail ChooseFork(int expeditionId, int fork, RoutePath path, long now)
        {
            Advance(now);
            var e = State.Expeditions.Find(x => x.Id == expeditionId);
            if (e == null || e.Back || fork < 0 || fork > 1)
                return Fail.NotFound;
            var map = RouteMapOf(e);
            var walk = RouteWalk(e);
            int firstOfFork = walk.IndexOf(map.Branch(fork, e.Forks[fork]).First());
            if (e.Passed >= firstOfFork)
                return Fail.NotRunning; // the team is already past the fork
            if (e.Forks[fork] == path)
                return Fail.None;
            long before = RouteSeconds(map, walk);
            e.Forks[fork] = path;
            long after = RouteSeconds(map, RouteWalk(e));
            var times = RouteTimes(e);
            long passedAt = e.Passed > 0 ? times[e.Passed - 1] : e.Start;
            e.End = Math.Max(Math.Max(now, passedAt) + 60, e.End + after - before);
            return Fail.None;
        }

        // ------------------------------------------------------------------ on the way

        /// <summary>Called by Advance: the teams pass the points they reached; the ones back bring their loot and the cards.</summary>
        void ReturnRoutes(long now)
        {
            foreach (var e in State.Expeditions.Where(x => !x.Back).ToList())
            {
                WalkRoute(e, Math.Min(now, e.End));
                if (e.End <= now)
                    FinishRoute(e);
            }
        }

        void WalkRoute(Expedition e, long until)
        {
            var walk = RouteWalk(e);
            var times = RouteTimes(e);
            if (e.Log == null)
                e.Log = new List<string>();
            while (e.Passed < walk.Count && times[e.Passed] <= until)
            {
                PassPoint(e, walk[e.Passed]);
                e.Passed++;
            }
        }

        /// <summary>A check of the team: the sum of a stat × 0.8–1.2 against the need (the watchtower's specializations lower it).</summary>
        bool RouteCheck(List<Dweller> team, Stat stat, int need, Rng rng)
        {
            int have = team.Sum(d => d.StatOf(stat));
            return have * (0.8 + 0.4 * rng.Next()) >= need * (1 - Math.Min(0.5, SpecSum(SpecEffect.RouteCheck)));
        }

        void PassPoint(Expedition e, int nodeId)
        {
            var r = Balance.Activities.Routes;
            var node = RouteMapOf(e).Node(nodeId);
            var team = e.Team.Select(FindDweller).Where(d => d != null).ToList();
            var rng = new Rng(e.Seed * 104729 + nodeId * 7907 + 3);
            bool ok = RouteCheck(team, node.Stat, node.Need, rng);
            string line = node.Name + ": ";
            switch (node.Type)
            {
                case RoutePoint.Fight:
                    int hand = e.Skirmish != null && e.Skirmish.TryGetValue(nodeId, out int s) ? s : -1;
                    if (hand >= 0)
                        ok = hand == 1;
                    if (node.Key)
                    {
                        e.Share += ok ? 0.7 + (hand == 1 ? r.SkirmishBonus : 0) : 0.1;
                        if (!ok)
                            e.Hurt++;
                    }
                    else
                    {
                        e.MatHours += (ok ? 1 : 0.5) * 0.5 * r.MaterialHours;
                        e.Share += ok ? 0.1 + (hand == 1 ? r.SkirmishBonus : 0) : 0;
                    }
                    line += (ok ? "схватка выиграна" : "отступили с потерями") + (hand >= 0 ? " (бой вручную)" : "");
                    break;
                case RoutePoint.Danger:
                    e.Share += ok ? 0.7 : 0.1;
                    if (!ok)
                        e.Hurt++;
                    line += ok ? "прошли" : "ранение";
                    break;
                case RoutePoint.Event:
                    if (node.Fork >= 0)
                        e.Share += 0.4; // the safe way
                    int card = (int)(rng.Next() * RouteCards.Length) % RouteCards.Length;
                    if (e.Card < 0)
                        e.Card = card;
                    else if (e.MoreCards.Count + 1 < r.CardsMax)
                        e.MoreCards.Add(card);
                    line += "встретили людей — решить дома: «" + RouteCards[card].Title + "»";
                    break;
                case RoutePoint.Rest:
                    if (node.Fork >= 0)
                        e.Share += 0.175; // the long way
                    if (ok)
                        e.Rests++;
                    line += ok ? "хорошо отдохнули" : "отдых не задался";
                    break;
                default: // resources
                    if (node.Fork < 0)
                    {
                        double h = 0.5 * r.MaterialHours;
                        e.MatHours += ok ? h * 1.25 : h;
                    }
                    else if (node.Path == RoutePath.Long)
                    {
                        e.Share += 0.175;
                        e.MatHours += 0.5 * r.MaterialHours;
                    }
                    line += ok ? "нашли больше припасов" : "кое-что нашли";
                    break;
            }
            // pieces of maps: the cache of the risky way and the far stores of the long one
            double piece = node.Fork >= 0 && node.Type == RoutePoint.Resource
                ? (node.Path == RoutePath.Risky ? r.CacheFragmentChance : r.FarFragmentChance) + SpecSum(SpecEffect.FragmentChance)
                : 0;
            if (piece > 0 && rng.Next() < piece)
            {
                e.Fragments++;
                line += ", обломок карты";
            }
            // the ruins keep notes about the world
            if (node.Lore && rng.Next() < r.NoteChance + SpecSum(SpecEffect.NoteChance) + (ok ? 0.2 : 0))
            {
                int note = NextNote(e);
                if (note >= 0)
                {
                    e.Notes.Add(note);
                    line += ", записка «" + RouteNotes[note].title + "»";
                }
            }
            e.Log.Add(line);
        }

        /// <summary>The next note not found yet (in the order of the story), or -1 when all are found.</summary>
        int NextNote(Expedition e)
        {
            for (int i = 0; i < RouteNotes.Length; i++)
            {
                if (!State.RouteNotes.Contains(i) && (e == null || !e.Notes.Contains(i)))
                    return i;
            }
            return -1;
        }

        /// <summary>The illness from the fog is rarer after good rests on the way (GameSession.Rooms.cs SettleIllness).</summary>
        double RouteRestGuard(Expedition e) => Math.Max(0.2, 1 - Balance.Activities.Routes.RestGuard * e.Rests);

        void FinishRoute(Expedition e)
        {
            e.Back = true;
            var r = Balance.Activities.Routes;
            int L = Math.Max(1, e.HearthLevel);
            var rng = new Rng(e.Seed);
            var team = e.Team.Select(FindDweller).Where(d => d != null).ToList();
            long bp = (long)Math.Round(r.BlueprintsByLevel[L - 1] * e.Share);
            long mats = (long)Math.Round(r.IncomeByLevel[L - 1] * e.MatHours);
            long essence = (long)Math.Round(Balance.Activities.EssenceByLevel[L - 1] * r.EssenceShare);
            State.Wallet[ResourceType.Blueprints] += bp;
            State.Wallet[ResourceType.Essence] += essence;
            State.Rewards.Add(ResourceBag.Materials(mats, Balance.SilverShare));
            var lines = new List<string>();
            if (rng.Next() < 0.15 + SpecSum(SpecEffect.RefugeeChance) && !PopulationFull())
            {
                RefugeesArrive(1);
                lines.Add("привели беженца");
            }
            foreach (var d in team)
            {
                d.RouteId = 0;
                if (e.Hurt > 0 && d == team[0])
                    d.InjuredUntil = e.End + 6 * 3600;
            }
            foreach (int note in e.Notes.Where(n => !State.RouteNotes.Contains(n)))
                State.RouteNotes.Add(note);
            if (e.Notes.Count > 0)
                lines.Add("записок: " + e.Notes.Count);
            if (e.Fragments > 0)
            {
                State.MapFragments += e.Fragments;
                lines.Add("обломков карт: " + e.Fragments);
            }
            if (e.Card < 0)
                e.Card = (int)(rng.Next() * RouteCards.Length) % RouteCards.Length; // every route brings a card
            var item = Drop("route", L, e.Seed, "Вылазка"); // its own generator: the route's numbers stay the same
            if (item != null)
                lines.Add("нашли: " + ItemName(item) + " (" + ItemRarityInfo(item.Rarity).Name + ")");
            if (e.Hurt > 0)
                lines.Add("ранение");
            e.Report = string.Format("{0}{1}чертежей {2}, материалов {3}, эссенции {4}", string.Join(", ", lines), lines.Count > 0 ? "; " : "", bp, mats, essence);
            Count(DailyTask.Route);
        }

        /// <summary>The current decision card of a returned team (option 1 or 2); the expedition closes after its last card.</summary>
        public Fail ResolveRouteCard(int expeditionId, int option, long now)
        {
            Advance(now);
            var e = State.Expeditions.Find(x => x.Id == expeditionId);
            if (e == null || !e.Back)
                return Fail.NotFound;
            if (e.Card >= 0)
            {
                var rng = new Rng(e.Seed * 31 + 7 + e.MoreCards.Count * 1009);
                var team = e.Team.Select(FindDweller).Where(d => d != null).ToList();
                int L = Math.Max(1, e.HearthLevel);
                switch (e.Card * 2 + (option == 1 ? 0 : 1))
                {
                    case 0:
                        State.Wallet[ResourceType.Food] = Math.Max(0, State.Wallet[ResourceType.Food] - 20);
                        RefugeesArrive(1);
                        break;
                    case 1:
                    case 7:
                        State.Wallet[ResourceType.Blueprints] += 1;
                        break;
                    case 2:
                    case 8:
                        var stat = e.Card == 1 ? Stat.Intellect : Stat.Agility;
                        int sum = team.Sum(d => d.StatOf(stat));
                        if (sum * (0.8 + 0.4 * rng.Next()) >= RouteNeed(L))
                            State.Wallet[ResourceType.Blueprints] += e.Card == 1 ? 3 : 2;
                        else if (team.Count > 0)
                            team[0].InjuredUntil = Math.Max(team[0].InjuredUntil, now + 6 * 3600);
                        break;
                    case 4:
                        State.Wallet[ResourceType.Food] = Math.Max(0, State.Wallet[ResourceType.Food] - 30);
                        State.Wallet[ResourceType.Blueprints] += 2;
                        break;
                    case 5:
                        State.Rewards.Add(ResourceBag.Materials(Balance.Activities.Routes.IncomeByLevel[L - 1], Balance.SilverShare));
                        break;
                    case 6:
                        State.HuntTracks = Math.Min(State.HuntTracks + 1, Balance.Activities.Hunt.TracksPerDay + 1);
                        break;
                    case 9:
                        State.Rewards.Add(ResourceBag.Materials(Balance.Activities.Routes.IncomeByLevel[L - 1] / 2, Balance.SilverShare));
                        break;
                    case 10:
                        State.Wallet[ResourceType.Essence] += (long)Math.Round(Balance.Activities.EssenceByLevel[L - 1] * Balance.Activities.Routes.EssenceShare);
                        break;
                    case 11:
                        int note = NextNote(null);
                        if (note >= 0)
                            State.RouteNotes.Add(note);
                        break;
                }
            }
            if (e.MoreCards != null && e.MoreCards.Count > 0)
            {
                e.Card = e.MoreCards[0];
                e.MoreCards.RemoveAt(0);
                ClaimRewards();
                return Fail.None;
            }
            State.Expeditions.Remove(e);
            ClaimRewards();
            return Fail.None;
        }

        // ------------------------------------------------------------------ the fight by hand

        CombatBalance skirmishNumbers, skirmishFrom;

        /// <summary>The battle numbers with the time cut to the skirmish's 30 seconds.</summary>
        CombatBalance SkirmishNumbers()
        {
            if (skirmishNumbers == null || skirmishFrom != Combat)
            {
                var c = CombatBalance.FromJson(JsonConvert.SerializeObject(Combat));
                c.MaxTicks = (int)Math.Round(Balance.Activities.Routes.SkirmishSeconds / Math.Max(0.01, c.TickSeconds));
                c.StarTicks = c.MaxTicks * 2 / 3;
                skirmishNumbers = c;
                skirmishFrom = Combat;
            }
            return skirmishNumbers;
        }

        /// <summary>The next fight on the team's way that can still be played by hand, or null.</summary>
        public RouteNode NextSkirmish(Expedition e)
        {
            if (e == null || e.Back || Combat == null)
                return null;
            var map = RouteMapOf(e);
            var walk = RouteWalk(e);
            for (int i = e.Passed; i < walk.Count; i++)
            {
                var n = map.Node(walk[i]);
                if (n.Type == RoutePoint.Fight)
                    return e.Skirmish != null && e.Skirmish.ContainsKey(n.Id) ? null : n;
            }
            return null;
        }

        static HeroClass ClassOfStat(Stat s)
        {
            switch (s)
            {
                case Stat.Strength: return HeroClass.Swordsman;
                case Stat.Agility: return HeroClass.Archer;
                case Stat.Intellect: return HeroClass.AshMage;
                case Stat.Endurance: return HeroClass.Guardian;
                default: return HeroClass.CoalPriest;
            }
        }

        /// <summary>
        /// The team fights the next skirmish of its way by hand: 30 seconds against an enemy as strong as the team
        /// (heroes with their gear, the others by their best stat). Leaving the battle leaves the point to the stats.
        /// </summary>
        public Fail StartRouteSkirmish(int expeditionId, long now, out Battle battle)
        {
            battle = null;
            Advance(now);
            var e = State.Expeditions.Find(x => x.Id == expeditionId);
            if (e == null || e.Back)
                return Fail.NotFound;
            var node = NextSkirmish(e);
            if (node == null)
                return Fail.NotRunning;
            var team = e.Team.Select(FindDweller).Where(d => d != null).ToList();
            if (team.Count == 0)
                return Fail.NoHeroes;
            var r = Balance.Activities.Routes;
            var setup = new BattleSetup { Seed = e.Seed * 13 + node.Id };
            foreach (var d in team)
            {
                var best = Enumerable.Range(0, 5).OrderByDescending(k => d.Stats[k]).First();
                setup.Heroes.Add(new HeroSetup
                {
                    Id = d.Id,
                    Name = d.Name,
                    Class = d.IsHero ? d.HeroClass : ClassOfStat((Stat)best),
                    Level = Math.Max(1, d.IsHero ? d.HeroLevel : e.HearthLevel),
                    SkillLevel = Math.Max(1, d.SkillLevel),
                    Stats = (int[])d.Stats.Clone(),
                    Gear = d.IsHero ? HeroGear(d) : null,
                });
            }
            var families = (MonsterFamily[])Enum.GetValues(typeof(MonsterFamily));
            setup.Waves.Add(new WaveSpec
            {
                Family = families[(e.Seed + node.Id) % families.Length], Rank = Math.Max(1, e.HearthLevel), Mirror = true,
                HpMult = r.SkirmishHp, AttackMult = r.SkirmishAttack,
            });
            if (e.Skirmish == null)
                e.Skirmish = new Dictionary<int, int>();
            e.Skirmish[node.Id] = -1; // until the battle ends: left unfinished, the stats decide
            battle = new Battle(SkirmishNumbers(), setup);
            return Fail.None;
        }

        /// <summary>Writes down how the skirmish went; the point uses it when the team gets there.</summary>
        public Fail FinishRouteSkirmish(int expeditionId, Battle battle, out string result)
        {
            result = "";
            var e = State.Expeditions.Find(x => x.Id == expeditionId);
            if (e == null || e.Skirmish == null || battle == null)
                return Fail.NotFound;
            var node = RouteMapOf(e).Nodes.Find(n => e.Skirmish.TryGetValue(n.Id, out int v) && v == -1);
            if (node == null)
                return Fail.NotFound;
            if (!battle.Over)
                battle.RunToEnd();
            e.Skirmish[node.Id] = battle.Won ? 1 : 0;
            result = battle.Won
                ? string.Format("Схватка «{0}» выиграна: отряд пройдёт её без потерь{1}", node.Name, Balance.Activities.Routes.SkirmishBonus > 0 ? " и принесёт больше чертежей" : "")
                : string.Format("Схватка «{0}» проиграна: {1}", node.Name, node.Key ? "будет ранение" : "отряд отступит");
            return Fail.None;
        }

        // ------------------------------------------------------------------ notes and pieces of maps

        /// <summary>A piece of a map for the Treasure hunters: on the current map it crosses out a third of the places.</summary>
        public Fail UseMapFragment(long now)
        {
            Advance(now);
            if (State.MapFragments <= 0)
                return Fail.NotEnough;
            var run = ActiveRun("Treasure", now);
            if (run == null)
                return Fail.NotRunning;
            if (run.RouteFragments >= Balance.Activities.Routes.FragmentsPerMap)
                return Fail.LimitReached;
            State.MapFragments--;
            run.RouteFragments++;
            run.Pieces++;
            return Fail.None;
        }

        public bool CanUseMapFragment(long now)
        {
            var run = ActiveRun("Treasure", now);
            return State.MapFragments > 0 && run != null && run.RouteFragments < Balance.Activities.Routes.FragmentsPerMap;
        }
    }
}
