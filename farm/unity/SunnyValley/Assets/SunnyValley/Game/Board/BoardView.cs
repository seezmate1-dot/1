using System;
using System.Collections.Generic;
using SunnyValley.Core.Match3;
using SunnyValley.Game.UI;
using SunnyValley.Layout;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SunnyValley.Game.Match3View
{
    /// <summary>
    /// The match-3 board inside the rectangle the layout gave it (whole-unit cells, so the grid is crisp).
    /// Draws a <see cref="Match3Game"/>: the shaped board (checker of soil-green tiles, holes, walls, portals,
    /// spawners), grass, blockers and overlays with their layers, the pieces; takes swipes and tap-tap swaps,
    /// booster targets; and plays every action's step log as a <see cref="Timeline"/> (BoardView.Cues.cs).
    ///
    /// Performance: two nested canvases — the cells (rebuilt only when an obstacle changes) and the moving part
    /// (pieces, overlays, effects) — so the HUD never re-batches because a piece moved; pieces and effect sprites
    /// are pooled; animations are plain fields advanced in one Update, nothing is allocated per frame.
    /// </summary>
    public sealed partial class BoardView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        static readonly Color FrameColor = new Color32(0x3B, 0x6B, 0x26, 0xFF);
        static readonly Color TileLight = new Color32(0xDC, 0xF0, 0xAE, 0xFF);
        static readonly Color TileDark = new Color32(0xC6, 0xE3, 0x92, 0xFF);

        int cols, rows, nodeCols, nodeRows;
        float cell;
        RectTransform content, cellsLayer, tilesRt, underRt, piecesRt, overRt, fxRt;
        Match3Game game;
        CellVisual[] cells = Array.Empty<CellVisual>();
        readonly Dictionary<int, PieceVisual> byId = new Dictionary<int, PieceVisual>();
        readonly List<PieceVisual> live = new List<PieceVisual>();
        readonly Stack<PieceVisual> freePieces = new Stack<PieceVisual>();
        readonly List<GameObject> statics = new List<GameObject>();
        FxPool fx, fly;
        RectTransform flyLayer;
        float now;
        Sprite cropSprite;

        // input
        Image selection, highlight;
        Pos selected = Pos.None, downCell = Pos.None, targetCell = Pos.None;
        Vector2 downLocal;
        bool pointerDown, swiped;

        // hint
        float idle;
        bool hintShown;
        PieceVisual hintA, hintB;

        // shake
        float shakeT0 = -1, shakeDur, shakeAmp;

        /// <summary>The player may swap / aim (the screen turns it off while a move plays).</summary>
        public bool InputEnabled = true;

        /// <summary>Booster aiming: -1 off, 0 a cell (Лопата), 1 a row (Грабли), 2 a column (Лейка).</summary>
        public int TargetMode = -1;

        /// <summary>The player swiped a piece onto a neighbour, or tapped two neighbours one after the other.</summary>
        public event Action<Pos, Pos> SwapGesture;

        /// <summary>The player chose where a booster goes (released the finger over a cell).</summary>
        public event Action<Pos> TargetChosen;

        /// <summary>The moves counter should show this now (the step log's MovesChanged, the bonus moves).</summary>
        public event Action<int> MovesShown;

        /// <summary>A goal ghost has reached its icon (or a goal changed without a ghost): the counter may change.</summary>
        public event Action<GoalProgressEvent> GoalReached;

        /// <summary>Where goal i's icon is (world position), for the ghosts.</summary>
        public Func<int, Vector3> GoalTarget;

        /// <summary>Where the moves counter is (world position), for the bonus sparks.</summary>
        public Func<Vector3> MovesTarget;

        /// <summary>A move to suggest after a while without input (Hints.Find).</summary>
        public Func<SwapMove?> HintProvider;

        /// <summary>Seconds without input before the hint wiggles.</summary>
        public float HintDelay = 5f;

        /// <summary>A game has been shown.</summary>
        public bool HasGame => game != null;

        public int Cols => cols;
        public int Rows => rows;
        public float CellSize => cell;

        /// <summary>No timeline is playing.</summary>
        public bool Idle => !playing;

        /// <summary>Idle, and no goal ghost or spark is still flying.</summary>
        public bool Settled => !playing && (fly == null || fly.LiveCount == 0);

        // ------------------------------------------------------------------ building

        /// <summary>Called by UiView for the layout's board node: sizes and the empty layers.</summary>
        public void Build(UiNode node)
        {
            cols = nodeCols = Math.Max(1, node.Cols);
            rows = nodeRows = Math.Max(1, node.Rows);
            cell = node.Cell;
            var self = (RectTransform)transform;
            // the layout's translucent board face: the board draws its own shaped frame
            var face = self.Find("face");
            if (face != null)
                face.gameObject.SetActive(false);

            content = Layer("content", self);
            cellsLayer = Layer("cells", content);
            cellsLayer.gameObject.AddComponent<Canvas>();
            tilesRt = Layer("tiles", cellsLayer);
            underRt = Layer("under", cellsLayer);

            var dyn = Layer("moving", content);
            dyn.gameObject.AddComponent<Canvas>();
            // pieces are clipped at the top edge only (they drop in from above the board)
            var mask = Layer("mask", dyn);
            mask.offsetMin = new Vector2(-cell, -cell);
            mask.offsetMax = new Vector2(cell, 0);
            mask.gameObject.AddComponent<RectMask2D>();
            piecesRt = Layer("pieces", mask);
            piecesRt.offsetMin = new Vector2(cell, cell);
            piecesRt.offsetMax = new Vector2(-cell, 0);
            overRt = Layer("over", dyn);
            fxRt = Layer("fx", dyn);
            fx = new FxPool(fxRt);

            selection = MakeImage("selection", overRt, BoardSprites.Ring, cell * 1.12f);
            selection.color = new Color(1, 1, 0.85f, 0.95f);
            selection.enabled = false;
            highlight = MakeImage("highlight", overRt, ShapeSprites.RoundRect, cell);
            highlight.type = Image.Type.Sliced;
            highlight.pixelsPerUnitMultiplier = ShapeSprites.SliceRadius / Mathf.Max(4, cell * 0.2f);
            highlight.color = new Color(1, 1, 0.8f, 0.4f);
            highlight.enabled = false;
        }

        static RectTransform Layer(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        Image MakeImage(string name, RectTransform parent, Sprite sprite, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>The goal ghosts and bonus sparks fly over the HUD: they live on this layer (the screen's root).</summary>
        public void SetFlyLayer(RectTransform layer)
        {
            flyLayer = layer;
            var go = new GameObject("board fly", typeof(RectTransform));
            go.layer = layer.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(layer, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.SetAsLastSibling();
            fly = new FxPool(rt);
            fly.Ended = OnFlyEnded;
            flyRoot = rt;
        }

        RectTransform flyRoot;

        /// <summary>The centre of a cell in the board's own coordinates (lower left corner = 0, 0).</summary>
        public Vector2 At(Pos p) => new Vector2((p.X + 0.5f) * cell, (rows - p.Y - 0.5f) * cell);

        /// <summary>World position of a cell's centre.</summary>
        public Vector3 WorldAt(Pos p) => piecesRt.TransformPoint(At(p) - piecesRt.rect.size * piecesRt.pivot);

        Vector2 ToFly(Vector3 world)
        {
            Vector3 local = flyRoot.InverseTransformPoint(world);
            return new Vector2(local.x, local.y);
        }

        // ------------------------------------------------------------------ showing a game

        /// <summary>
        /// Draws the game as it is now, without animation. Pieces that the pre-level boosters turned into
        /// specials (<paramref name="initial"/>, played afterwards) are drawn as the plain tiles they were.
        /// </summary>
        public void Show(Match3Game g, IReadOnlyList<StepEvent> initial = null)
        {
            game = g;
            StopPlaying();
            ClearPieces();
            fx.Clear(false);
            fly?.Clear(true);
            foreach (var go in statics)
            {
                if (Application.isPlaying)
                    Destroy(go);
                else
                    DestroyImmediate(go);
            }
            statics.Clear();
            cells = new CellVisual[g.Topo.Count];
            selected = Pos.None;
            selection.enabled = false;
            highlight.enabled = false;
            shakeT0 = -1;
            // the board's own size, centred in the layout's rectangle (they agree when the layout was made for it)
            cols = g.Topo.Width;
            rows = g.Topo.Height;
            var inset = new Vector2((nodeCols - cols) * cell / 2, (nodeRows - rows) * cell / 2);
            content.offsetMin = inset;
            content.offsetMax = -inset;
            idle = 0;
            hintShown = false;

            string crop = string.IsNullOrEmpty(g.Level.BedCrop) ? LevelData.DefaultBedCrop : g.Level.BedCrop;
            cropSprite = ShapeSprites.Icon(crop);

            var topo = g.Topo;
            // the frame first (each cell's slightly larger dark square: together they outline the board's shape)
            float pad = Mathf.Max(3, cell * 0.07f);
            for (int i = 0; i < topo.Count; i++)
            {
                if (!topo.Exists[i])
                    continue;
                var p = topo.PosOf(i);
                var frame = MakeImage("frame", tilesRt, ShapeSprites.RoundRect, cell + 2 * pad);
                frame.type = Image.Type.Sliced;
                frame.pixelsPerUnitMultiplier = ShapeSprites.SliceRadius / Mathf.Max(4, cell * 0.22f);
                frame.color = FrameColor;
                frame.rectTransform.anchoredPosition = At(p);
                statics.Add(frame.gameObject);
            }
            for (int i = 0; i < topo.Count; i++)
            {
                if (!topo.Exists[i])
                    continue;
                var p = topo.PosOf(i);
                var tile = MakeImage("tile", tilesRt, ShapeSprites.RoundRect, cell - 2);
                tile.type = Image.Type.Sliced;
                tile.pixelsPerUnitMultiplier = ShapeSprites.SliceRadius / Mathf.Max(4, cell * 0.14f);
                tile.color = (p.X + p.Y) % 2 == 0 ? TileLight : TileDark;
                tile.rectTransform.anchoredPosition = At(p);
                statics.Add(tile.gameObject);
                var c = new CellVisual(p, At(p), cell, underRt, overRt, statics) { Crop = cropSprite };
                c.Read(g.Board, i);
                cells[i] = c;
            }
            Marks(g);

            var plainBefore = new Dictionary<int, TileColor>();
            if (initial != null)
            {
                foreach (var e in initial)
                    if (e is TransformEvent t)
                        plainBefore[t.PieceId] = t.Color == TileColor.None ? (TileColor)(t.PieceId % Math.Max(1, g.Colors)) : t.Color;
            }
            for (int i = 0; i < topo.Count; i++)
            {
                var piece = g.Board.Pieces[i];
                if (!topo.Exists[i] || piece.IsEmpty)
                    continue;
                if (plainBefore.TryGetValue(piece.Id, out var color))
                    NewPiece(piece.Id, piece.Kind, color, SpecialKind.None, topo.PosOf(i));
                else
                    NewPiece(piece.Id, piece.Kind, piece.Color, piece.Special, topo.PosOf(i));
            }
            selection.rectTransform.SetAsLastSibling();
            highlight.rectTransform.SetAsLastSibling();
        }

        /// <summary>Walls, portals, custom spawners and chick exits.</summary>
        void Marks(Match3Game g)
        {
            var topo = g.Topo;
            var wood = new Color32(0x8A, 0x55, 0x2A, 0xFF);
            bool customSpawners = g.Level.Spawners != null && g.Level.Spawners.Count > 0;
            bool chicks = g.Level.Chicks != null && g.Level.Chicks.Spawn > 0 || HasChicks(g);
            for (int i = 0; i < topo.Count; i++)
            {
                if (!topo.Exists[i])
                    continue;
                var p = topo.PosOf(i);
                var at = At(p);
                if (topo.WallRight[i])
                    Plank(at + new Vector2(cell / 2, 0), new Vector2(cell * 0.14f, cell * 1.08f), wood);
                if (topo.WallDown[i])
                    Plank(at + new Vector2(0, -cell / 2), new Vector2(cell * 1.08f, cell * 0.14f), wood);
                if (topo.PortalEntrance[i])
                    Portal(at + new Vector2(0, -cell * 0.46f));
                if (topo.PortalExit[i])
                    Portal(at + new Vector2(0, cell * 0.46f));
                if (topo.Spawner[i] && customSpawners)
                    Chevron(at + new Vector2(0, cell * 0.58f), new Color(0.45f, 0.85f, 0.35f, 0.95f));
                if (topo.ChickExit[i] && chicks)
                    Chevron(at + new Vector2(0, -cell * 0.6f), new Color(1f, 0.85f, 0.25f, 0.95f));
            }
        }

        static bool HasChicks(Match3Game g)
        {
            foreach (var goal in g.Goals)
                if (goal.Type == GoalType.Chick)
                    return true;
            return false;
        }

        void Plank(Vector2 at, Vector2 size, Color color)
        {
            var img = MakeImage("wall", overRt, ShapeSprites.RoundRect, 1);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ShapeSprites.SliceRadius / Mathf.Max(2, Mathf.Min(size.x, size.y) * 0.45f);
            img.color = color;
            img.rectTransform.sizeDelta = size;
            img.rectTransform.anchoredPosition = at;
            statics.Add(img.gameObject);
        }

        void Portal(Vector2 at)
        {
            var img = MakeImage("portal", overRt, BoardSprites.Ring, cell * 0.8f);
            img.color = new Color(0.7f, 0.45f, 1f, 0.95f);
            img.rectTransform.sizeDelta = new Vector2(cell * 0.86f, cell * 0.3f);
            img.rectTransform.anchoredPosition = at;
            statics.Add(img.gameObject);
            var glow = MakeImage("portal glow", overRt, BoardSprites.Glow, cell * 0.8f);
            glow.color = new Color(0.75f, 0.55f, 1f, 0.55f);
            glow.rectTransform.sizeDelta = new Vector2(cell * 0.8f, cell * 0.26f);
            glow.rectTransform.anchoredPosition = at;
            statics.Add(glow.gameObject);
        }

        void Chevron(Vector2 at, Color color)
        {
            var img = MakeImage("mark", overRt, BoardSprites.Chevron, cell * 0.36f);
            img.color = color;
            img.rectTransform.anchoredPosition = at;
            statics.Add(img.gameObject);
        }

        // ------------------------------------------------------------------ pieces

        PieceVisual NewPiece(int id, PieceKind kind, TileColor color, SpecialKind special, Pos at)
        {
            PieceVisual p;
            if (freePieces.Count > 0)
            {
                p = freePieces.Pop();
            }
            else
            {
                var go = new GameObject("piece", typeof(RectTransform));
                go.layer = piecesRt.gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(piecesRt, false);
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(cell, cell);
                var back = MakeImage("glow", rt, BoardSprites.Glow, cell);
                back.rectTransform.anchorMin = back.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                back.rectTransform.anchoredPosition = Vector2.zero;
                var icon = MakeImage("icon", rt, null, cell * 0.86f);
                icon.preserveAspect = true;
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                icon.rectTransform.anchoredPosition = Vector2.zero;
                p = new PieceVisual { Rt = rt, Back = back, Icon = icon, IconRt = icon.rectTransform };
            }
            p.Setup(id, kind, color, special, at, At(at), cell);
            byId[id] = p;
            live.Add(p);
            return p;
        }

        PieceVisual Piece(int id) => id > 0 && byId.TryGetValue(id, out var p) ? p : null;

        PieceVisual PieceAt(Pos cellPos)
        {
            foreach (var p in live)
                if (p.Live && p.ReleaseAt < 0 && p.Cell == cellPos)
                    return p;
            return null;
        }

        /// <summary>The piece leaves the board's bookkeeping now and goes back to the pool at <paramref name="at"/>.</summary>
        void Retire(PieceVisual p, float at)
        {
            byId.Remove(p.Id);
            p.ReleaseAt = at;
            if (p == hintA || p == hintB)
                StopHint();
        }

        void ClearPieces()
        {
            foreach (var p in live)
            {
                p.Live = false;
                p.Rt.gameObject.SetActive(false);
                freePieces.Push(p);
            }
            live.Clear();
            byId.Clear();
            hintA = hintB = null;
        }

        /// <summary>Makes the drawing match the game exactly (after a timeline; a safety net, normally a no-op).</summary>
        bool Resync()
        {
            if (game == null)
                return true;
            var topo = game.Topo;
            bool ok = true;
            int count = 0;
            for (int i = 0; i < topo.Count; i++)
            {
                if (!topo.Exists[i])
                    continue;
                var c = cells[i];
                if (c != null && !c.Matches(game.Board, i))
                {
                    c.Read(game.Board, i);
                    ok = false;
                }
                var piece = game.Board.Pieces[i];
                if (piece.IsEmpty)
                    continue;
                count++;
                var v = Piece(piece.Id);
                var p = topo.PosOf(i);
                if (v == null || v.Cell != p || v.Special != piece.Special || v.Kind != piece.Kind ||
                    (piece.Special != SpecialKind.Rainbow && v.Color != piece.Color))
                {
                    ok = false;
                    break;
                }
            }
            if (ok && count != byId.Count)
                ok = false;
            if (!ok)
            {
                Debug.LogWarning("Поле: рисунок разошёлся с игрой, перерисовываем");
                ClearPieces();
                for (int i = 0; i < topo.Count; i++)
                {
                    var piece = game.Board.Pieces[i];
                    if (topo.Exists[i] && !piece.IsEmpty)
                        NewPiece(piece.Id, piece.Kind, piece.Color, piece.Special, topo.PosOf(i));
                }
            }
            return ok;
        }

        // ------------------------------------------------------------------ the frame

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            Advance(dt);
        }

        /// <summary>Moves the board clock on by dt and plays what is due (Update calls it; captures call it by hand).</summary>
        public void Advance(float dt)
        {
            now += dt;
            if (playing)
                RunCues();

            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                if (!p.Active)
                    continue;
                if (!p.Tick(now))
                {
                    live.RemoveAt(i);
                    p.Live = false;
                    p.ReleaseAt = -1;
                    p.Rt.gameObject.SetActive(false);
                    freePieces.Push(p);
                }
            }
            for (int i = 0; i < cells.Length; i++)
            {
                var c = cells[i];
                if (c != null && c.Animating)
                    c.Tick(now);
            }
            fx?.Tick(now);
            fly?.Tick(now);

            if (shakeT0 >= 0)
            {
                float k = (now - shakeT0) / shakeDur;
                if (k >= 1)
                {
                    shakeT0 = -1;
                    content.anchoredPosition = Vector2.zero; // the insets are symmetric: centred is zero
                }
                else if (k >= 0)
                {
                    float a = shakeAmp * (1 - k) * (1 - k);
                    float t = now - shakeT0;
                    content.anchoredPosition = new Vector2(Mathf.Sin(t * 71f) * a, Mathf.Cos(t * 53f) * a * 0.8f);
                }
            }

            if (selected != Pos.None)
            {
                float s = 1 + 0.06f * Mathf.Sin(now * 7f);
                selection.rectTransform.localScale = new Vector3(s, s, 1);
            }
            if (highlight.enabled)
            {
                var c = highlight.color;
                c.a = 0.32f + 0.12f * Mathf.Sin(now * 8f);
                highlight.color = c;
            }

            UpdateHint(dt);
        }

        // ------------------------------------------------------------------ hint

        void UpdateHint(float dt)
        {
            if (!InputEnabled || playing || pointerDown || TargetMode >= 0 || game == null || game.Status != GameStatus.Playing)
            {
                idle = 0;
                return;
            }
            idle += dt;
            if (hintShown || idle < HintDelay || HintProvider == null)
                return;
            hintShown = true;
            var hint = HintProvider();
            if (hint == null)
                return;
            hintA = PieceAt(hint.Value.A);
            hintB = PieceAt(hint.Value.B);
            if (hintA == null || hintB == null)
            {
                hintA = hintB = null;
                return;
            }
            var dir = (At(hint.Value.B) - At(hint.Value.A)).normalized;
            hintA.Hint(dir, now);
            hintB.Hint(-dir, now);
        }

        void StopHint()
        {
            hintA?.StopHint();
            hintB?.StopHint();
            hintA = hintB = null;
        }

        /// <summary>Any touch or move starts the hint clock again.</summary>
        void Poke()
        {
            idle = 0;
            hintShown = false;
            StopHint();
        }

        // ------------------------------------------------------------------ input

        bool CellUnder(PointerEventData e, out Pos p, out Vector2 local)
        {
            p = Pos.None;
            local = Vector2.zero;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(piecesRt, e.position, e.pressEventCamera, out var lp))
                return false;
            local = lp + piecesRt.rect.size * piecesRt.pivot;
            int x = Mathf.FloorToInt(local.x / cell), yb = Mathf.FloorToInt(local.y / cell);
            int y = rows - 1 - yb;
            if (x < 0 || y < 0 || x >= cols || y >= rows)
                return false;
            if (game != null && !game.Topo.IsCell(x, y))
                return false;
            p = new Pos(x, y);
            return true;
        }

        public void OnPointerDown(PointerEventData e)
        {
            Poke();
            pointerDown = true;
            swiped = false;
            downCell = Pos.None;
            if (!InputEnabled || playing)
                return;
            if (!CellUnder(e, out var p, out var local))
                return;
            downCell = p;
            downLocal = local;
            if (TargetMode >= 0)
                Aim(p);
        }

        public void OnBeginDrag(PointerEventData e)
        {
        }

        public void OnDrag(PointerEventData e)
        {
            if (!InputEnabled || playing || downCell == Pos.None)
                return;
            if (TargetMode >= 0)
            {
                if (CellUnder(e, out var over, out _))
                    Aim(over);
                return;
            }
            if (swiped)
                return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(piecesRt, e.position, e.pressEventCamera, out var lp))
                return;
            var local = lp + piecesRt.rect.size * piecesRt.pivot;
            var d = local - downLocal;
            if (d.magnitude < cell * 0.3f)
                return;
            swiped = true;
            var to = Mathf.Abs(d.x) > Mathf.Abs(d.y)
                ? new Pos(downCell.X + (d.x > 0 ? 1 : -1), downCell.Y)
                : new Pos(downCell.X, downCell.Y + (d.y > 0 ? -1 : 1)); // up on screen is a smaller row
            Select(Pos.None);
            if (game != null && game.Topo.IsCell(to))
                SwapGesture?.Invoke(downCell, to);
        }

        public void OnEndDrag(PointerEventData e)
        {
        }

        public void OnPointerUp(PointerEventData e)
        {
            pointerDown = false;
            if (!InputEnabled || playing || downCell == Pos.None)
                return;
            if (TargetMode >= 0)
            {
                highlight.enabled = false;
                if (CellUnder(e, out var at, out _))
                    TargetChosen?.Invoke(at);
                return;
            }
            if (swiped)
                return;
            var p = downCell;
            if (selected == Pos.None)
            {
                Select(p);
            }
            else if (selected == p)
            {
                Select(Pos.None);
            }
            else if (selected.IsAdjacent(p))
            {
                var a = selected;
                Select(Pos.None);
                SwapGesture?.Invoke(a, p);
            }
            else
            {
                Select(p);
            }
        }

        void Select(Pos p)
        {
            selected = p;
            selection.enabled = p != Pos.None;
            if (selection.enabled)
            {
                selection.rectTransform.anchoredPosition = At(p);
                selection.rectTransform.SetAsLastSibling();
            }
        }

        /// <summary>Lights the cell, row or column the booster would hit.</summary>
        void Aim(Pos p)
        {
            targetCell = p;
            highlight.enabled = true;
            var rt = highlight.rectTransform;
            switch (TargetMode)
            {
                case 1:
                    rt.sizeDelta = new Vector2(cols * cell, cell);
                    rt.anchoredPosition = new Vector2(cols * cell / 2, At(p).y);
                    break;
                case 2:
                    rt.sizeDelta = new Vector2(cell, rows * cell);
                    rt.anchoredPosition = new Vector2(At(p).x, rows * cell / 2);
                    break;
                default:
                    rt.sizeDelta = new Vector2(cell, cell);
                    rt.anchoredPosition = At(p);
                    break;
            }
            rt.SetAsLastSibling();
        }

        /// <summary>Booster aiming on (0 cell, 1 row, 2 column) or off (-1).</summary>
        public void SetTargetMode(int mode)
        {
            TargetMode = mode;
            highlight.enabled = false;
            Select(Pos.None);
            Poke();
        }
    }
}
