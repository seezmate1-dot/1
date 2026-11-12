using System;

namespace SunnyValley.Game.Farm
{
    /// <summary>A point or a vector on the map or the view (canvas units).</summary>
    public struct P2
    {
        public float X, Y;

        public P2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static P2 operator +(P2 a, P2 b) => new P2(a.X + b.X, a.Y + b.Y);
        public static P2 operator -(P2 a, P2 b) => new P2(a.X - b.X, a.Y - b.Y);
        public static P2 operator *(P2 a, float k) => new P2(a.X * k, a.Y * k);
        public float Length => (float)Math.Sqrt(X * X + Y * Y);
        public static float Distance(P2 a, P2 b) => (a - b).Length;
        public override string ToString() => "(" + X.ToString("0.#") + ", " + Y.ToString("0.#") + ")";
    }

    /// <summary>
    /// The farm's camera as numbers (docs/FARM_UX.md §3): where the map lies in the view and how big it is drawn.
    /// One finger drags it; past the edge it follows the finger less and less (a rubber band) and springs back when
    /// let go; a flick keeps it gliding and friction stops it; two fingers zoom about the point between them inside
    /// a clamped range (a little past the range while the fingers hold, back on release). Insets keep room for the
    /// HUD: every map point can be brought into the part of the view no button covers.
    /// Coordinates: the view's origin is its centre; <see cref="Position"/> is where the map's centre is in the view
    /// (exactly the anchoredPosition of FarmWorld's content); the map's own origin is its lower left corner.
    /// Plain C#: FarmWorld feeds it fingers and frames, the tests feed it numbers.
    /// </summary>
    public sealed class FarmCamera
    {
        public const float MinZoom = 0.35f;
        public const float MaxZoom = 1.6f;
        public const float StartZoom = 0.8f;
        /// <summary>The zoom a double tap goes to (and back from).</summary>
        public const float CloseZoom = 1.25f;
        /// <summary>How far past an edge a finger can pull the map, canvas units.</summary>
        public const float MaxOverscroll = 170;
        /// <summary>How far past the zoom range a pinch can stretch (a factor).</summary>
        public const float ZoomStretch = 1.12f;
        /// <summary>Glide: the speed falls e times every 1/Friction seconds.</summary>
        public const float Friction = 4.5f;
        public const float MaxSpeed = 6000;
        public const float StopSpeed = 12;
        /// <summary>How fast the map springs back from past an edge (1/s).</summary>
        public const float Spring = 13;

        public float MapW, MapH;
        public float ViewW = 1080, ViewH = 1920;
        /// <summary>Parts of the view under the HUD (top plates, the play button): the map may slide out from under them.</summary>
        public float InsetLeft, InsetRight, InsetTop, InsetBottom;

        public P2 Position;
        public float Zoom = StartZoom;
        P2 velocity;
        P2 zoomFocus;

        public FarmCamera(float mapW, float mapH)
        {
            MapW = mapW;
            MapH = mapH;
        }

        public P2 Velocity => velocity;

        /// <summary>Gliding after a flick or springing back from an edge or a zoom past the range.</summary>
        public bool Moving { get; private set; }

        // ------------------------------------------------------------------ limits

        /// <summary>The smallest zoom allowed: the map always covers the whole view.</summary>
        public float LowestZoom => Math.Min(MaxZoom, Math.Max(MinZoom, Math.Max(ViewW / Math.Max(1, MapW), ViewH / Math.Max(1, MapH))));

        public float ClampZoom(float z) => Math.Max(LowestZoom, Math.Min(MaxZoom, z));

        /// <summary>The range of Position on each axis at a zoom: the map covers the view but for the insets.</summary>
        public void Bounds(float zoom, out float minX, out float maxX, out float minY, out float maxY)
        {
            float hx = Math.Max(0, (MapW * zoom - ViewW) / 2), hy = Math.Max(0, (MapH * zoom - ViewH) / 2);
            minX = -hx - InsetRight;
            maxX = hx + InsetLeft;
            minY = -hy - InsetTop;
            maxY = hy + InsetBottom;
        }

        public P2 ClampPosition(P2 p, float zoom)
        {
            Bounds(zoom, out float minX, out float maxX, out float minY, out float maxY);
            return new P2(Math.Max(minX, Math.Min(maxX, p.X)), Math.Max(minY, Math.Min(maxY, p.Y)));
        }

