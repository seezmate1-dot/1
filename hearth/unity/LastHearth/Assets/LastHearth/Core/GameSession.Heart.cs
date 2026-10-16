using System;
using System.Collections.Generic;

namespace LastHearth.Core
{
    public enum HeartBranch
    {
        Warmth,
        Economy,
        Defense,
    }

    public enum SpecEffect
    {
        None,
        Output,
        StockHours,
        NoFreeze,
        WorkerSlot,
        Defense,
    }

    public sealed class SpecOption
    {
        public string Name = "";
        public string Text = "";
        public SpecEffect Effect;
        public double Value;
    }

    /// <summary>
    /// ACTIVITIES.md 2 and 4: the Heart of the Hearth (Ember talents) and room specializations
    /// (Blueprints). Both change the numbers of the fortress, so they matter every day.
    /// </summary>
    public sealed partial class GameSession
    {
        // ------------------------------------------------------------------ Heart of the Hearth

        public int HeartTotal => State.Heart[0] + State.Heart[1] + State.Heart[2];

        public double WarmthMult() => 1 + Balance.Activities.Heart.Warmth * State.Heart[(int)HeartBranch.Warmth];

        public double OutputMult() => 1 + Balance.Activities.Heart.Output * State.Heart[(int)HeartBranch.Economy];

        public double DefenseMult() => 1 + Balance.Activities.Heart.Defense * State.Heart[(int)HeartBranch.Defense] + HallsDefenseBonus();

        public double CoalBurnPerHour()
        {
            double save = 1 - Balance.Activities.Heart.Coal * State.Heart[(int)HeartBranch.Economy];
            save -= ActivePetValue(PetEffect.CoalBurn); // the Coal cat (GDD 8.1)
            save -= ResearchValue(ResearchEffect.CoalBurn); // the library (GameSession.Rooms.cs)
            return Formulas.CoalBurnPerHour(Balance, State.HearthLevel, State.Mode) * Math.Max(0.1, save);
        }

        /// <summary>Price of the next talent (any branch) and the hearth level it needs.</summary>
        public (long cost, int gate) NextHeartNode()
        {
            var h = Balance.Activities.Heart;
            int n = HeartTotal;
            return n >= h.NodeCost.Length ? (0, int.MaxValue) : (h.NodeCost[n], h.NodeGate[n]);
        }

        public Fail BuyHeart(HeartBranch branch)
        {
            var h = Balance.Activities.Heart;
            if (State.Heart[(int)branch] >= h.PerBranch)
                return Fail.LimitReached;
            var (cost, gate) = NextHeartNode();
            if (State.HearthLevel < gate)
                return Fail.Locked;
            if (State.Wallet[ResourceType.Ember] < cost)
                return Fail.NotEnough;
            State.Wallet[ResourceType.Ember] -= cost;
            State.Heart[(int)branch]++;
            return Fail.None;
        }

        // ------------------------------------------------------------------ specializations

        /// <summary>Two options per tier; the tiers open at room level 5, 10, 15, 20 and 25.</summary>
        public static readonly SpecOption[][] SpecTiers =
        {
            new[] { new SpecOption { Name = "Усердие", Text = "+10% выработки", Effect = SpecEffect.Output, Value = 0.10 },
                    new SpecOption { Name = "Запасливость", Text = "+2 часа запаса", Effect = SpecEffect.StockHours, Value = 2 } },
            new[] { new SpecOption { Name = "Тёплые стены", Text = "не промерзает", Effect = SpecEffect.NoFreeze, Value = 1 },
                    new SpecOption { Name = "Лишние руки", Text = "+1 место работника", Effect = SpecEffect.WorkerSlot, Value = 1 } },
            new[] { new SpecOption { Name = "Мастерство", Text = "+15% выработки", Effect = SpecEffect.Output, Value = 0.15 },
                    new SpecOption { Name = "Дозор", Text = "+15% обороны этажа в Наплыве", Effect = SpecEffect.Defense, Value = 0.15 } },
            new[] { new SpecOption { Name = "Глубокие кладовые", Text = "+3 часа запаса", Effect = SpecEffect.StockHours, Value = 3 },
                    new SpecOption { Name = "Вторая смена", Text = "+1 место работника", Effect = SpecEffect.WorkerSlot, Value = 1 } },
            new[] { new SpecOption { Name = "Слава зала", Text = "+25% выработки", Effect = SpecEffect.Output, Value = 0.25 },
                    new SpecOption { Name = "Бастион", Text = "+25% обороны этажа в Наплыве", Effect = SpecEffect.Defense, Value = 0.25 } },
        };

