using System;

namespace SunnyValley.Layout
{
    /// <summary>A point or a size in canvas units. Plain C#, so the layout runs and is tested without Unity.</summary>
    public readonly struct V2
    {
        public readonly float X;
        public readonly float Y;

        public V2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static readonly V2 Zero = new V2(0, 0);
        public static readonly V2 One = new V2(1, 1);
        public static readonly V2 Half = new V2(0.5f, 0.5f);

        public static V2 operator +(V2 a, V2 b) => new V2(a.X + b.X, a.Y + b.Y);
        public static V2 operator -(V2 a, V2 b) => new V2(a.X - b.X, a.Y - b.Y);

        public override string ToString() => $"({X:0.#}, {Y:0.#})";
    }

    /// <summary>
    /// A rectangle in canvas units with Unity's convention: X, Y is the lower left corner and Y grows upwards
    /// (the same as RectTransform.rect and Screen.safeArea), so numbers can be compared with the editor directly.
    /// </summary>
    public readonly struct Box
    {
        public readonly float X;
        public readonly float Y;
        public readonly float W;
        public readonly float H;

        public Box(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public float XMax => X + W;
        public float YMax => Y + H;
        public float CenterX => X + W / 2;
        public float CenterY => Y + H / 2;
        public float MinSide => Math.Min(W, H);

        public static Box FromMinMax(float x0, float y0, float x1, float y1) => new Box(x0, y0, x1 - x0, y1 - y0);

        /// <summary>True when other lies inside this box, allowing eps of rounding.</summary>
        public bool Contains(Box other, float eps = 0.5f) =>
            other.X >= X - eps && other.Y >= Y - eps && other.XMax <= XMax + eps && other.YMax <= YMax + eps;

        /// <summary>True when the horizontal extent of other lies inside this box.</summary>
        public bool ContainsX(Box other, float eps = 0.5f) => other.X >= X - eps && other.XMax <= XMax + eps;

        /// <summary>Area shared by the two boxes (0 when they only touch).</summary>
        public float OverlapArea(Box other)
        {
            float w = Math.Min(XMax, other.XMax) - Math.Max(X, other.X);
            float h = Math.Min(YMax, other.YMax) - Math.Max(Y, other.Y);
            return w > 0 && h > 0 ? w * h : 0;
        }

        /// <summary>The empty gap between two boxes along the axis where they are apart (negative when they overlap).</summary>
        public float Gap(Box other)
        {
            float gx = Math.Max(other.X - XMax, X - other.XMax);
            float gy = Math.Max(other.Y - YMax, Y - other.YMax);
            return Math.Max(gx, gy);
        }

        public bool Intersects(Box other) => OverlapArea(other) > 0;

        public Box Inset(float left, float right, float top, float bottom) =>
            new Box(X + left, Y + bottom, W - left - right, H - top - bottom);

        public Box Inset(float all) => Inset(all, all, all, all);

        public override string ToString() => $"[{X:0.#}, {Y:0.#} {W:0.#}×{H:0.#}]";
    }
}
