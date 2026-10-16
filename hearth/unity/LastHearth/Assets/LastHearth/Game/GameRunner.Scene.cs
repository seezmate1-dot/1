using System.Linq;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// The surface around the fortress, like Hustle Castle's: everything on the scene, not on buttons.
    /// On the ground left and right of the mountain gate stand the places that open their windows: the
    /// pier with a boat (the world map), the quest board, the merchant's tent (the shop), the pet pen,
    /// the small arena and the magic portal (the events). They take over the round buttons along the
    /// right edge once they are painted (Art.json "scene": Scene/Pier, Scene/Board, Scene/Merchant,
    /// Scene/Pen, Scene/Arena, Scene/Portal; 512 x 512, transparent, the object on the bottom edge);
    /// until then nothing here is drawn or loaded. Each place breathes a little, sinks under the finger,
    /// carries its name on a plate under it and the red dot of the button it replaces, and hops while
    /// something waits there.
    /// </summary>
    public sealed partial class GameRunner
    {
        // Mountain.png: the surface ground runs at 0.755 of its height, the gate stands on 0.40..0.60 of its width
        const float SceneGround = 0.755f, SceneGateLeft = 0.40f, SceneGateRight = 0.60f;

        // the top of the mountain's sky lies above the view (under the resource bar), so on a phone the places
        // and their names fit between the bars at the top of the scroll; the cutaway starts at 0.8 of the picture
        const float MountainSky = 0.12f, MountainShown = 0.8f - MountainSky;

        /// <summary>The places from the left edge of the mountain to the gate, then from the gate to the right edge.</summary>
        static readonly (string art, string name, Win win)[] ScenePlaces =
        {
            ("Pier", "Карта", Win.None), ("Board", "Задания", Win.Quests), ("Merchant", "Лавка", Win.Shop),
            ("Pen", "Питомцы", Win.Pets), ("Arena", "Арена", Win.Arena), ("Portal", "События", Win.Events),
        };

        /// <summary>The lots of the places: three on each side of the gate; the right side ends short of the
        /// buttons along the right edge («Награды» stays there), so nothing stands under them.</summary>
        (float left, float right) SceneLots(float mountainW)
        {
            float rightEnd = Mathf.Min(mountainW, viewWidth - HudRight - 4);
            return (Mathf.Max(20, mountainW * SceneGateLeft / 3), Mathf.Max(20, (rightEnd - mountainW * SceneGateRight) / 3));
        }

        /// <summary>How big a place's picture is: a little wider than the narrower lot (the pictures have
        /// transparent margins), at most 0.3 of the mountain's height, so the peak stays the backdrop.</summary>
        float SceneSize(float mountainW)
        {
            var (left, right) = SceneLots(mountainW);
            return Mathf.Min(Mathf.Min(left, right) * 1.15f, mountainW * 0.5f * 0.3f);
        }

        /// <summary>Whether the window a place opens has something waiting: the same red dot as its old button.</summary>
        bool SceneWaits(Win win, long now)
        {
            switch (win)
            {
                case Win.None: return MissionWaits();
                case Win.Quests: return QuestsWait(now);
                case Win.Pets: return KennelWaits();
                case Win.Arena: return PlaceWaits("Arena", now);
                case Win.Events: return EventsWait(now);
                default: return false;
            }
        }

        /// <summary>
        /// The places on the ground of the mountain's picture: each object stands on the grass of its lot,
        /// its name on a plate under its foot, kept above the first floor of rooms (gridY). The picture
        /// overlaps its neighbours' transparent margins only; the taps (the object and the plate) never overlap.
        /// </summary>
        void DrawScene(Rect mountain, float gridY, long now)
        {
            var (leftLot, rightLot) = SceneLots(mountain.width);
            float size = SceneSize(mountain.width);
            float feet = mountain.y + mountain.height * SceneGround;
            float t = Time.time;
            int n = ScenePlaces.Length;
            var pictures = new Rect[n];
            var plates = new Rect[n];
            var hits = new Rect[n];
            var dots = new bool[n];
            for (int i = 0; i < n; i++)
            {
                var (art, name, win) = ScenePlaces[i];
                float lot = i < 3 ? leftLot : rightLot;
                float cx = i < 3 ? mountain.x + lot * (i + 0.5f) : mountain.x + mountain.width * SceneGateRight + lot * (i - 3 + 0.5f);
                // a narrow lot (a narrow screen) takes the buttons' smaller names
                float ph = lot < 90 ? 20 : 22;
                float pw = Mathf.Min(name.Length * (lot < 90 ? 7.2f : 8.5f) + 26, lot - 6);
                plates[i] = new Rect(cx - pw / 2, Mathf.Min(feet - ph * 0.4f, gridY - ph - 2), pw, ph);
                float hw = Mathf.Min(size * 0.8f, lot - 4);
                hits[i] = new Rect(cx - hw / 2, feet - size * 0.85f, hw, plates[i].yMax - (feet - size * 0.85f));
                dots[i] = SceneWaits(win, now);
                // idle: the object breathes, slower than the buttons and out of step with its neighbours;
                // it hops now and then while something waits there
                float breathe = 0.014f * Mathf.Sin(t * 1.7f + i * 1.3f);
                float hop = dots[i] ? size * 0.035f * Mathf.Max(0, Mathf.Sin(t * 3.2f + i)) : 0;
                // pressed: sinks under the finger (Pressable) toward its foot
                float press = blockCutaway ? 1 : Pressable("scene" + art, hits[i]).width / hits[i].width;
                float w = size * (1 - breathe * 0.5f) * press, h = size * (1 + breathe) * press;
                pictures[i] = new Rect(cx - w / 2, feet + size * 0.03f - h - hop, w, h);
            }
            for (int i = 0; i < n; i++)
                ArtLibrary.Draw(pictures[i], "Scene/" + ScenePlaces[i].art);
            for (int i = 0; i < n; i++)
            {
                var p = plates[i];
                ArtLibrary.DrawSliced(p, "UI/Pill", 30);
                // a pixel up: the bold face sits low in its line, so the name looks centred on the plate
                UiSkin.Outlined(new Rect(p.x, p.y - 1, p.width, p.height), ScenePlaces[i].name, p.height < 22 ? buttonLabel : BadgeStyle());
                if (dots[i])
                    ArtLibrary.Draw(new Rect(p.xMax - 12, p.y - 9, 20, 20), "UI/Dot");
            }
            for (int i = 0; i < n; i++)
            {
                if (!Tap(hits[i]))
                    continue;
                sounds?.Play("Click", 0.4f);
                var win = ScenePlaces[i].win;
                if (win == Win.None)
                {
                    OpenMap(true); // the boat sails to the islands
                }
                else
                {
                    selectedRoom = -1;
                    selectedFloor = -1;
                    Open(win);
                }
            }
        }
    }
}
