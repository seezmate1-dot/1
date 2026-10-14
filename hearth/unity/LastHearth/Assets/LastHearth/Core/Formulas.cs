using System;

namespace LastHearth.Core
{
    /// <summary>Pure formulas of the GDD, all driven by the balance.</summary>
    public static class Formulas
    {
        /// <summary>GDD 7: T = 100 + 35 x (level - 1), times the hearth mode.</summary>
        public static double HearthWarmth(Balance b, int hearthLevel, HearthMode mode)
        {
            return (b.Warmth.Base + b.Warmth.PerLevel * (hearthLevel - 1)) * b.Mode(mode).Warmth;
        }

        /// <summary>GDD 7: warmth falls by 20% per floor away from the hearth.</summary>
        public static double FloorWarmth(Balance b, int hearthLevel, HearthMode mode, int floor)
        {
            int distance = Math.Abs(floor - b.Grid.HearthFloor);
            return HearthWarmth(b, hearthLevel, mode) * Math.Pow(b.Warmth.FloorFalloff, distance);
        }

        /// <summary>Warmth a room needs at its level; higher rooms want to sit closer to the hearth.</summary>
        public static double RoomWarmthNeed(Balance b, int roomLevel)
        {
            return b.Warmth.RoomNeedBase * Math.Pow(b.Warmth.RoomNeedGrowth, roomLevel - 1);
        }

        public static bool IsFloorHabitable(Balance b, int hearthLevel, HearthMode mode, int floor, int roomLevel)
        {
            return FloorWarmth(b, hearthLevel, mode, floor) >= RoomWarmthNeed(b, roomLevel);
        }

        /// <summary>Coal per hour: rooms use 85% of the hearth warmth, 10 warmth burn 1 coal (x mode).</summary>
        public static double CoalBurnPerHour(Balance b, int hearthLevel, HearthMode mode)
        {
            double steady = (b.Warmth.Base + b.Warmth.PerLevel * (hearthLevel - 1)) * b.CoalPerWarmthHour;
            return steady * b.Mode(mode).Coal;
        }

        /// <summary>GDD 6: output of one worker = base(room level) x (1 + stat / 25).</summary>
        public static double WorkerOutputPerHour(Balance b, ResourceType resource, int roomLevel, int stat)
        {
            return b.ProductionBase(resource, roomLevel) * (1 + stat / b.Production.StatDivisor);
        }

        /// <summary>GDD 8: dwellers allowed at this hearth level.</summary>
        public static int DwellerCap(Balance b, int hearthLevel)
        {
            return b.Dwellers.CapBase + b.Dwellers.CapPerLevel * (hearthLevel - 1);
        }

        public static int HeroLevelCap(Balance b, int hearthLevel)
        {
            return b.Heroes.CapPerHearth * hearthLevel + b.Heroes.CapBase;
        }

        /// <summary>Essence to raise a hero from this level to the next.</summary>
        public static long HeroLevelCost(Balance b, int heroLevel)
        {
            return (long)Math.Round(b.Heroes.LevelCostBase * Math.Pow(b.Heroes.LevelCostGrowth, heroLevel));
        }

        /// <summary>
        /// Diamonds to finish a timer: log-log between the balance points, the last 5 minutes free
        /// (ECONOMY.md, section 9).
        /// </summary>
        public static long SpeedupCost(Balance b, long seconds)
        {
            if (seconds <= b.Build.FreeFinishSeconds)
                return 0;
            double minutes = seconds / 60.0;
            var pts = b.SpeedupPoints;
            if (minutes <= pts[0][0])
                return pts[0][1];
            for (int i = 1; i < pts.Count; i++)
            {
                if (minutes <= pts[i][0])
                {
                    double t = (Math.Log(minutes) - Math.Log(pts[i - 1][0])) / (Math.Log(pts[i][0]) - Math.Log(pts[i - 1][0]));
                    return (long)Math.Ceiling(Math.Exp(Math.Log(pts[i - 1][1]) + (Math.Log(pts[i][1]) - Math.Log(pts[i - 1][1])) * t));
                }
            }
            var last = pts[pts.Count - 1];
            return (long)Math.Ceiling(last[1] * minutes / last[0]);
        }
    }
}