        int[] Choices(RoomType type)
        {
            if (!State.SpecChoice.TryGetValue(type, out var c))
                State.SpecChoice[type] = c = new int[SpecTiers.Length];
            return c;
        }

        int[] Ranks(RoomType type)
        {
            if (!State.SpecRank.TryGetValue(type, out var r))
                State.SpecRank[type] = r = new int[SpecTiers.Length];
            return r;
        }

        public int SpecChosen(RoomType type, int tier) => Choices(type)[tier];

        public int SpecRankOf(RoomType type, int tier) => Ranks(type)[tier];

        /// <summary>Sum of an effect over the chosen options of the room's type; ranks scale numbers by 1, 1.5, 2.</summary>
        public double SpecValue(Room room, SpecEffect effect)
        {
            if (!State.SpecChoice.TryGetValue(room.Type, out var choices))
                return 0;
            var ranks = Ranks(room.Type);
            double total = 0;
            for (int t = 0; t < choices.Length; t++)
            {
                if (choices[t] == 0)
                    continue;
                var opt = SpecTiers[t][choices[t] - 1];
                if (opt.Effect != effect)
                    continue;
                bool scales = effect == SpecEffect.Output || effect == SpecEffect.StockHours || effect == SpecEffect.Defense;
                total += scales ? opt.Value * (1 + 0.5 * (Math.Max(1, ranks[t]) - 1)) : opt.Value;
            }
            return total;
        }

        public bool Specialized(Room room, SpecEffect effect) => SpecValue(room, effect) > 0;

        public int SpecTierLevel(int tier) => Balance.Activities.Specializations.Tiers[tier][0];

        public long SpecCost(int tier) => Balance.Activities.Specializations.Tiers[tier][1];

        public Fail BuySpec(RoomType type, int tier, int option)
        {
            if (tier < 0 || tier >= SpecTiers.Length || option < 1 || option > 2 || type == RoomType.Hearth)
                return Fail.NotFound;
            if (MaxRoomLevel(type) < SpecTierLevel(tier))
                return Fail.Locked;
            if (Choices(type)[tier] != 0)
                return Fail.AlreadyQueued;
            long cost = SpecCost(tier);
            if (State.Wallet[ResourceType.Blueprints] < cost)
                return Fail.NotEnough;
            State.Wallet[ResourceType.Blueprints] -= cost;
            Choices(type)[tier] = option;
            Ranks(type)[tier] = 1;
            return Fail.None;
        }

        /// <summary>Mastery from hearth 25: rank 2 costs x2 and rank 3 x3 of the tier price.</summary>
        public Fail RankUpSpec(RoomType type, int tier)
        {
            var sp = Balance.Activities.Specializations;
            if (Choices(type)[tier] == 0)
                return Fail.NotFound;
            if (State.HearthLevel < sp.RanksFromHearth)
                return Fail.Locked;
            int rank = Ranks(type)[tier];
            if (rank >= sp.Ranks)
                return Fail.LimitReached;
            long cost = SpecCost(tier) * (rank + 1);
            if (State.Wallet[ResourceType.Blueprints] < cost)
                return Fail.NotEnough;
            State.Wallet[ResourceType.Blueprints] -= cost;
            Ranks(type)[tier] = rank + 1;
            return Fail.None;
        }
    }
}