        /// <summary>Hard limits at once (a screen rebuild, a camera flight's frame).</summary>
        public void Clamp()
        {
            Zoom = ClampZoom(Zoom);
            Position = ClampPosition(Position, Zoom);
        }

        // ------------------------------------------------------------------ points

        /// <summary>A view point (origin: the view's centre) on the map (origin: its lower left corner).</summary>
        public P2 ViewToMap(P2 view) => new P2((view.X - Position.X) / Zoom + MapW / 2, (view.Y - Position.Y) / Zoom + MapH / 2);

        public P2 MapToView(P2 map) => new P2((map.X - MapW / 2) * Zoom + Position.X, (map.Y - MapH / 2) * Zoom + Position.Y);

        /// <summary>The Position that shows a map point at a view point (the centre of the free part by default).</summary>
        public P2 PositionFor(P2 map, float zoom, P2 at) => new P2(at.X - (map.X - MapW / 2) * zoom, at.Y - (map.Y - MapH / 2) * zoom);

        /// <summary>The middle of the view's part the HUD leaves free.</summary>
        public P2 FreeCentre => new P2((InsetLeft - InsetRight) / 2, (InsetBottom - InsetTop) / 2);

        /// <summary>Where a flight to a map point ends: the point in the middle of the free part, inside the limits.</summary>
        public P2 Target(P2 map, float zoom)
        {
            zoom = ClampZoom(zoom);
            return ClampPosition(PositionFor(map, zoom, FreeCentre), zoom);
        }

        // ------------------------------------------------------------------ fingers

        /// <summary>A finger went down: the glide stops under it.</summary>
        public void Grab()
        {
            velocity = new P2(0, 0);
            Moving = false;
        }

        /// <summary>One finger moved by a view delta: the map follows; past an edge, less and less.</summary>
        public void DragBy(P2 delta)
        {
            Bounds(Zoom, out float minX, out float maxX, out float minY, out float maxY);
            Position = new P2(Rubber(Position.X, delta.X, minX, maxX), Rubber(Position.Y, delta.Y, minY, maxY));
        }

        static float Rubber(float p, float d, float min, float max)
        {
            float over = p > max ? p - max : p < min ? min - p : 0;
            bool outward = p >= max && d > 0 || p <= min && d < 0;
            if (outward)
                d *= Math.Max(0, 1 - over / MaxOverscroll);
            float next = p + d;
            // never further than the band stretches
            return Math.Max(min - MaxOverscroll, Math.Min(max + MaxOverscroll, next));
        }

        /// <summary>The finger left with a speed (view units per second): the map glides on.</summary>
        public void Release(P2 v)
        {
            float speed = v.Length;
            if (speed > MaxSpeed)
                v = v * (MaxSpeed / speed);
            velocity = speed < 60 ? new P2(0, 0) : v;
            Moving = true;
        }

        /// <summary>
        /// Two fingers: zooms by a factor about a view point, which stays under the fingers. The zoom may stretch a
        /// little past its range while they hold; <see cref="Step"/> brings it back.
        /// </summary>
        public void ZoomAbout(P2 view, float factor, bool stretch = true)
        {
            if (factor <= 0 || float.IsNaN(factor) || float.IsInfinity(factor))
                return;
            float lo = LowestZoom, hi = MaxZoom;
            float next = Zoom * factor;
            if (stretch)
            {
                // past the range each step counts for less
                if (next > hi && factor > 1)
                    next = Zoom * (1 + (factor - 1) * Math.Max(0, 1 - (Zoom / hi - 1) / (ZoomStretch - 1)));
                if (next < lo && factor < 1)
                    next = Zoom * (1 + (factor - 1) * Math.Max(0, 1 - (lo / Zoom - 1) / (ZoomStretch - 1)));
                next = Math.Max(lo / ZoomStretch, Math.Min(hi * ZoomStretch, next));
            }
            else
            {
                next = Math.Max(lo, Math.Min(hi, next));
            }
            SetZoomAbout(view, next);
            zoomFocus = view;
            velocity = new P2(0, 0);
        }

        void SetZoomAbout(P2 view, float next)
        {
            float k = next / Zoom;
            Position = new P2(view.X + (Position.X - view.X) * k, view.Y + (Position.Y - view.Y) * k);
            Zoom = next;
        }

        /// <summary>What a double tap at a view point asks for: closer; from close, back out. The point stays put.</summary>
        public void DoubleTapTarget(P2 view, out P2 position, out float zoom)
        {
            zoom = ClampZoom(Zoom < (StartZoom + CloseZoom) / 2 ? CloseZoom : StartZoom);
            float k = zoom / Zoom;
            position = ClampPosition(new P2(view.X + (Position.X - view.X) * k, view.Y + (Position.Y - view.Y) * k), zoom);
        }

