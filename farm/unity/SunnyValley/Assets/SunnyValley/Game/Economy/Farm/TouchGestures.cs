using System;
using System.Collections.Generic;

namespace SunnyValley.Game.Farm
{
    public enum GestureKind
    {
        /// <summary>The first finger touched: what is under it may show itself pressed.</summary>
        Press,
        /// <summary>The press is over without a tap (it became a drag, a pinch, a long press, or was cancelled).</summary>
        PressEnd,
        Tap,
        DoubleTap,
        LongPress,
        PanStart,
        /// <summary>One finger moved: Delta is how far since the last event.</summary>
        Pan,
        /// <summary>The finger left: Delta is its speed, units per second.</summary>
        PanEnd,
        PinchStart,
        /// <summary>Two fingers: Point is between them, Delta is how far that point moved, Scale how the distance changed.</summary>
        Pinch,
        PinchEnd,
    }

    public struct Gesture
    {
        public GestureKind Kind;
        public P2 Point;
        public P2 Delta;
        public float Scale;

        public override string ToString() => Kind + " " + Point;
    }

    /// <summary>
    /// Fingers on the farm → what they mean (docs/FARM_UX.md §3). A touch is a tap until it moves further than the
    /// slop (a physical distance: 10 dp, so the same on any screen density) — then it is a drag and never a tap;
    /// the jump over the slop is not passed on, so the map does not jerk. Two taps close in time and place are a
    /// double tap. A finger held still is a long press. A second finger turns anything into a pinch; when one of
    /// two fingers leaves, the other goes on dragging without a jump and without a tap at the end.
    /// Plain C#: positions in any units (FarmWorld feeds view units and the slop converted to them), time in seconds.
    /// </summary>
    public sealed class TouchGestures
    {
        /// <summary>How far a finger may slip during a tap, dp (Android's touch slop is 8 dp; fingers on a game map are sloppier).</summary>
        public const float SlopDp = 10;
        public const float DoubleTapDp = 40;
        public const float DoubleTapSeconds = 0.3f;
        public const float LongPressSeconds = 0.45f;
        /// <summary>Two fingers closer than this do not zoom (the ratio of tiny distances is noise), dp.</summary>
        public const float MinPinchDp = 24;

        /// <summary>Units per dp: positions are multiplied by nothing, the thresholds by this.</summary>
        public float UnitsPerDp = 3;

        enum Mode { Idle, Candidate, Pan, Pinch }

        Mode mode;
        readonly List<int> ids = new List<int>();
        readonly Dictionary<int, P2> points = new Dictionary<int, P2>();
        P2 downAt;
        float downTime;
        bool pressShown, longFired, tapAllowed;
        P2 lastTapAt;
        float lastTapTime = -10;
        readonly VelocityTracker tracker = new VelocityTracker();
        readonly List<Gesture> events = new List<Gesture>();

        public int Fingers => ids.Count;
        public bool Dragging => mode == Mode.Pan || mode == Mode.Pinch;

        /// <summary>Forgets the last tap: the next one starts a new pair (the first tap opened something).</summary>
        public void ForgetTap() => lastTapTime = -10;

        float Slop => SlopDp * UnitsPerDp;

        IReadOnlyList<Gesture> Flush()
        {
            var r = events.ToArray();
            events.Clear();
            return r;
        }

        void Emit(GestureKind kind, P2 point, P2 delta = default(P2), float scale = 1) =>
            events.Add(new Gesture { Kind = kind, Point = point, Delta = delta, Scale = scale });

        void EndPress(P2 at)
        {
            if (pressShown)
                Emit(GestureKind.PressEnd, at);
            pressShown = false;
        }

        public IReadOnlyList<Gesture> Down(int id, P2 at, float time)
        {
            if (points.ContainsKey(id))
            {
                points[id] = at;
                return Flush();
            }
            points[id] = at;
            ids.Add(id);
            if (ids.Count == 1)
            {
                mode = Mode.Candidate;
                downAt = at;
                downTime = time;
                longFired = false;
                tapAllowed = true;
                pressShown = true;
                tracker.Reset();
                tracker.Add(time, at);
                Emit(GestureKind.Press, at);
            }
            else if (ids.Count == 2)
            {
                EndPress(at);
                if (mode == Mode.Pan)
                    Emit(GestureKind.PanEnd, points[ids[0]]);
                mode = Mode.Pinch;
                tapAllowed = false;
                Emit(GestureKind.PinchStart, Middle());
            }
            return Flush();
        }

        P2 Middle() => (points[ids[0]] + points[ids[1]]) * 0.5f;

