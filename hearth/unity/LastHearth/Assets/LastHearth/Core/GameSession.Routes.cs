using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core.Combat;

namespace LastHearth.Core
{
    public sealed class RouteCard
    {
        public string Title = "";
        public string OptionA = "";
        public string OptionB = "";
    }

    /// <summary>ACTIVITIES.md, 4: expedition routes with forks and decision cards, for Blueprints.</summary>
    public sealed partial class GameSession
    {
        const long RouteBaseSeconds = 2 * 3600;

        public static readonly RouteCard[] RouteCards =
        {
            new RouteCard { Title = "Беженцы просят взять их с собой", OptionA = "Взять (−20 еды, +житель)", OptionB = "Указать путь (+1 чертёж)" },
            new RouteCard { Title = "Старый тайник Стражей", OptionA = "Вскрыть (проверка Ума: +3 чертежа или ранение)", OptionB = "Оставить" },
            new RouteCard { Title = "Раненый следопыт у дороги", OptionA = "Вылечить (−30 еды, +2 чертежа)", OptionB = "Расспросить (+час материалов)" },
            new RouteCard { Title = "Свежие следы чудовища", OptionA = "Проследить (+1 след Охоты)", OptionB = "Обойти (+1 чертёж)" },
        };

        static readonly Stat[] ForkStats = { Stat.Agility, Stat.Strength, Stat.Intellect };

        static long PathSeconds(RoutePath p) => p == RoutePath.Long ? 3 * 3600 : 3600;

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

        /// <summary>The stat a fork tests and the team sum it needs (about 90% of a typical team of three).</summary>
        public (Stat stat, int need) ForkCheck(int seed, int fork, int hearthLevel)
        {
            var stat = ForkStats[(seed + fork) % ForkStats.Length];
            int need = (int)Math.Round(3 * (5 + 2 * hearthLevel) * 0.9);
            return (stat, need);
        }

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
            route = new Expedition
            {
                Id = State.NextExpeditionId++,
                Team = team.ToList(),
                Forks = new[] { first, second },
                Start = now,
                End = now + RouteBaseSeconds + PathSeconds(first) + PathSeconds(second),
                Seed = State.NextBattleSeed++,
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

        /// <summary>Called by Advance: teams that are back bring their loot and a decision card.</summary>
        void ReturnRoutes(long now)
        {
            foreach (var e in State.Expeditions.Where(x => !x.Back && x.End <= now).ToList())
            {
                e.Back = true;
                var r = Balance.Activities.Routes;
                int L = e.HearthLevel;
                var rng = new Rng(e.Seed);
                var team = e.Team.Select(FindDweller).Where(d => d != null).ToList();
                double bpShare = 0, matHours = r.MaterialHours, injured = 0;
                var lines = new List<string>();
                for (int f = 0; f < 2; f++)
                {
                    var (stat, need) = ForkCheck(e.Seed, f, L);
                    switch (e.Forks[f])
                    {
                        case RoutePath.Safe:
                            bpShare += 0.4;
                            lines.Add("безопасный путь");
                            break;
                        case RoutePath.Long:
                            bpShare += 0.35;
                            matHours += 0.5 * r.MaterialHours;
                            lines.Add("долгий путь: больше материалов");
                            break;
                        default:
                            int have = team.Sum(d => d.StatOf(stat));
                            bool ok = have * (0.8 + 0.4 * rng.Next()) >= need;
                            bpShare += ok ? 0.7 : 0.1;
                            if (!ok)
                                injured++;
                            lines.Add(ok ? "рискованный путь пройден" : "рискованный путь: ранение");
                            break;
                    }
                }
                long bp = (long)Math.Round(r.BlueprintsByLevel[L - 1] * bpShare);
                long mats = (long)Math.Round(r.IncomeByLevel[L - 1] * matHours);
                long essence = (long)Math.Round(Balance.Activities.EssenceByLevel[L - 1] * r.EssenceShare);
                State.Wallet[ResourceType.Blueprints] += bp;
                State.Wallet[ResourceType.Essence] += essence;
                State.Rewards.Add(ResourceBag.Materials(mats, Balance.SilverShare));
                if (rng.Next() < 0.15 && !PopulationFull())
                {
                    RefugeesArrive(1);
                    lines.Add("привели беженца");
                }
                foreach (var d in team)
                {
                    d.RouteId = 0;
                    if (injured > 0 && d == team[0])
                        d.InjuredUntil = e.End + 6 * 3600;
                }
                e.Card = (int)(rng.Next() * RouteCards.Length);
                var item = Drop("route", L, e.Seed, "Вылазка"); // its own generator: the route's numbers stay the same
                if (item != null)
                    lines.Add("нашли: " + ItemName(item) + " (" + ItemRarityInfo(item.Rarity).Name + ")");
                e.Report = string.Format("{0}; чертежей {1}, материалов {2}, эссенции {3}", string.Join(", ", lines), bp, mats, essence);
                Count(DailyTask.Route);
            }
        }

        /// <summary>The decision card of a returned team (option 1 or 2); closes the expedition.</summary>
        public Fail ResolveRouteCard(int expeditionId, int option, long now)
        {
            Advance(now);
            var e = State.Expeditions.Find(x => x.Id == expeditionId);
            if (e == null || !e.Back)
                return Fail.NotFound;
            if (e.Card >= 0)
            {
                var rng = new Rng(e.Seed * 31 + 7);
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
                        var team = e.Team.Select(FindDweller).Where(d => d != null).ToList();
                        int mind = team.Sum(d => d.StatOf(Stat.Intellect));
                        if (mind * (0.8 + 0.4 * rng.Next()) >= ForkCheck(e.Seed, 0, e.HearthLevel).need)
                            State.Wallet[ResourceType.Blueprints] += 3;
                        else if (team.Count > 0)
                            team[0].InjuredUntil = Math.Max(team[0].InjuredUntil, now + 6 * 3600);
                        break;
                    case 4:
                        State.Wallet[ResourceType.Food] = Math.Max(0, State.Wallet[ResourceType.Food] - 30);
                        State.Wallet[ResourceType.Blueprints] += 2;
                        break;
                    case 5:
                        State.Rewards.Add(ResourceBag.Materials(Balance.Activities.Routes.IncomeByLevel[e.HearthLevel - 1], Balance.SilverShare));
                        break;
                    case 6:
                        State.HuntTracks = Math.Min(State.HuntTracks + 1, Balance.Activities.Hunt.TracksPerDay + 1);
                        break;
                }
            }
            State.Expeditions.Remove(e);
            ClaimRewards();
            return Fail.None;
        }
    }
}
