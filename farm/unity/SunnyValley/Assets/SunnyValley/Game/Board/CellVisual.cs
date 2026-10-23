using SunnyValley.Core.Match3;
using SunnyValley.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SunnyValley.Game.Match3View
{
    /// <summary>
    /// The obstacles of one cell as the player sees them: grass under the piece, a blocker instead of it (crate 1–3,
    /// stone, weed, garden bed with its sprout, basket with its counter) and an overlay on it (ice, chain — two
    /// crossed for two layers — honey). Pictures are made on first need; a hit punches the picture.
    /// </summary>
    public sealed class CellVisual
    {
        public Pos Cell;
        public Vector2 At;
        float size;
        RectTransform under, over;

        public int Grass;
        public ObstacleKind Blocker;
        public int BlockerLayers;
        public int BedNeed;
        public TileColor BasketColor;
        /// <summary>What grows in garden beds (the level's crop).</summary>
        public Sprite Crop;
        public ObstacleKind Overlay;
        public int OverlayLayers;

        Image grass, blocker, sprout, overlay, overlay2;
        Text counter;
        float punchT0 = -1;
        bool punchOver;

        public bool Animating => punchT0 >= 0;

        public CellVisual(Pos cell, Vector2 at, float size, RectTransform under, RectTransform over)
        {
            Cell = cell;
            At = at;
            this.size = size;
            this.under = under;
            this.over = over;
        }

        Image Make(string name, RectTransform parent, float scale)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = At;
            rt.sizeDelta = new Vector2(size * scale, size * scale);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        /// <summary>Shows the state of the board's cell.</summary>
        public void Read(Board b, int i)
        {
            SetGrass(b.Grass[i]);
            BedNeed = b.BedNeed[i];
            BasketColor = b.BasketColor[i];
            SetBlocker(b.Blocker[i], b.BlockerLayers[i]);
            SetOverlay(b.Overlay[i], b.OverlayLayers[i]);
        }

        public bool Matches(Board b, int i) =>
            Grass == b.Grass[i] && Blocker == b.Blocker[i] && BlockerLayers == b.BlockerLayers[i] &&
            Overlay == b.Overlay[i] && OverlayLayers == b.OverlayLayers[i];

        public void SetGrass(int layers)
        {
            Grass = layers;
            if (layers <= 0)
            {
                if (grass != null)
                    grass.enabled = false;
                return;
            }
            if (grass == null)
            {
                grass = Make("grass", under, 0.98f);
                grass.preserveAspect = false;
                grass.rectTransform.SetAsFirstSibling();
            }
            grass.enabled = true;
            grass.sprite = BoardSprites.Grass;
            grass.color = layers >= 2 ? new Color(0.72f, 0.86f, 0.62f) : Color.white;
        }

        public void SetBlocker(ObstacleKind kind, int layers)
        {
            Blocker = kind;
            BlockerLayers = layers;
            if (kind == ObstacleKind.None)
            {
                if (blocker != null)
                    blocker.enabled = false;
                if (sprout != null)
                    sprout.enabled = false;
                if (counter != null)
                    counter.enabled = false;
                return;
            }
            if (blocker == null)
                blocker = Make("blocker", under, 0.96f);
            blocker.enabled = true;
            blocker.sprite = BoardSprites.Obstacle(kind, layers);
            blocker.color = kind == ObstacleKind.Stone && layers >= 2 ? new Color(0.78f, 0.78f, 0.82f) : Color.white;

            if (kind == ObstacleKind.Bed)
            {
                if (sprout == null)
                    sprout = Make("sprout", under, 0.6f);
                float grow = BedNeed > 0 ? Mathf.Clamp01((float)layers / BedNeed) : 0;
                sprout.enabled = layers > 0;
                sprout.sprite = Crop != null ? Crop : BoardSprites.Tile(TileColor.Carrot);
                sprout.rectTransform.sizeDelta = Vector2.one * size * Mathf.Lerp(0.3f, 0.62f, grow);
                sprout.rectTransform.anchoredPosition = At + new Vector2(0, size * 0.08f);
            }
            else if (sprout != null)
            {
                sprout.enabled = false;
            }

            if (kind == ObstacleKind.Basket)
            {
                if (counter == null)
                {
                    var go = new GameObject("count", typeof(RectTransform));
                    go.layer = under.gameObject.layer;
                    var rt = (RectTransform)go.transform;
                    rt.SetParent(under, false);
                    rt.anchorMin = rt.anchorMax = Vector2.zero;
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = At + new Vector2(0, -size * 0.12f);
                    rt.sizeDelta = new Vector2(size, size * 0.5f);
                    counter = go.AddComponent<Text>();
                    counter.font = UiView.Font;
                    counter.fontSize = Mathf.RoundToInt(size * 0.34f);
                    counter.alignment = TextAnchor.MiddleCenter;
                    counter.raycastTarget = false;
                    var outline = go.AddComponent<Outline>();
                    outline.effectColor = new Color(0.25f, 0.13f, 0.03f, 0.9f);
                    outline.effectDistance = new Vector2(2, -2);
                    if (sprout == null)
                        sprout = Make("sprout", under, 0.42f);
                }
                counter.enabled = true;
                counter.text = layers.ToString();
                counter.color = Color.white;
                sprout.enabled = BasketColor != TileColor.None;
                if (sprout.enabled)
                {
                    sprout.sprite = BoardSprites.Tile(BasketColor);
                    sprout.rectTransform.sizeDelta = Vector2.one * size * 0.42f;
                    sprout.rectTransform.anchoredPosition = At + new Vector2(0, size * 0.2f);
                }
                counter.rectTransform.SetAsLastSibling();
            }
            else if (counter != null)
            {
                counter.enabled = false;
            }
        }

        public void SetOverlay(ObstacleKind kind, int layers)
        {
            Overlay = kind;
            OverlayLayers = layers;
            if (kind == ObstacleKind.None)
            {
                if (overlay != null)
                    overlay.enabled = false;
                if (overlay2 != null)
                    overlay2.enabled = false;
                return;
            }
            if (overlay == null)
                overlay = Make("overlay", over, 1f);
            overlay.enabled = true;
            overlay.sprite = BoardSprites.Obstacle(kind, layers);
            overlay.rectTransform.localRotation = Quaternion.identity;
            switch (kind)
            {
                case ObstacleKind.Ice:
                    overlay.color = new Color(1, 1, 1, layers >= 2 ? 0.92f : 0.68f);
                    overlay.rectTransform.sizeDelta = Vector2.one * size * (layers >= 2 ? 1.02f : 0.96f);
                    break;
                case ObstacleKind.Honey:
                    overlay.color = new Color(1, 1, 1, 0.82f);
                    overlay.rectTransform.sizeDelta = Vector2.one * size;
                    break;
                default:
                    overlay.color = Color.white;
                    overlay.rectTransform.sizeDelta = Vector2.one * size * 0.95f;
                    overlay.rectTransform.localRotation = Quaternion.Euler(0, 0, 35);
                    break;
            }
            bool second = kind == ObstacleKind.Chain && layers >= 2;
            if (second && overlay2 == null)
                overlay2 = Make("overlay2", over, 0.95f);
            if (overlay2 != null)
            {
                overlay2.enabled = second;
                if (second)
                {
                    overlay2.sprite = BoardSprites.Chain;
                    overlay2.rectTransform.localRotation = Quaternion.Euler(0, 0, -35);
                }
            }
        }

        /// <summary>Applies one hit from the step log.</summary>
        public void Apply(ObstacleHitEvent h, float now)
        {
            switch (h.Obstacle)
            {
                case ObstacleKind.Grass:
                    SetGrass(h.LayersAfter);
                    break;
                case ObstacleKind.Ice:
                case ObstacleKind.Chain:
                case ObstacleKind.Honey:
                    SetOverlay(h.Removed ? ObstacleKind.None : h.Obstacle, h.Removed ? 0 : h.LayersAfter);
                    punchOver = true;
                    break;
                default:
                    SetBlocker(h.Removed ? ObstacleKind.None : h.Obstacle, h.Removed ? 0 : h.LayersAfter);
                    punchOver = false;
                    break;
            }
            punchT0 = now;
        }

        /// <summary>A bump of the picture (it grew here).</summary>
        public void Punch(float now)
        {
            punchOver = Overlay != ObstacleKind.None;
            punchT0 = now;
        }

        public void Tick(float now)
        {
            if (punchT0 < 0)
                return;
            float k = Mathf.Clamp01((now - punchT0) / 0.22f);
            float s = 1 + 0.16f * Mathf.Sin(k * Mathf.PI) * (1 - k * 0.5f);
            var img = punchOver ? overlay : blocker;
            if (img != null)
                img.rectTransform.localScale = new Vector3(s, s, 1);
            if (punchOver && overlay2 != null)
                overlay2.rectTransform.localScale = new Vector3(s, s, 1);
            if (!punchOver && sprout != null)
                sprout.rectTransform.localScale = new Vector3(s, s, 1);
            if (k >= 1)
                punchT0 = -1;
        }

        /// <summary>Colour of the debris when it breaks.</summary>
        public static Color Debris(ObstacleKind kind)
        {
            switch (kind)
            {
                case ObstacleKind.Crate: return new Color(0.72f, 0.5f, 0.28f);
                case ObstacleKind.Ice: return new Color(0.75f, 0.93f, 1f);
                case ObstacleKind.Chain: return new Color(0.6f, 0.62f, 0.66f);
                case ObstacleKind.Honey: return new Color(1f, 0.72f, 0.15f);
                case ObstacleKind.Stone: return new Color(0.6f, 0.6f, 0.62f);
                case ObstacleKind.Weed: return new Color(0.36f, 0.62f, 0.2f);
                case ObstacleKind.Grass: return new Color(0.45f, 0.78f, 0.28f);
                case ObstacleKind.Bed: return new Color(0.5f, 0.33f, 0.18f);
                case ObstacleKind.Basket: return new Color(0.9f, 0.7f, 0.4f);
                default: return Color.white;
            }
        }
    }
}
