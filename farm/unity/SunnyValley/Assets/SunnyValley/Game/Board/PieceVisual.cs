using SunnyValley.Core.Match3;
using SunnyValley.Layout;
using UnityEngine;
using UnityEngine.UI;

namespace SunnyValley.Game.Match3View
{
    /// <summary>How a piece travels from one point to another.</summary>
    public enum PathKind : byte
    {
        Line,
        /// <summary>A hop: rises by Arc (in cells) in the middle.</summary>
        Arc,
        /// <summary>There and back again (a swap without effect).</summary>
        PingPong,
        /// <summary>Through a portal: sinks and fades at From, rises and appears at To.</summary>
        Portal,
    }

    /// <summary>What happens to a piece's size (and how it fades) while it animates.</summary>
    public enum ScaleFx : byte
    {
        None,
        /// <summary>Squash, swell, shrink to nothing (matched).</summary>
        Pop,
        /// <summary>From nothing with an overshoot (a new special, a spawn into a hidden spawner).</summary>
        Appear,
        /// <summary>Squash on landing and a little bounce.</summary>
        Land,
        /// <summary>Shrinks while it flies into something (merges, baskets).</summary>
        Shrink,
        /// <summary>A quick bump (turned into a special).</summary>
        Punch,
        /// <summary>A bright swell and gone (a special fires).</summary>
        Fire,
        /// <summary>Jumps off the board (a rescued chick).</summary>
        Hop,
    }

    /// <summary>
    /// One piece on the board: a glow disc behind (specials, selection) and the picture. Its animations are plain
    /// fields (a move, a size effect, a release time) advanced by the board each frame — no allocations, no tween
    /// objects — so a hundred pieces can fall at once on a phone.
    /// </summary>
    public sealed class PieceVisual
    {
        public RectTransform Rt;
        public Image Back;
        public Image Icon;
        public RectTransform IconRt;

        public int Id;
        public PieceKind Kind;
        public TileColor Color;
        public SpecialKind Special;
        public Pos Cell;
        public bool Live;

        // move
        bool moving;
        Vector2 from, to, home;
        float moveT0, moveDur, arc;
        Easing.Kind moveEase;
        PathKind path;

        // size
        ScaleFx fx;
        float fxT0, fxDur;

        // fade-in (spawns out of nowhere)
        float fadeT0 = -1, fadeDur;

        /// <summary>The board clock time when the piece goes back to the pool (-1 never).</summary>
        public float ReleaseAt = -1;

        // idle hint wiggle
        bool hinting;
        Vector2 hintDir;
        float hintT0;

        float cellSize;

        public bool Active => moving || fx != ScaleFx.None || ReleaseAt >= 0 || hinting || fadeT0 >= 0;

        public void Setup(int id, PieceKind kind, TileColor color, SpecialKind special, Pos cell, Vector2 at, float cell0)
        {
            Id = id;
            Cell = cell;
            cellSize = cell0;
            moving = false;
            fx = ScaleFx.None;
            ReleaseAt = -1;
            hinting = false;
            fadeT0 = -1;
            home = at;
            Rt.anchoredPosition = at;
            Rt.localScale = Vector3.one;
            Rt.localRotation = Quaternion.identity;
            SetLook(kind, color, special);
            SetAlpha(1);
            Rt.gameObject.SetActive(true);
            Live = true;
        }

        public void SetLook(PieceKind kind, TileColor color, SpecialKind special)
        {
            Kind = kind;
            Color = color;
            Special = special;
            Icon.sprite = BoardSprites.Piece(kind, color, special);
            IconRt.localRotation = special == SpecialKind.LineH ? Quaternion.Euler(0, 0, -90) : Quaternion.identity;
            float s = kind == PieceKind.Chick ? 0.92f : special == SpecialKind.None ? 0.86f : 0.8f;
            IconRt.sizeDelta = new Vector2(cellSize * s, cellSize * s);
            bool glow = kind == PieceKind.Tile && special != SpecialKind.None;
            Back.enabled = glow;
            if (glow)
            {
                var c = BoardSprites.ColorOf(special == SpecialKind.Rainbow ? TileColor.None : color);
                Back.color = new Color(c.r, c.g, c.b, 0.9f);
                Back.rectTransform.sizeDelta = new Vector2(cellSize * 1.25f, cellSize * 1.25f);
            }
        }

        void SetAlpha(float a)
        {
            var c = Icon.color;
            c.a = a;
            Icon.color = c;
            if (Back.enabled)
            {
                var b = Back.color;
                b.a = 0.9f * a;
                Back.color = b;
            }
        }

        /// <summary>Starts a move to <paramref name="target"/> at board time t0 (the piece stays put until then).</summary>
        public void MoveTo(Vector2 target, float t0, float duration, Easing.Kind ease, PathKind kind = PathKind.Line, float arcCells = 0)
        {
            from = moving ? to : Rt.anchoredPosition;
            if (hinting)
            {
                hinting = false;
                from = home;
            }
            to = target;
            moveT0 = t0;
            moveDur = Mathf.Max(0.0001f, duration);
            moveEase = ease;
            path = kind;
            arc = arcCells * cellSize;
            moving = true;
            if (kind != PathKind.PingPong)
                home = target;
        }

        public void Effect(ScaleFx kind, float t0, float duration)
        {
            fx = kind;
            fxT0 = t0;
            fxDur = Mathf.Max(0.0001f, duration);
        }

        public void FadeIn(float t0, float duration)
        {
            fadeT0 = t0;
            fadeDur = Mathf.Max(0.0001f, duration);
            SetAlpha(0);
        }

