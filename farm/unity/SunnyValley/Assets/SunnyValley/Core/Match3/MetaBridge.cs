using SunnyValley.Core.Meta;

namespace SunnyValley.Core.Match3
{
    /// <summary>
    /// The one place where the board talks to the meta: a finished attempt becomes the meta's
    /// <see cref="LevelResult"/> (SunnyValley.Core.Meta), which <c>MetaGame.FinishLevel</c> turns into rewards.
    /// </summary>
    public static class Match3MetaBridge
    {
        /// <summary>Coins the board pays per move left on a win (the meta adds its own win coins on top).</summary>
        public const int CoinsPerMoveLeft = 10;

        public static LevelResult ToLevelResult(Match3Game game)
        {
            bool won = game.Status == GameStatus.Won;
            var r = new LevelResult
            {
                Level = game.Level.Id,
                Won = won,
                MovesLeft = game.MovesLeft,
                Stars = won ? game.Stars : 0,
                CoinsEarned = won ? game.MovesLeft * CoinsPerMoveLeft : 0,
            };
            if (game.CropsHarvested > 0)
                r.Crops[string.IsNullOrEmpty(game.Level.BedCrop) ? LevelData.DefaultBedCrop : game.Level.BedCrop] = game.CropsHarvested;
            return r;
        }
    }
}
