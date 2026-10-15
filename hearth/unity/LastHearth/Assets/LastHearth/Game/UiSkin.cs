using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// Dresses Unity's immediate-mode controls in the art of the rooms (Resources/Art/UI, cut from the
    /// kits' hand-painted textures by hearth/tools/art/ui.py): wooden plank buttons with iron studs, a
    /// gold-trimmed plank for the chosen tab, parchment boxes, wooden scroll bars, a dark stone wall
    /// behind everything. The pictures are nine-slice: corners keep their size, the middle stretches.
    /// </summary>
    public static class UiSkin
    {
        static readonly Color Cream = new Color(1f, 0.94f, 0.8f, 1f);
        static readonly Color Gold = new Color(1f, 0.84f, 0.42f, 1f);
        static readonly Color Ink = new Color(0.24f, 0.14f, 0.08f, 1f);
        static Texture2D background;

        /// <summary>Applies the look to GUI.skin; false when the art is missing (the plain look stays).</summary>
        public static bool Apply()
        {
            // a rounded bold face with Cyrillic (Nunito, SIL Open Font License), like the casual games' titles
            var font = UnityEngine.Resources.Load<Font>("Fonts/Nunito-ExtraBold");
            if (font != null)
                GUI.skin.font = font;
            var plank = ArtLibrary.Tex("UI/Button");
            if (plank == null)
                return false;
            var b = GUI.skin.button;
            Set(b.normal, plank, Cream);
            Set(b.focused, plank, Cream);
            Set(b.hover, ArtLibrary.Tex("UI/ButtonHover"), Cream);
            Set(b.active, ArtLibrary.Tex("UI/ButtonActive"), Cream);
            var on = ArtLibrary.Tex("UI/ButtonOn");
            Set(b.onNormal, on, Gold);
            Set(b.onHover, on, Gold);
            Set(b.onActive, on, Gold);
            Set(b.onFocused, on, Gold);
            b.border = new RectOffset(14, 14, 14, 14);
            b.fontStyle = FontStyle.Bold;

            var box = GUI.skin.box;
            var panel = ArtLibrary.Tex("UI/Panel");
            Set(box.normal, panel, Ink);
            box.border = new RectOffset(24, 24, 24, 24);
            box.padding = new RectOffset(18, 18, 16, 16);

            var field = GUI.skin.textField;
            var paper = ArtLibrary.Tex("UI/Field");
            Set(field.normal, paper, Ink);
            Set(field.hover, paper, Ink);
            Set(field.focused, paper, Ink);
            field.border = new RectOffset(10, 10, 10, 10);

            Set(GUI.skin.verticalScrollbar.normal, ArtLibrary.Tex("UI/ScrollTrack"), Cream);
            GUI.skin.verticalScrollbar.border = new RectOffset(6, 6, 8, 8);
            var thumb = ArtLibrary.Tex("UI/ScrollThumb");
            Set(GUI.skin.verticalScrollbarThumb.normal, thumb, Cream);
            Set(GUI.skin.verticalScrollbarThumb.hover, thumb, Cream);
            Set(GUI.skin.verticalScrollbarThumb.active, thumb, Cream);
            GUI.skin.verticalScrollbarThumb.border = new RectOffset(6, 6, 10, 10);

            background = ArtLibrary.Tex("UI/Background");
            if (background != null)
                background.wrapMode = TextureWrapMode.Repeat;
            return true;
        }

        /// <summary>Text with a dark outline, so it reads over any art (the plates, buttons and map).</summary>
        public static void Outlined(Rect r, string text, GUIStyle style, float width = 1.5f)
        {
            var old = GUI.color;
            GUI.color = new Color(0.12f, 0.07f, 0.03f, old.a * 0.9f);
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4;
                GUI.Label(new Rect(r.x + Mathf.Cos(a) * width, r.y + Mathf.Sin(a) * width, r.width, r.height), text, style);
            }
            GUI.color = old;
            GUI.Label(r, text, style);
        }

        static void Set(GUIStyleState s, Texture2D tex, Color text)
        {
            s.background = tex;
            s.textColor = text;
        }

        /// <summary>The stone wall behind the interface, tiled over the whole screen.</summary>
        public static void DrawBackground(Rect r)
        {
            if (background != null)
                GUI.DrawTextureWithTexCoords(r, background, new Rect(0, 0, r.width / 256f, r.height / 256f));
        }
    }
}
