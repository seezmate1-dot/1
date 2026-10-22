using System;
using System.Collections.Generic;

namespace SunnyValley.Layout
{
    /// <summary>What a node is; the Unity side picks components by it (UiView).</summary>
    public enum NodeKind
    {
        /// <summary>The canvas itself.</summary>
        Root,
        /// <summary>An invisible container (the safe area, a row).</summary>
        Group,
        /// <summary>A coloured shape: panel, sub-panel, background.</summary>
        Panel,
        /// <summary>A picture (placeholder shapes until the art lands).</summary>
        Icon,
        /// <summary>Text.</summary>
        Label,
        /// <summary>Something to tap.</summary>
        Button,
        /// <summary>A currency plate: tappable (opens the shop).</summary>
        Plate,
        /// <summary>An on/off switch.</summary>
        Toggle,
        /// <summary>A progress bar; its fill is drawn by the view from Value.</summary>
        Bar,
        /// <summary>A scrolling list's window: children are clipped to it and scroll vertically.</summary>
        Scroll,
        /// <summary>The 3-in-a-row board: Cols × Rows cells of Cell units.</summary>
        Board,
        /// <summary>The dark veil behind a popup: covers the whole screen and swallows taps.</summary>
        Dim,
        /// <summary>The farm: a full-screen area panned and zoomed with fingers under the interface.</summary>
        World,
    }

    /// <summary>Shapes for panels and icons. Icons are named by IconId instead.</summary>
    public enum Shape
    {
        None,
        RoundRect,
        Circle,
    }

    [Flags]
    public enum NodeFlags
    {
        None = 0,
        /// <summary>Drawn under the cut-out and gesture bar on purpose (backgrounds, the dim veil, the farm).</summary>
        FullBleed = 1,
        /// <summary>May stick out of its parent (a badge on a corner). Still has to stay in the safe area.</summary>
        Overhang = 2,
        /// <summary>Exists but is not shown now.</summary>
        Hidden = 4,
    }

    public enum TextAlign
    {
        Left,
        Center,
        Right,
    }

    /// <summary>A label's text and how it may shrink: from Size down to MinSize, on up to MaxLines lines.</summary>
    public sealed class TextSpec
    {
        public string Value;
        public float Size;
        public float MinSize;
        public int MaxLines = 1;
        public TextAlign Align = TextAlign.Center;
        public uint Color = Tokens.TextDark;
        /// <summary>A dark outline for light text on busy backgrounds.</summary>
        public bool Outline;

        /// <summary>The size the text was fitted at (whole units, like Unity's Text.fontSize); set by TextFit.</summary>
        public int FittedSize;

        /// <summary>False when even MinSize does not fit: the layout check reports it.</summary>
        public bool Fits = true;

        /// <summary>The lines it was broken into at FittedSize.</summary>
        public List<string> Lines = new List<string>();
    }

    /// <summary>
    /// One element of the interface: a rectangle placed exactly like a Unity RectTransform (anchors in the parent
    /// plus offsets of the corners), with what is drawn in it. Screens are trees of these built in plain C#
    /// (Screens/*.cs); the tree is resolved into rectangles here and checked by tests on many screens; the Unity
    /// side (UiView) turns the very same tree into GameObjects, setting the same anchors and offsets.
    /// </summary>
    public sealed class UiNode
    {
        public readonly string Id;
        public readonly NodeKind Kind;

        public V2 AnchorMin = V2.Zero;
        public V2 AnchorMax = V2.One;
        public V2 OffsetMin = V2.Zero;
        public V2 OffsetMax = V2.Zero;
        public V2 Pivot = V2.Half;

        public NodeFlags Flags;
        public Shape Shape;
        public float Radius;
        public uint Color = Tokens.White;
        /// <summary>A darker band under a shape (the cartoon «thickness» of buttons and panels), in units; 0 for none.</summary>
        public float Depth;
        public uint DepthColor = Tokens.Shadow;
        public string Icon;
        public TextSpec Text;

        /// <summary>Progress bars: 0..1. Toggles: 1 = on.</summary>
        public float Value;

        /// <summary>Boards: columns, rows and the cell size.</summary>
        public int Cols, Rows;
        public float Cell;

        public UiNode Parent { get; private set; }
        public readonly List<UiNode> Children = new List<UiNode>();

        /// <summary>The resolved rectangle in canvas units (lower left origin), set by Resolve.</summary>
        public Box Rect;

        public UiNode(string id, NodeKind kind)
        {
            Id = id;
            Kind = kind;
        }

        /// <summary>Things a finger may tap. The farm and the dim veil take taps too but sit under or over everything by design.</summary>
        public bool Interactive => Kind == NodeKind.Button || Kind == NodeKind.Plate || Kind == NodeKind.Toggle ||
                                   Kind == NodeKind.Board || Kind == NodeKind.Scroll;

        public bool Has(NodeFlags flag) => (Flags & flag) != 0;

        public UiNode Add(UiNode child)
        {
            child.Parent = this;
            Children.Add(child);
            return child;
        }

        // ------------------------------------------------------------------ placing (all set RectTransform-style anchors)

