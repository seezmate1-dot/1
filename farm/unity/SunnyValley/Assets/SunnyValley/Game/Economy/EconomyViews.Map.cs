using SunnyValley.Core.Meta;
using SunnyValley.Layout;

namespace SunnyValley.Game.Economy
{
    /// <summary>What the farm map's menus (Farm/FarmMenus) share with the windows: the same questions and ways.</summary>
    public static partial class EconomyViews
    {
        /// <summary>The question before a speed-up for crystals, as the windows ask it.</summary>
        public static ConfirmInfo SpeedUpAsk(MetaGame g, string icon, string what, long left) => SpeedUpQuestion(g, icon, what, left);

        /// <summary>Where a feed comes from: the button's words and what it does (the mill's window, the fields).</summary>
        public static (RowButton button, EconomyAction way) FeedWay(MetaGame g, string feed) => FeedSource(g, feed);

        public static string LockedText(int level) => "Откроется на " + level + " уровне фермы";
    }
}
