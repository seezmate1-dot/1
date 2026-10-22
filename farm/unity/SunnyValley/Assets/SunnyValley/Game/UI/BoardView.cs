using System;
using System.Collections.Generic;
using SunnyValley.Layout;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SunnyValley.Game.UI
{
    /// <summary>
    /// The board inside the rectangle the layout gave it: Cols × Rows cells of the model's cell size (whole units,
    /// so the grid is crisp), a checker of light cells and a piece in each. Taps come back as (col, row), row 0 at
    /// the bottom. Changed cells drop in from above with a bounce.
    /// </summary>
    public sealed class BoardView : MonoBehaviour, IPointerClickHandler
    {
        int cols, rows;
        float cell;
        Image[,] pieces;
        Func<int, int, int> pieceAt;

        /// <summary>A tap on a cell.</summary>
        public event Action<int, int> Tapped;

        public void Build(UiNode node)
        {
            cols = node.Cols;
            rows = node.Rows;
            cell = node.Cell;
            pieces = new Image[cols, rows];
            var parent = (RectTransform)transform;
            for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
            {
                var bg = Cell("cell", parent, c, r, 4).gameObject.AddComponent<Image>();
                bg.sprite = ShapeSprites.RoundRect;
                bg.type = Image.Type.Sliced;
                bg.pixelsPerUnitMultiplier = ShapeSprites.SliceRadius / Mathf.Max(4, cell * 0.16f);
                bg.color = UiView.ToColor((c + r) % 2 == 0 ? 0xC9EBA0F0u : 0xB5DE88F0u);
                bg.raycastTarget = false;
            }
            for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
            {
                var img = Cell("piece", parent, c, r, cell * 0.08f).gameObject.AddComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.enabled = false;
                pieces[c, r] = img;
            }
        }

        RectTransform Cell(string name, RectTransform parent, int c, int r, float inset)
        {
            var go = new GameObject(name + " " + c + "," + r, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(c * cell + inset, r * cell + inset);
            rt.sizeDelta = new Vector2(cell - 2 * inset, cell - 2 * inset);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition += rt.sizeDelta / 2;
            return rt;
        }

        /// <summary>Shows a level's pieces; pieceAt gives 0..5 or -1.</summary>
        public void Show(Func<int, int, int> pieceAt)
        {
            this.pieceAt = pieceAt;
            for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
                SetPiece(c, r, pieceAt(c, r));
        }

        void SetPiece(int c, int r, int kind)
        {
            var img = pieces[c, r];
            img.enabled = kind >= 0;
            if (kind >= 0)
                img.sprite = ShapeSprites.Icon(Icons.Pieces[kind % Icons.Pieces.Length]);
        }

        /// <summary>Redraws changed cells: each drops from above its place, the higher ones a moment later.</summary>
        public void Refresh(IReadOnlyList<(int col, int row)> changed)
        {
            if (pieceAt == null)
                return;
            foreach (var (c, r) in changed)
            {
                if (c < 0 || r < 0 || c >= cols || r >= rows)
                    continue;
                SetPiece(c, r, pieceAt(c, r));
                var rt = pieces[c, r].rectTransform;
                Vector2 home = new Vector2(c * cell + cell / 2, r * cell + cell / 2);
                Vector2 from = home + new Vector2(0, cell * 1.2f);
                Tweener.Move(rt, from, home, 0.32f, Easing.Kind.OutBack, null, r * 0.025f);
            }
        }

        public void OnPointerClick(PointerEventData e)
        {
            var rt = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var local))
                return;
            // local is relative to the pivot: move it to the lower left corner
            local += rt.rect.size * rt.pivot;
            int c = Mathf.FloorToInt(local.x / cell), r = Mathf.FloorToInt(local.y / cell);
            if (c >= 0 && r >= 0 && c < cols && r < rows)
                Tapped?.Invoke(c, r);
        }
    }
}
