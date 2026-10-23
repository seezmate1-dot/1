using SunnyValley.Core.Match3;

namespace SunnyValley.Game.Match3View
{
    /// <summary>Icon names (ShapeSprites.Icon) for level goals: the crop for «collect», the obstacle otherwise.</summary>
    public static class GoalIcons
    {
        public static string For(GoalType type, TileColor color)
        {
            switch (type)
            {
                case GoalType.Collect: return color == TileColor.None ? "tomato" : TileColors.Name(color);
                case GoalType.Grass: return "grass";
                case GoalType.Crate: return "crate";
                case GoalType.Ice: return "ice";
                case GoalType.Stone: return "stone";
                case GoalType.Honey: return "honey";
                case GoalType.Weed: return "weed";
                case GoalType.Chain: return "chain";
                case GoalType.Chick: return "chick";
                case GoalType.Crop: return "bed";
                case GoalType.Basket: return "basket";
                default: return "star";
            }
        }

        public static string For(LevelGoal goal) => For(goal.Type, goal.Color);

        /// <summary>The icon a goal ghost shows when it flies from the board (the crop of a harvested bed).</summary>
        public static string Ghost(GoalType type, TileColor color, string bedCrop) =>
            type == GoalType.Crop ? (string.IsNullOrEmpty(bedCrop) ? "carrot" : bedCrop) : For(type, color);
    }
}
