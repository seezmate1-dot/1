using System.Collections.Generic;
using SunnyValley.Core.Meta;
using SunnyValley.Game.Economy;
using SunnyValley.Game.Farm;
using SunnyValley.Game.UI;
using SunnyValley.Layout;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SunnyValley.Game.Flow
{
    /// <summary>
    /// The working farm played on the map itself (docs/FARM_UX.md §5–6), instead of in lists. A tap on a site does
    /// the obvious thing at once (takes what is ready, feeds, opens the barn or the orders) or opens the site's small
    /// menu next to it; a tool dragged out of the menu — seeds, the sickle, feed, the basket — works on everything
    /// it is swept across; near the screen's edge the map slides on under it. Every action answers: the site
    /// squashes, a number floats up, what was gathered flies in an arc to the barn's button and the button bumps,
    /// coins tick; what cannot be done shakes and says why once. Rules and saving are MetaGame's, through the same
    /// calls as the windows (FarmMenus, ToolStroke — tested without Unity); crystals are spent only after a question.
    /// </summary>
    public sealed class FarmInteraction
    {
        readonly GameApp app;
        readonly FarmScreen screen;
        readonly FarmWorld world;
        readonly FarmSitesView sites;
        readonly FarmMenuView menu;

        FarmTool menuTool;
        ToolStroke stroke;
        RectTransform ghost;
        Vector2 dragStart, dragScreen, lastMap;
        bool armed;
        float clock;
        static bool dragHintShown;

        /// <summary>The finger carries the tool above itself, so it is seen; it works a little above the finger too.</summary>
        const float GhostLift = 130, ToolLift = 70;

        public FarmInteraction(GameApp app, FarmScreen screen, FarmWorld world)
        {
            this.app = app;
            this.screen = screen;
            this.world = world;
            // the sickle is the farm's own picture; the rest of the icons come from the icon set
            var sickle = FarmArt.Painted("Farm/Sickle");
            if (sickle != null)
                ShapeSprites.Register(FarmMenus.SickleIcon, sickle);
            // a drag out of a menu button starts after a finger's slip, not after a few pixels
            if (EventSystem.current != null)
                EventSystem.current.pixelDragThreshold = Mathf.Max(10, Mathf.RoundToInt(TouchGestures.SlopDp * (Screen.dpi > 1 ? Screen.dpi : 160) / 160f));
            Haptics.Enabled = app.State.vibration;

            sites = FarmSitesView.Create(world, Meta);
            menu = FarmMenuView.Create(world);
            menu.Clicked = OnItemClicked;
            menu.DragBegan = OnDragBegan;
            menu.Dragged = OnDragged;
            menu.DragEnded = OnDragEnded;
            world.SiteTapped += OnSiteTapped;
            world.SitePressed += sites.Press;
            world.SiteReleased += sites.Release;
            world.GroundTouched += CloseMenu;
        }

        MetaGame Meta => app.State.Meta;
        UiRoot Ui => app.Ui;
        static long Now => GameState.Now;

        bool Free => app.Popups.Count == 0 && !screen.Busy && stroke == null;

        // ------------------------------------------------------------------ frames

        public void Tick(float dt)
        {
            if (stroke != null)
                EdgePan(dt);
            clock += dt;
            if (clock < 0.5f)
                return;
            clock = 0;
            Sync();
        }

        /// <summary>The farm changed (an action here or in a window): the sites and the open menu show it.</summary>
        public void Sync()
        {
            if (sites == null)
                return;
            long now = Now;
            var site = menu.Site;
            sites.Sync(now);
            if (site == null)
                return;
            // the map may have been built again: the menu's site is found by its id
            var again = sites.Map.Find(site.Id);
            if (again == null)
            {
                CloseMenu();
                return;
            }
            if (stroke != null)
                return;
            var model = menuTool != null ? FarmMenus.OfTool(menuTool, Meta) : FarmMenus.For(again, Meta, now);
            if (again != site)
            {
                menu.Show(again, model);
                sites.Select(again);
            }
            else
            {
                menu.Refresh(model);
            }
        }

        // ------------------------------------------------------------------ taps

        void OnSiteTapped(FarmSite site)
        {
            if (!Free)
                return;
            long now = Now;
            Haptics.Tap();
            var before = FarmSiteMap.Status(site, Meta, now);
            var primary = FarmMenus.Primary(site, Meta, now);
            if (primary == null)
            {
                // the same site again closes its menu
                if (menu.Open && menu.Site == site && menuTool == null)
                {
                    CloseMenu();
                    return;
                }
                sites.Pop(site);
                OpenMenu(site, FarmMenus.For(site, Meta, now), null);
                return;
            }
            if (primary.Kind == ActionKind.Open)
            {
                CloseMenu();
                sites.Pop(site);
                world.Reveal(new Vector2(site.X, site.Y + site.H / 2));
                OpenWindow(primary.Window, primary.Arg);
                return;
            }
            var r = primary.Run(now);
            if (!r.Ok)
            {
                Refuse(site, r.Error);
                // what can be done about it is in the menu (a full barn: the sickle waits; no feed: the way to the mill)
                OpenMenu(site, FarmMenus.For(site, Meta, now), null);
                return;
            }
            Done(site, r.Reward, before);
            app.State.Save();
            Announce();
            var tool = FarmMenus.FollowUp(site, before, sites.Map, Meta, now);
            if (tool != null)
                OpenMenu(site, FarmMenus.OfTool(tool, Meta), tool);
            else
                CloseMenu();
        }

        void OpenMenu(FarmSite site, SiteMenu model, FarmTool tool)
        {
            menuTool = tool;
            sites.Select(site);
            world.Reveal(new Vector2(site.X, site.Y + site.H / 2));
            menu.Show(site, model);
        }

        public bool MenuOpen => menu != null && menu.Open;

        public void CloseMenu()
        {
            if (stroke != null)
                return;
            menuTool = null;
            sites?.Select(null);
            menu?.Hide();
        }

        /// <summary>A side button of the HUD: the camera flies to that part of the farm, to what needs a hand first.</summary>
        public void GoTo(string what)
        {
            if (!Free || sites.Map == null)
                return;
            CloseMenu();
            long now = Now;
            FarmSite best = null, first = null;
            foreach (var s in sites.Map.Sites)
            {
                bool mine = what == EconomyViews.Fields ? s.Kind == SiteKind.Plot
                    : what == EconomyViews.Animals ? s.Kind == SiteKind.Animal || s.Kind == SiteKind.Pen
                    : s.Kind == SiteKind.Workshop;
                if (!mine)
                    continue;
                first ??= s;
                var st = FarmSiteMap.Status(s, Meta, now);
                if (best == null && (st.Attention || st.Phase == SitePhase.Empty))
                    best = s;
            }
            var to = best ?? first;
            if (to == null)
                return;
            float zoom = what == EconomyViews.Fields ? FarmSiteMap.FieldZoom : 1.05f;
            if (what == EconomyViews.Fields)
            {
                // the whole field in view: its middle column, the row of the plot that needs a hand
                var mid = FarmSiteMap.PlotPoint(FarmArt.Layout, FarmSiteMap.PlotColumns / 2);
                var seen = to;
                world.Focus(new Vector2(mid.X, Mathf.Min(to.Y + 200, mid.Y) - 150), 0.55f, zoom, () => sites.Pop(seen));
                return;
            }
            world.Focus(new Vector2(to.X, to.Y + to.H / 2), 0.55f, zoom, () => sites.Pop(to));
        }

        // ------------------------------------------------------------------ the menu's buttons

        void OnItemClicked(int index)
        {
            if (!Free || menu.Menu == null || index >= menu.Menu.Items.Count)
                return;
            var item = menu.Menu.Items[index];
            var site = menu.Site;
            Haptics.Tap();
            if (!item.Enabled)
            {
                FarmFx.Shake(menu.ItemRect(index));
                Ui.Toast(item.Reason ?? "Пока нельзя");
                return;
            }
            if (item.Tool != null)
            {
                // a tap uses the tool right here; the first time, say that it can be dragged
                var one = new ToolStroke(Meta, sites.Map, item.Tool);
                var hit = one.Touch(site, Now);
                if (hit != null)
                    Hit(hit);
                Finish(one);
                if (!dragHintShown && !one.Stopped)
                {
                    dragHintShown = true;
                    Ui.Toast("Ведите пальцем от кнопки по ферме — так быстрее");
                }
                AfterStroke(one, site);
                return;
            }
            Perform(item.Action, site);
        }

        void Perform(EconomyAction a, FarmSite site)
        {
            if (a == null)
                return;
            switch (a.Kind)
            {
                case ActionKind.Toast:
                    Ui.Toast(a.Text);
                    break;
                case ActionKind.Open:
                    CloseMenu();
                    OpenWindow(a.Window, a.Arg);
                    break;
                case ActionKind.Run:
                    // crystals are spent only after a question
                    if (a.Confirm != null)
                        new ConfirmPopup(app, a.Confirm, () => Execute(a, site)).Open();
                    else
                        Execute(a, site);
                    break;
            }
        }

        void Execute(EconomyAction a, FarmSite site)
        {
            long coins = Meta.Coins;
            var r = a.Run(Now);
            if (!r.Ok)
            {
                Refuse(site, r.Error);
                return;
            }
            app.State.Save();
            sites.Sync(Now);
            var now = sites.Map.Find(site.Id) ?? site;
            sites.Pop(now);
            FarmFx.Sparks(Ui.OverlayLayer, sites.WorldPoint(now), 6, 110);
            if (Meta.Coins < coins)
                Spent(now, coins - Meta.Coins);
            if (!r.Reward.IsEmpty)
                Gather(now, r.Reward);
            if (a.Text != null)
                Ui.Toast(a.Text);
            Announce();
            if (a.Close)
                CloseMenu();
            else
                Sync();
        }

        /// <summary>A window for what does not fit on the map; the fields are on the map, so «Поля» is a flight there.</summary>
        void OpenWindow(string window, string arg)
        {
            if (window == EconomyViews.Fields)
            {
                GoTo(EconomyViews.Fields);
                return;
            }
            if (window == EconomyViews.Workshop && arg != null)
            {
                var ws = sites.Map.Find("workshop." + arg);
                if (ws != null)
                    world.Focus(new Vector2(ws.X, ws.Y + ws.H / 2), 0.45f);
            }
            new EconomyPopup(app, window, arg).Open();
        }

        // ------------------------------------------------------------------ a dragged tool

        Vector2 ToolMap(Vector2 screenPoint) => world.ScreenToMap(screenPoint + new Vector2(0, ToolLift * Ui.Canvas.scaleFactor));

        void OnDragBegan(int index, PointerEventData e)
        {
            if (!Free || menu.Menu == null || index >= menu.Menu.Items.Count)
                return;
            var item = menu.Menu.Items[index];
            if (item.Tool == null)
                return;
            if (!item.Enabled)
            {
                FarmFx.Shake(menu.ItemRect(index));
                Ui.Toast(item.Reason ?? "Пока нельзя");
                return;
            }
            stroke = new ToolStroke(Meta, sites.Map, item.Tool);
            dragHintShown = true;
            armed = false;
            dragStart = e.pressPosition;
            dragScreen = e.position;
            lastMap = ToolMap(e.position);
            menu.Dim(true);

            var go = new GameObject("tool", typeof(RectTransform));
            go.layer = world.gameObject.layer;
            ghost = (RectTransform)go.transform;
            ghost.SetParent(menu.transform.parent, false);
            ghost.anchorMin = ghost.anchorMax = ghost.pivot = new Vector2(0.5f, 0.5f);
            ghost.sizeDelta = new Vector2(150, 150);
            ghost.SetAsLastSibling();
            FarmFx.Picture(ghost, ShapeSprites.Icon(item.Tool.Icon));
            Tweener.Scale(ghost, 0.6f, 1.15f, 0.18f, Easing.Kind.OutBack);
            MoveGhost();
            Haptics.Tap();
        }

        void MoveGhost()
        {
            if (ghost == null)
                return;
            Vector2 v = world.ScreenToView(dragScreen);
            ghost.anchoredPosition = v + new Vector2(0, GhostLift);
            // the tool leans the way it goes
            ghost.localRotation = Quaternion.Euler(0, 0, Mathf.Clamp((dragStart.x - dragScreen.x) * 0.02f, -14, 14));
        }

        void OnDragged(int index, PointerEventData e)
        {
            if (stroke == null)
                return;
            dragScreen = e.position;
            MoveGhost();
            Sweep();
        }

        /// <summary>The tool works on everything between where it was and where it is.</summary>
        void Sweep()
        {
            Vector2 map = ToolMap(dragScreen);
            if (!armed)
            {
                // not before the finger has left the button it took the tool from: no planting under the menu by a slip
                float away = (world.ScreenToView(dragScreen) - world.ScreenToView(dragStart)).magnitude;
                if (away < RadialLayout.Item * 0.6f)
                {
                    lastMap = map;
                    return;
                }
                armed = true;
                lastMap = map;
            }
            bool was = stroke.Stopped;
            var hits = stroke.Sweep(new P2(lastMap.x, lastMap.y), new P2(map.x, map.y), Now, world.Zoom);
            lastMap = map;
            for (int i = 0; i < hits.Count; i++)
                Hit(hits[i], i * 0.04f);
            if (hits.Count > 0)
            {
                sites.Sync(Now);
                screen.Refresh(false);
            }
            if (stroke.Stopped && !was)
            {
                Ui.Toast(StopText(stroke));
                FarmFx.Shake(ghost, 22);
                if (stroke.StopError == MetaError.BarnFull)
                    screen.Nudge("farm.barn", true);
            }
        }

        /// <summary>Held near the screen's edge the tool pulls the map along, and goes on working.</summary>
        void EdgePan(float dt)
        {
            if (!armed || ghost == null)
                return;
            Vector2 v = world.ScreenToView(dragScreen);
            P2 speed = world.Cam.EdgePan(new P2(v.x, v.y));
            if (speed.Length < 1)
                return;
            world.Slide(new Vector2(speed.X, speed.Y) * Mathf.Min(0.05f, dt));
            Sweep();
        }

        void OnDragEnded(int index, PointerEventData e)
        {
            if (stroke == null)
                return;
            var done = stroke;
            var site = menu.Site;
            stroke = null;
            if (ghost != null)
            {
                var go = ghost.gameObject;
                Tweener.Scale(ghost, ghost.localScale.x, 0, 0.14f, Easing.Kind.InQuad, () => Object.Destroy(go));
                ghost = null;
            }
            menu.Dim(false);
            Finish(done);
            AfterStroke(done, site);
        }

        /// <summary>Saves what a tool did and tells the HUD.</summary>
        void Finish(ToolStroke s)
        {
            if (s.Done > 0)
            {
                app.State.Save();
                Announce();
            }
            sites.Sync(Now);
        }

        /// <summary>After a tool: the menu stays while the tool still has work in sight, else it goes.</summary>
        void AfterStroke(ToolStroke s, FarmSite site)
        {
            if (site == null)
            {
                CloseMenu();
                return;
            }
            long now = Now;
            bool more = false;
            if (!s.Stopped)
            {
                foreach (var other in sites.Map.Sites)
                {
                    if (ToolStroke.Fits(s.Tool, other, Meta, now))
                    {
                        more = true;
                        break;
                    }
                }
            }
            var again = sites.Map.Find(site.Id);
            if (s.Done > 0 && !more || again == null)
            {
                CloseMenu();
                return;
            }
            // the tool did the menu's own site: it turns into the one tool that goes on (the seeds picked, the sickle)
            if (s.Done > 0 && !ToolStroke.Fits(s.Tool, again, Meta, now))
            {
                menuTool = s.Tool;
                var model = FarmMenus.OfTool(s.Tool, Meta);
                if (s.Tool.Kind == ToolKind.Seed)
                {
                    model.Hint = "Проведите семенами по грядкам";
                    var crop = Meta.Data.Crops[s.Tool.Arg];
                    model.Items[0].Label = Format.Amount(crop.SeedCoins);
                    model.Items[0].LabelIcon = Icons.Coin;
                }
                menu.Show(again, model);
                return;
            }
            Sync();
        }

        static string StopText(ToolStroke s)
        {
            switch (s.StopError)
            {
                case MetaError.NotEnoughCoins: return "Монеты кончились — семена стоят денег. Продайте что-нибудь из амбара";
                case MetaError.BarnFull: return "Амбар полон — продайте товары или расширьте его";
                case MetaError.NeedsFeed:
                case MetaError.NotEnoughItems: return "Корм кончился — его делает мельница";
                default: return Texts.Error(s.StopError);
            }
        }

        // ------------------------------------------------------------------ answers

        /// <summary>What a tool did to a site: planted, cut, fed, picked up — or could not.</summary>
        void Hit(StrokeHit h, float delay = 0)
        {
            if (!h.Ok)
            {
                sites.Shake(h.Site);
                return;
            }
            Haptics.Tap();
            sites.Pop(h.Site);
            var at = sites.WorldPoint(h.Site);
            if (h.Coins > 0)
                Spent(h.Site, h.Coins);
            else if (stroke == null || stroke.Tool.Kind == ToolKind.Seed || stroke.Tool.Kind == ToolKind.Feed)
            {
                if (h.Item != null && h.Reward.IsEmpty)
                    FarmFx.FloatText(Ui.OverlayLayer, "−1", at, new Color(1, 0.9f, 0.75f, 1), ShapeSprites.Icon(Icons.Item(h.Item)), delay);
            }
            if (!h.Reward.IsEmpty)
                Gather(h.Site, h.Reward, delay);
        }

        /// <summary>A tap that took or fed: the same answers as a tool's.</summary>
        void Done(FarmSite site, Reward reward, SiteStatus before)
        {
            sites.Pop(site);
            sites.Sync(Now);
            if (!reward.IsEmpty)
                Gather(site, reward);
            else if (before.Phase == SitePhase.Hungry && before.Item != null)
                FarmFx.FloatText(Ui.OverlayLayer, "−1", sites.WorldPoint(site), new Color(1, 0.9f, 0.75f, 1), ShapeSprites.Icon(Icons.Item(before.Item)));
            screen.Refresh(false);
        }

        void Spent(FarmSite site, long coins)
        {
            FarmFx.FloatText(Ui.OverlayLayer, "−" + Format.Amount(coins), sites.WorldPoint(site), new Color(1, 0.86f, 0.4f, 1), ShapeSprites.Icon(Icons.Coin));
            screen.Count("farm.coins");
        }

        /// <summary>
        /// What was gathered leaves the site: «+2» floats up, the goods fly in an arc to the barn's button (coins and
        /// crystals to their plates), and the counter bumps as each lands.
        /// </summary>
        void Gather(FarmSite site, Reward r, float delay = 0)
        {
            var from = sites.WorldPoint(site);
            var layer = Ui.OverlayLayer;
            int line = 0;
            var all = new List<KeyValuePair<string, int>>();
            foreach (var kv in r.Items)
                all.Add(new KeyValuePair<string, int>(EconomyViews.IconOf(kv.Key), kv.Value));
            foreach (var kv in r.Tools)
                all.Add(new KeyValuePair<string, int>(Icons.Tool(kv.Key), kv.Value));
            foreach (var kv in r.Boosters)
                all.Add(new KeyValuePair<string, int>(EconomyViews.BoosterIcon(kv.Key), kv.Value));
            var barn = screen.RectOf("farm.barn.disc") ?? screen.RectOf("farm.barn");
            foreach (var kv in all)
            {
                var sprite = ShapeSprites.Icon(kv.Key);
                FarmFx.FloatText(layer, "+" + kv.Value, from + new Vector3(0, line * 70 * layer.lossyScale.y, 0), Color.white, sprite, delay + line * 0.12f);
                line++;
                if (barn == null)
                    continue;
                int n = Mathf.Clamp(kv.Value, 1, 3);
                for (int i = 0; i < n; i++)
                {
                    bool last = i == n - 1;
                    FarmFx.Fly(layer, sprite, from, barn.position, 96, delay + i * 0.07f, () =>
                    {
                        if (last)
                            screen.Nudge("farm.barn", false);
                    }, (i - (n - 1) / 2f) * 90);
                }
            }
            if (r.Coins > 0)
                FlyTo("farm.coins", Icons.Coin, from, "+" + Format.Amount(r.Coins), delay);
            if (r.Crystals > 0)
                FlyTo("farm.crystals", Icons.Crystal, from, "+" + r.Crystals, delay);
            if (r.Xp > 0)
                FlyTo("farm.xp", Icons.Xp, from, null, delay);
            if (all.Count > 0)
                FarmFx.Sparks(layer, from, 5, 90);
        }

        void FlyTo(string plate, string icon, Vector3 from, string text, float delay)
        {
            var to = screen.RectOf(plate + ".icon") ?? screen.RectOf(plate);
            var sprite = ShapeSprites.Icon(icon);
            if (text != null)
                FarmFx.FloatText(Ui.OverlayLayer, text, from, Color.white, sprite, delay);
            if (to != null)
                FarmFx.Fly(Ui.OverlayLayer, sprite, from, to.position, 90, delay, () => screen.Count(plate));
        }

        /// <summary>«No», with the reason once: the site shakes, a full barn's button shakes too.</summary>
        void Refuse(FarmSite site, MetaError error)
        {
            sites.Shake(site);
            Ui.Toast(Texts.Error(error));
            if (error == MetaError.BarnFull)
                screen.Nudge("farm.barn", true);
            else if (error == MetaError.NotEnoughCoins)
                screen.Nudge("farm.coins", true);
            else if (error == MetaError.NotEnoughCrystals)
                screen.Nudge("farm.crystals", true);
        }

        void Announce()
        {
            app.AnnounceEvents();
            screen.Refresh(false);
        }
    }
}
