using System.Collections.Generic;

namespace LastHearth.Core
{
    public enum RequirementKind
    {
        MaxLevel,
        AlreadyQueued,
        Warehouse,
        KeyRoom,
        Heroes,
        Materials,
        QueueFull,
    }

    /// <summary>One line of the checklist on the hearth screen: "Склад 11/12 -> улучшить".</summary>
    public sealed class Requirement
    {
        public RequirementKind Kind;
        public RoomType Room;
        public long Need;
        public long Have;

        public override string ToString() => Kind + (Kind == RequirementKind.KeyRoom ? " " + Room : "") + " " + Have + "/" + Need;
    }

    /// <summary>
    /// GDD 9.5, anti-rush: the hearth job L -> L+1 starts only when the warehouse and one key room are
    /// at level L and five heroes are at 2L - 2. The free player meets it on his own (ECONOMY.md, 11).
    /// </summary>
    public static class HearthRules
    {
        public static List<Requirement> Unmet(GameSession s, long now)
        {
            var unmet = new List<Requirement>();
            var b = s.Balance;
            var st = s.State;
            int level = st.HearthLevel;
            if (level >= b.MaxHearthLevel)
            {
                unmet.Add(new Requirement { Kind = RequirementKind.MaxLevel, Need = b.MaxHearthLevel, Have = level });
                return unmet;
            }
            if (st.Queue.IsQueued(BuildJob.HearthTarget))
                unmet.Add(new Requirement { Kind = RequirementKind.AlreadyQueued });
            var row = b.Level(level);

            if (level >= b.Rules.StorageGateFrom)
            {
                int warehouse = s.MaxRoomLevel(RoomType.Warehouse);
                if (warehouse < level)
                    unmet.Add(new Requirement { Kind = RequirementKind.Warehouse, Room = RoomType.Warehouse, Need = level, Have = warehouse });
            }

            int key = s.MaxRoomLevel(row.KeyRoom);
            if (key < level)
                unmet.Add(new Requirement { Kind = RequirementKind.KeyRoom, Room = row.KeyRoom, Need = level, Have = key });

            if (level >= b.Rules.HeroGateFrom)
            {
                int ready = 0;
                foreach (var d in st.Dwellers)
                {
                    if (d.IsHero && d.HeroLevel >= row.HeroRequirement)
                        ready++;
                }
                if (ready < b.Rules.HeroCore)
                    unmet.Add(new Requirement { Kind = RequirementKind.Heroes, Need = b.Rules.HeroCore, Have = ready });
            }

            var cost = s.HearthJobCost(level);
            if (!st.Wallet.Covers(cost))
            {
                long need = cost[ResourceType.Silver] + cost[ResourceType.Iron];
                long have = st.Wallet[ResourceType.Silver] + st.Wallet[ResourceType.Iron];
                unmet.Add(new Requirement { Kind = RequirementKind.Materials, Need = need, Have = have });
            }

            if (st.Queue.PickBuilder(b.Build.QueuePerBuilder, now) < 0)
                unmet.Add(new Requirement { Kind = RequirementKind.QueueFull });
            return unmet;
        }
    }
}
