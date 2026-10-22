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

    /// <summary>
    /// ACTIVITIES.md 2: the Heart of the Hearth (Ember talents). It changes the numbers of the fortress,
    /// so it matters every day. Room specializations (Blueprints) are in GameSession.Specs.cs.
    /// </summary>
    public sealed partial class GameSession
    {
        // ------------------------------------------------------------------ Heart of the Hearth

        public int HeartTotal => State.Heart[0] + State.Heart[1] + State.Heart[2];

        public double WarmthMult() => (1 + Balance.Activities.Heart.Warmth * State.Heart[(int)HeartBranch.Warmth]) * NightWarmth();

        public double OutputMult() => (1 + Balance.Activities.Heart.Output * State.Heart[(int)HeartBranch.Economy]) * WarmWeekMult();

        public double DefenseMult() => 1 + Balance.Activities.Heart.Defense * State.Heart[(int)HeartBranch.Defense] + HallsDefenseBonus() + SpecSum(SpecEffect.SurgeAll);

        public double CoalBurnPerHour()
        {
            double save = 1 - Balance.Activities.Heart.Coal * State.Heart[(int)HeartBranch.Economy];
            save -= ActivePetValue(PetEffect.CoalBurn); // the Coal cat (GDD 8.1)
            save -= ResearchValue(ResearchEffect.CoalBurn); // the library (GameSession.Rooms.cs)
            save -= SpecSum(SpecEffect.CoalBurn); // «Ровный жар» of the coal mine (GameSession.Specs.cs)
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
    }
}
