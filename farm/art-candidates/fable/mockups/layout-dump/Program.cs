using System; using System.Collections.Generic; using System.IO; using SunnyValley.Layout; using Newtonsoft.Json;
static class P {
  static object N(UiNode n, bool hidden) {
    bool h = hidden || n.Has(NodeFlags.Hidden);
    var kids = new List<object>(); foreach (var c in n.Children) kids.Add(N(c, h));
    return new { id = n.Id, kind = n.Kind.ToString(), shape = n.Shape.ToString(), x = n.Rect.X, y = n.Rect.Y, w = n.Rect.W, h = n.Rect.H,
      radius = n.Radius, color = n.Color, depth = n.Depth, depthColor = n.DepthColor, icon = n.Icon, value = n.Value, hidden = h,
      cols = n.Cols, rows = n.Rows, cell = n.Cell,
      text = n.Text == null ? null : new { lines = n.Text.Lines, size = n.Text.FittedSize, align = n.Text.Align.ToString(), color = n.Text.Color, outline = n.Text.Outline },
      children = kids };
  }
  static void W(string name, UiLayout L) => File.WriteAllText(name + ".json", JsonConvert.SerializeObject(new { W = L.Fit.Width, H = L.Fit.Height, root = N(L.Root, false) }));
  static void Main(string[] a) {
    var fit = new CanvasFit(new ScreenSpec("ref", 1080, 1920, 400, new Box(0, 0, 1080, 1920)));
    W("level", Screens.Level(fit, new LevelHud { Level = 12, Moves = 23, Cols = 8, Rows = 8,
      Goals = new List<Goal> { new Goal("tomato", 18), new Goal("blueberry", 12), new Goal("crate", 6) }, Boosters = new[] { 3, 2, 0, 1 } }));
    W("farm", Screens.Farm(fit, new FarmHud { Coins = 12450, Stars = 7, Crystals = 85, Lives = 4, NextLifeSeconds = 754, Level = 12, TasksBadge = 2, OrdersBadge = 1, ZoneName = "Двор", ZoneDone = 5, ZoneTotal = 9 }));
    W("farm2", Screens.Farm(fit, new FarmHud { Coins = 12450, Stars = 7, Crystals = 85, Lives = 5, Level = 57, Economy = true, FarmLevel = 6, XpHave = 1340, XpNeed = 2000, FieldsBadge = 3, OrdersBadge = 2, BarnBadge = 1 }));
    W("win", Popups.Win(fit, new WinInfo { Level = 12, Stars = 3, Coins = 150, FarmStars = 1 }));
    W("start", Popups.LevelStart(fit, new LevelStartInfo { Level = 12, Goals = new List<Goal> { new Goal("tomato", 18), new Goal("blueberry", 12), new Goal("crate", 6) }, Boosters = new[] { 2, 1, 0 }, Picked = new[] { true, false, false } }));
    W("settings", Popups.Settings(fit, new SettingsInfo()));
    W("lose", Popups.Lose(fit, new LoseInfo()));
    W("loading", Screens.Loading(fit, 0.62f));
  }
}