        public void Hint(Vector2 towards, float t0)
        {
            hinting = true;
            hintDir = towards;
            hintT0 = t0;
        }

        public void StopHint()
        {
            if (!hinting)
                return;
            hinting = false;
            Rt.anchoredPosition = home;
            Rt.localRotation = Quaternion.identity;
        }

        /// <summary>Puts the piece where it belongs now, ending every animation (a rebuild, a resync).</summary>
        public void Snap(Vector2 at)
        {
            moving = false;
            fx = ScaleFx.None;
            hinting = false;
            fadeT0 = -1;
            home = at;
            Rt.anchoredPosition = at;
            Rt.localScale = Vector3.one;
            Rt.localRotation = Quaternion.identity;
            SetAlpha(1);
        }

        /// <summary>Advances the animations to board time <paramref name="now"/>. Returns false when the piece should go back to the pool.</summary>
        public bool Tick(float now)
        {
            if (moving && now >= moveT0)
            {
                float k = Mathf.Clamp01((now - moveT0) / moveDur);
                float e = Easing.Apply(moveEase, k);
                Vector2 p;
                switch (path)
                {
                    case PathKind.Arc:
                        p = Vector2.LerpUnclamped(from, to, e);
                        p.y += arc * 4 * k * (1 - k);
                        break;
                    case PathKind.PingPong:
                    {
                        float q = k < 0.5f ? Easing.Apply(Easing.Kind.OutQuad, k * 2) : Easing.Apply(Easing.Kind.InOutQuad, (1 - k) * 2);
                        p = Vector2.LerpUnclamped(from, to, q);
                        break;
                    }
                    case PathKind.Portal:
                    {
                        // first half: sink into the entrance; second half: rise out of the exit
                        float half = cellSize * 0.5f;
                        if (k < 0.5f)
                        {
                            p = from + new Vector2(0, -half * k * 2);
                            SetAlpha(1 - k * 2);
                        }
                        else
                        {
                            p = to + new Vector2(0, half * (1 - k) * 2);
                            SetAlpha((k - 0.5f) * 2);
                        }
                        break;
                    }
                    default:
                        p = Vector2.LerpUnclamped(from, to, e);
                        break;
                }
                Rt.anchoredPosition = p;
                if (k >= 1)
                {
                    moving = false;
                    if (path == PathKind.Portal)
                        SetAlpha(1);
                }
            }

            if (fx != ScaleFx.None && now >= fxT0)
            {
                float k = Mathf.Clamp01((now - fxT0) / fxDur);
                Vector3 s = Vector3.one;
                switch (fx)
                {
                    case ScaleFx.Pop:
                        if (k < 0.3f)
                        {
                            float q = k / 0.3f;
                            s = new Vector3(1 + 0.28f * q, 1 + 0.12f * q, 1);
                        }
                        else
                        {
                            float q = (k - 0.3f) / 0.7f;
                            float g = (1 - q * q) * 1.25f;
                            s = new Vector3(g * (1.02f + 0.03f * q), g, 1);
                            SetAlpha(1 - q * q);
                        }
                        break;
                    case ScaleFx.Appear:
                        s = Vector3.one * Easing.OutBack(k, 2.2f);
                        break;
                    case ScaleFx.Land:
                    {
                        float q = Mathf.Sin(k * Mathf.PI) * (1 - k);
                        s = new Vector3(1 + 0.16f * q, 1 - 0.2f * q, 1);
                        break;
                    }
                    case ScaleFx.Shrink:
                        s = Vector3.one * Mathf.Lerp(1, 0.45f, k * k);
                        SetAlpha(1 - Mathf.Clamp01((k - 0.6f) / 0.4f));
                        break;
                    case ScaleFx.Punch:
                        s = Vector3.one * (1 + 0.3f * Mathf.Sin(k * Mathf.PI));
                        break;
                    case ScaleFx.Fire:
                        s = Vector3.one * (1 + 0.5f * k);
                        SetAlpha(1 - k);
                        break;
                    case ScaleFx.Hop:
                        s = Vector3.one * (1 + 0.25f * Mathf.Sin(k * Mathf.PI));
                        SetAlpha(1 - Mathf.Clamp01((k - 0.5f) * 2));
                        break;
                }
                Rt.localScale = s;
                if (k >= 1)
                {
                    bool gone = fx == ScaleFx.Pop || fx == ScaleFx.Shrink || fx == ScaleFx.Fire || fx == ScaleFx.Hop;
                    fx = ScaleFx.None;
                    if (!gone)
                        Rt.localScale = Vector3.one;
                }
            }

            if (fadeT0 >= 0 && now >= fadeT0)
            {
                float k = Mathf.Clamp01((now - fadeT0) / fadeDur);
                SetAlpha(k);
                if (k >= 1)
                    fadeT0 = -1;
            }

            if (hinting && !moving)
            {
                float t = now - hintT0;
                // two small nudges towards the partner, then a pause; repeats
                float cyc = t % 1.6f;
                float q = cyc < 0.7f ? Mathf.Sin(cyc / 0.7f * Mathf.PI * 2) * Mathf.Sin(cyc / 0.7f * Mathf.PI) : 0;
                Rt.anchoredPosition = home + hintDir * (q * cellSize * 0.09f);
                Rt.localRotation = Quaternion.Euler(0, 0, q * 7f);
            }

            return !(ReleaseAt >= 0 && now >= ReleaseAt);
        }
    }
}