        /// <summary>Stretches over the parent with margins from each edge.</summary>
        public UiNode Stretch(float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            AnchorMin = V2.Zero;
            AnchorMax = V2.One;
            OffsetMin = new V2(left, bottom);
            OffsetMax = new V2(-right, -top);
            return this;
        }

        /// <summary>Anchored to the top edge, stretched across with side margins; top margin and height.</summary>
        public UiNode StretchTop(float left, float right, float top, float height)
        {
            AnchorMin = new V2(0, 1);
            AnchorMax = new V2(1, 1);
            Pivot = new V2(0.5f, 1);
            OffsetMin = new V2(left, -top - height);
            OffsetMax = new V2(-right, -top);
            return this;
        }

        /// <summary>Anchored to the bottom edge, stretched across with side margins; bottom margin and height.</summary>
        public UiNode StretchBottom(float left, float right, float bottom, float height)
        {
            AnchorMin = new V2(0, 0);
            AnchorMax = new V2(1, 0);
            Pivot = new V2(0.5f, 0);
            OffsetMin = new V2(left, bottom);
            OffsetMax = new V2(-right, bottom + height);
            return this;
        }

        /// <summary>Stretched across with side margins, a fixed height centred vertically.</summary>
        public UiNode StretchMiddle(float left, float right, float height)
        {
            AnchorMin = new V2(0, 0.5f);
            AnchorMax = new V2(1, 0.5f);
            Pivot = V2.Half;
            OffsetMin = new V2(left, -height / 2);
            OffsetMax = new V2(-right, height / 2);
            return this;
        }

        /// <summary>
        /// A fixed size anchored to one of nine points of the parent: ax, ay in 0..1 (0, 1 = top left; 0.5, 0.5 = centre).
        /// dx, dy move it inwards from that point: right and down from the left/top, left and up from the right/bottom,
        /// and plain right/up from a centre.
        /// </summary>
        public UiNode At(float ax, float ay, float dx, float dy, float w, float h)
        {
            AnchorMin = new V2(ax, ay);
            AnchorMax = new V2(ax, ay);
            Pivot = new V2(ax, ay);
            float x = ax == 0 ? dx : ax == 1 ? -dx : dx;
            float y = ay == 1 ? -dy : ay == 0 ? dy : dy;
            // the rectangle's pivot sits at the anchor point moved by (x, y)
            float x0 = x - ax * w;
            float y0 = y - ay * h;
            OffsetMin = new V2(x0, y0);
            OffsetMax = new V2(x0 + w, y0 + h);
            return this;
        }

        /// <summary>Top-left anchored: x from the left, y down from the top.</summary>
        public UiNode TopLeft(float x, float y, float w, float h) => At(0, 1, x, y, w, h);

        public UiNode TopRight(float x, float y, float w, float h) => At(1, 1, x, y, w, h);

        public UiNode TopCenter(float dx, float y, float w, float h) => At(0.5f, 1, dx, y, w, h);

        public UiNode BottomCenter(float dx, float y, float w, float h) => At(0.5f, 0, dx, y, w, h);

        public UiNode Center(float dx, float dy, float w, float h) => At(0.5f, 0.5f, dx, dy, w, h);

        public UiNode LeftMiddle(float x, float dy, float w, float h) => At(0, 0.5f, x, dy, w, h);

        public UiNode RightMiddle(float x, float dy, float w, float h) => At(1, 0.5f, x, dy, w, h);

        // ------------------------------------------------------------------ resolving

        /// <summary>
        /// Computes Rect for this node and all below it from the parent's rectangle, with the same formula as
        /// RectTransform: min = parent.min + anchorMin × parent.size + offsetMin, max likewise.
        /// </summary>
        public void Resolve(Box parent)
        {
            float x0 = parent.X + AnchorMin.X * parent.W + OffsetMin.X;
            float y0 = parent.Y + AnchorMin.Y * parent.H + OffsetMin.Y;
            float x1 = parent.X + AnchorMax.X * parent.W + OffsetMax.X;
            float y1 = parent.Y + AnchorMax.Y * parent.H + OffsetMax.Y;
            Rect = Box.FromMinMax(x0, y0, x1, y1);
            foreach (var child in Children)
                child.Resolve(Rect);
        }

        // ------------------------------------------------------------------ walking

        public IEnumerable<UiNode> All()
        {
            yield return this;
            foreach (var child in Children)
            foreach (var n in child.All())
                yield return n;
        }

        public UiNode Find(string id)
        {
            foreach (var n in All())
            {
                if (n.Id == id)
                    return n;
            }
            return null;
        }

        public bool IsAncestorOf(UiNode other)
        {
            for (var p = other.Parent; p != null; p = p.Parent)
            {
                if (p == this)
                    return true;
            }
            return false;
        }

        /// <summary>True when this node or one above it has the flag.</summary>
        public bool InheritsFlag(NodeFlags flag)
        {
            for (var n = this; n != null; n = n.Parent)
            {
                if (n.Has(flag))
                    return true;
            }
            return false;
        }

        /// <summary>The nearest scrolling list above this node, or null.</summary>
        public UiNode ScrollAbove()
        {
            for (var p = Parent; p != null; p = p.Parent)
            {
                if (p.Kind == NodeKind.Scroll)
                    return p;
            }
            return null;
        }

        public override string ToString() => $"{Id} ({Kind}) {Rect}";
    }
}
