namespace SunnyValley.Layout
{
    /// <summary>
    /// A screen's layout on one particular screen: the tree of nodes under the canvas root, with a «safe» group that
    /// covers exactly the safe area. Begin makes the frame, the screen adds its elements, Finish resolves every
    /// rectangle and fits every label. Interactive elements go under Safe; only full-bleed backgrounds go to Root.
    /// </summary>
    public sealed class UiLayout
    {
        public readonly string Name;
        public readonly CanvasFit Fit;
        public readonly UiNode Root;
        public readonly UiNode Safe;

        UiLayout(string name, CanvasFit fit)
        {
            Name = name;
            Fit = fit;
            Root = new UiNode(name, NodeKind.Root);
            Safe = new UiNode(name + ".safe", NodeKind.Group);
            // the safe area as anchors in the canvas: the classic SafeArea pattern, the same numbers Unity gets
            var s = fit.Screen.SafeArea;
            Safe.AnchorMin = new V2(s.X / fit.Screen.Width, s.Y / fit.Screen.Height);
            Safe.AnchorMax = new V2(s.XMax / fit.Screen.Width, s.YMax / fit.Screen.Height);
            Safe.OffsetMin = V2.Zero;
            Safe.OffsetMax = V2.Zero;
        }

        public static UiLayout Begin(string name, CanvasFit fit) => new UiLayout(name, fit);

        /// <summary>Adds the safe group last of the root's children so it is drawn over the backgrounds.</summary>
        public UiLayout Finish()
        {
            if (Safe.Parent == null)
                Root.Add(Safe);
            Root.Resolve(Fit.Canvas);
            TextFit.FitAll(Root, Fit.Scale);
            return this;
        }

        /// <summary>The safe area's width and height in canvas units.</summary>
        public float W => Fit.Safe.W;
        public float H => Fit.Safe.H;

        public UiNode Find(string id) => Root.Find(id);
    }
}