        float Spread() => P2.Distance(points[ids[0]], points[ids[1]]);

        public IReadOnlyList<Gesture> Move(int id, P2 at, float time)
        {
            if (!points.TryGetValue(id, out var before))
                return Flush();
            switch (mode)
            {
                case Mode.Candidate:
                    points[id] = at;
                    if (P2.Distance(at, downAt) > Slop)
                    {
                        // a drag from here on: the slop's jump is swallowed
                        mode = Mode.Pan;
                        tapAllowed = false;
                        EndPress(at);
                        tracker.Reset();
                        tracker.Add(time, at);
                        Emit(GestureKind.PanStart, at);
                    }
                    break;
                case Mode.Pan:
                    points[id] = at;
                    if (id == ids[0])
                    {
                        tracker.Add(time, at);
                        Emit(GestureKind.Pan, at, at - before);
                    }
                    break;
                case Mode.Pinch:
                    if (id != ids[0] && id != ids[1])
                    {
                        points[id] = at;
                        break;
                    }
                    P2 m0 = Middle();
                    float d0 = Spread();
                    points[id] = at;
                    P2 m1 = Middle();
                    float d1 = Spread();
                    float min = MinPinchDp * UnitsPerDp;
                    float scale = d0 > min && d1 > min ? d1 / d0 : 1;
                    Emit(GestureKind.Pinch, m1, m1 - m0, scale);
                    break;
            }
            return Flush();
        }

        public IReadOnlyList<Gesture> Up(int id, P2 at, float time) => Lift(id, at, time, false);

        /// <summary>The system took the touch away (a call, the app paused): nothing is tapped.</summary>
        public IReadOnlyList<Gesture> Cancel(int id, float time) =>
            Lift(id, points.TryGetValue(id, out var p) ? p : default(P2), time, true);

        IReadOnlyList<Gesture> Lift(int id, P2 at, float time, bool cancelled)
        {
            if (!points.ContainsKey(id))
                return Flush();
            points[id] = at;
            int index = ids.IndexOf(id);
            switch (mode)
            {
                case Mode.Candidate:
                    Remove(id);
                    mode = Mode.Idle;
                    if (cancelled || !tapAllowed || longFired)
                    {
                        EndPress(at);
                        break;
                    }
                    pressShown = false;
                    if (time - lastTapTime <= DoubleTapSeconds && P2.Distance(at, lastTapAt) <= DoubleTapDp * UnitsPerDp)
                    {
                        lastTapTime = -10;
                        Emit(GestureKind.DoubleTap, at);
                    }
                    else
                    {
                        lastTapTime = time;
                        lastTapAt = at;
                        Emit(GestureKind.Tap, at);
                    }
                    break;
                case Mode.Pan:
                    Remove(id);
                    if (index == 0)
                    {
                        tracker.Add(time, at);
                        mode = Mode.Idle;
                        Emit(GestureKind.PanEnd, at, cancelled ? default(P2) : tracker.Velocity(time));
                    }
                    break;
                case Mode.Pinch:
                    bool pinching = index == 0 || index == 1;
                    Remove(id);
                    if (!pinching)
                        break;
                    if (ids.Count >= 2)
                        break; // a third finger takes the place of the one that left
                    Emit(GestureKind.PinchEnd, at);
                    if (ids.Count == 1)
                    {
                        // the finger that stays drags on, from where it is
                        mode = Mode.Pan;
                        tracker.Reset();
                        tracker.Add(time, points[ids[0]]);
                        Emit(GestureKind.PanStart, points[ids[0]]);
                    }
                    else
                    {
                        mode = Mode.Idle;
                    }
                    break;
                default:
                    Remove(id);
                    break;
            }
            if (ids.Count == 0)
                mode = Mode.Idle;
            return Flush();
        }

        void Remove(int id)
        {
            ids.Remove(id);
            points.Remove(id);
        }

        /// <summary>Every frame: a finger held still long enough is a long press (and no tap follows).</summary>
        public IReadOnlyList<Gesture> Tick(float time)
        {
            if (mode == Mode.Candidate && !longFired && time - downTime >= LongPressSeconds)
            {
                longFired = true;
                tapAllowed = false;
                EndPress(downAt);
                Emit(GestureKind.LongPress, downAt);
            }
            return Flush();
        }

        /// <summary>Drops every finger without events (the screen was rebuilt under them).</summary>
        public void Reset()
        {
            ids.Clear();
            points.Clear();
            events.Clear();
            mode = Mode.Idle;
            pressShown = false;
        }
    }
}