        /// <summary>
        /// How fast the map should slide while a dragged tool is held near the view's edge (view units per second),
        /// so a swipe can go on past the screen. Zero in the middle.
        /// </summary>
        public P2 EdgePan(P2 view, float margin = 150, float speed = 900)
        {
            float l = -ViewW / 2 + InsetLeft, r = ViewW / 2 - InsetRight, b = -ViewH / 2 + InsetBottom, t = ViewH / 2 - InsetTop;
            float vx = view.X < l + margin ? Edge(l + margin - view.X, margin) : view.X > r - margin ? -Edge(view.X - (r - margin), margin) : 0;
            float vy = view.Y < b + margin ? Edge(b + margin - view.Y, margin) : view.Y > t - margin ? -Edge(view.Y - (t - margin), margin) : 0;
            return new P2(vx * speed, vy * speed);
        }

        static float Edge(float depth, float margin) => Math.Min(1, depth / margin);

        /// <summary>Slides the map by a view delta inside the hard limits (the edge pan of a dragged tool).</summary>
        public void Slide(P2 delta) => Position = ClampPosition(Position + delta, Zoom);

        // ------------------------------------------------------------------ frames

        /// <summary>
        /// A frame with no finger on the map: the glide slows down, a map pulled past an edge springs back, a zoom
        /// stretched past its range returns. False when everything is at rest.
        /// </summary>
        public bool Step(float dt)
        {
            if (!Moving || dt <= 0)
                return Moving;
            bool busy = false;

            float z = ClampZoom(Zoom);
            if (Math.Abs(z - Zoom) > 0.0005f)
            {
                float next = z + (Zoom - z) * (float)Math.Exp(-Spring * dt);
                if (Math.Abs(next - z) < 0.001f)
                    next = z;
                SetZoomAbout(zoomFocus, next);
                busy = true;
            }

            Bounds(Zoom, out float minX, out float maxX, out float minY, out float maxY);
            float px = Position.X, py = Position.Y, vx = velocity.X, vy = velocity.Y;
            busy |= Axis(ref px, ref vx, minX, maxX, dt);
            busy |= Axis(ref py, ref vy, minY, maxY, dt);
            Position = new P2(px, py);
            velocity = new P2(vx, vy);
            Moving = busy;
            return busy;
        }

        static bool Axis(ref float p, ref float v, float min, float max, float dt)
        {
            if (p > max || p < min)
            {
                // past the edge: no glide, a spring home
                float home = p > max ? max : min;
                v = 0;
                p = home + (p - home) * (float)Math.Exp(-Spring * dt);
                if (Math.Abs(p - home) < 0.5f)
                {
                    p = home;
                    return false;
                }
                return true;
            }
            if (Math.Abs(v) < StopSpeed)
            {
                v = 0;
                return false;
            }
            p += v * dt;
            v *= (float)Math.Exp(-Friction * dt);
            if (p > max || p < min)
            {
                // the glide ran into the edge: a short soft bump, then the spring
                float home = p > max ? max : min;
                p = home + Math.Max(-MaxOverscroll * 0.35f, Math.Min(MaxOverscroll * 0.35f, p - home));
                v = 0;
            }
            return true;
        }
    }

    /// <summary>The finger's speed at release: over the last tenth of a second, so a pause before lifting stops the glide.</summary>
    public sealed class VelocityTracker
    {
        const int N = 8;
        public const float Window = 0.1f;
        readonly float[] times = new float[N];
        readonly P2[] points = new P2[N];
        int count, head;

        public void Reset() => count = head = 0;

        public void Add(float time, P2 point)
        {
            times[head] = time;
            points[head] = point;
            head = (head + 1) % N;
            if (count < N)
                count++;
        }

        public P2 Velocity(float now)
        {
            if (count < 2)
                return new P2(0, 0);
            int last = (head - 1 + N) % N, first = last;
            for (int i = 1; i < count; i++)
            {
                int k = (head - 1 - i + 2 * N) % N;
                if (now - times[k] > Window)
                    break;
                first = k;
            }
            float dt = times[last] - times[first];
            // the finger rested before it lifted
            if (first == last || dt < 0.004f || now - times[last] > Window * 0.6f)
                return new P2(0, 0);
            return (points[last] - points[first]) * (1 / dt);
        }
    }
}
