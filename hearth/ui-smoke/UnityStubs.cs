using System.Collections.Generic;
using System;
using System.IO;

// Just enough of UnityEngine to run the IMGUI debug screen outside Unity. Buttons are "clicked" at
// random by UiSmoke.Clicks, so every handler of the screen gets exercised.
namespace UnityEngine
{
    public class Object
    {
        public static T FindAnyObjectByType<T>() where T : Object => null;
        public static void DontDestroyOnLoad(Object o) { }
    }

    public class Component : Object
    {
        public GameObject gameObject => GameObject.Host;
    }

    public class AudioClip : Object { public string Name = ""; }

    public class AudioSource : Component
    {
        public static int Played;
        public bool loop, playOnAwake;
        public float volume = 1;
        public AudioClip clip;
        public void Play() { if (clip != null) Played++; }
        public void PlayOneShot(AudioClip c, float v) { if (c != null) Played++; }
    }

    public class MonoBehaviour : Component { }

    public class GameObject : Object
    {
        public static readonly GameObject Host = new GameObject("host");
        public GameObject(string n) { }
        public T AddComponent<T>() where T : Component => (T)Activator.CreateInstance(typeof(T));
    }

    public enum RuntimeInitializeLoadType { AfterSceneLoad }

    [AttributeUsage(AttributeTargets.Method)]
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { }
    }

    public static class Time
    {
        public static float unscaledDeltaTime = 0.1f;
        public static float realtimeSinceStartup;
        public static float time;
    }

    public enum KeyCode { Escape }

    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }

    public struct Touch
    {
        public TouchPhase phase;
        public Vector2 deltaPosition;
    }

    /// <summary>A scripted finger: UiSmoke feeds touches and the back button.</summary>
    public static class Input
    {
        public static Touch[] Touches = new Touch[0];
        public static bool Escape;
        public static int touchCount => Touches.Length;
        public static Touch GetTouch(int i) => Touches[i];
        public static bool GetKeyDown(KeyCode k) => Escape;
        public static bool GetMouseButton(int button) => false;
    }

    public static class SleepTimeout
    {
        public const int NeverSleep = -1;
        public const int SystemSetting = -2;
    }

    public static class PlayerPrefs
    {
        public static string GetString(string k, string d) => d;
        public static void SetString(string k, string v) { }
        public static int GetInt(string k, int d) => d;
        public static void SetInt(string k, int v) { }
        public static void Save() { }
    }

    public class TextAsset : Object { public string text = ""; }

    public class Font : Object { public string Name = ""; }

    public class Texture : Object { }

    public enum TextureWrapMode { Repeat, Clamp }

    public enum FontStyle { Normal, Bold }

    public enum TextAnchor { UpperLeft, MiddleLeft, MiddleCenter }

    public class Texture2D : Texture
    {
        public string Name = "";
        public TextureWrapMode wrapMode;
        public int width = 1, height = 1;
        public static Texture2D whiteTexture = new Texture2D { Name = "white" };
    }

    public static class Resources
    {
        public static string Folder = "";
        public static int TexturesLoaded;
        /// <summary>Pictures still being painted ("Art/Scene/Pier"): while missing they load as 512 x 512
        /// placeholders, so the layout made for them can be run before they exist. Any other must exist.</summary>
        public static readonly HashSet<string> Placeholders = new HashSet<string>();
        public static int PlaceholdersLoaded;

        public static T Load<T>(string p) where T : Object
        {
            object asset;
            if (typeof(T) == typeof(AudioClip))
            {
                string ogg = Path.Combine(Folder, p + ".ogg");
                if (!File.Exists(ogg))
                    throw new FileNotFoundException("missing sound", ogg);
                asset = new AudioClip { Name = p };
            }
            else if (typeof(T) == typeof(Texture2D))
            {
                // the art must exist: a typo in a name would show an empty room on the phone
                string png = Path.Combine(Folder, p + ".png");
                if (!File.Exists(png) && Placeholders.Contains(p))
                {
                    PlaceholdersLoaded++;
                    return (T)(object)new Texture2D { Name = p, width = 512, height = 512 };
                }
                if (!File.Exists(png))
                    throw new FileNotFoundException("missing art", png);
                TexturesLoaded++;
                // the size from the PNG header (IHDR: width and height, big-endian, at bytes 16..23)
                var head = new byte[24];
                using (var f = File.OpenRead(png))
                    f.Read(head, 0, 24);
                int Be(int i) => head[i] << 24 | head[i + 1] << 16 | head[i + 2] << 8 | head[i + 3];
                asset = new Texture2D { Name = p, width = Be(16), height = Be(20) };
            }
            else if (typeof(T) == typeof(Font))
            {
                string ttf = Path.Combine(Folder, p + ".ttf");
                if (!File.Exists(ttf))
                    throw new FileNotFoundException("missing font", ttf);
                asset = new Font { Name = p };
            }
            else
            {
                asset = new TextAsset { text = File.ReadAllText(Path.Combine(Folder, p + ".json")) };
            }
            return (T)asset;
        }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1, 1);
        public static Color black => new Color(0, 0, 0, 1);
    }

    public static class Application
    {
        public static string persistentDataPath = Path.GetTempPath();
        public static int targetFrameRate;
        public static int Quits;
        public static void Quit() => Quits++;
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
    }

    public struct Vector3 { public Vector3(float x, float y, float z) { } }

    public struct Matrix4x4 { public static Matrix4x4 Scale(Vector3 v) => default; }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public float yMax => y + height;
        public float xMax => x + width;
        public Vector2 center => new Vector2(x + width / 2, y + height / 2);
        public bool Contains(Vector2 p) => p.x >= x && p.x < x + width && p.y >= y && p.y < y + height;
    }

    public class RectOffset { public RectOffset(int l, int r, int t, int b) { } }

    /// <summary>A landscape phone: 2400 x 1080 at 420 dpi with a camera cut-out on the left.</summary>
    public static class Screen
    {
        public static int width = 2400, height = 1080;
        public static float dpi = 420;
        public static Rect safeArea = new Rect(90, 0, 2310, 1080);
        public static int sleepTimeout;
    }

    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a, b);
        public static int Clamp(int v, int a, int b) => Math.Max(a, Math.Min(b, v));
        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
        public const float PI = (float)Math.PI;
        public static float Abs(float f) => Math.Abs(f);
        public static float Sign(float f) => f >= 0 ? 1 : -1;
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Clamp(float v, float a, float b) => Math.Max(a, Math.Min(b, v));
        public static float Max(float a, float b, float c) => Math.Max(a, Math.Max(b, c));
        public static float Pow(float f, float p) => (float)Math.Pow(f, p);
        public static int RoundToInt(float f) => (int)Math.Round(f);
    }

    public class GUIStyleState
    {
        public Texture2D background;
        public Color textColor;
    }

    public class GUIStyle
    {
        public GUIStyle() { }
        public GUIStyle(GUIStyle o) { }
        public GUIStyleState normal = new GUIStyleState(), hover = new GUIStyleState(), active = new GUIStyleState(),
            focused = new GUIStyleState(), onNormal = new GUIStyleState(), onHover = new GUIStyleState(),
            onActive = new GUIStyleState(), onFocused = new GUIStyleState();
        public RectOffset border;
        public FontStyle fontStyle;
        public TextAnchor alignment;
        public bool richText;
        public bool wordWrap;
        public int fontSize;
        public Font font;
        public float fixedWidth;
        public RectOffset padding;
        public static implicit operator GUIStyle(string s) => new GUIStyle();
        public static GUIStyle none = new GUIStyle();
    }

    public class GUISkin
    {
        public Font font;
        public GUIStyle label = new GUIStyle();
        public GUIStyle button = new GUIStyle();
        public GUIStyle box = new GUIStyle();
        public GUIStyle textField = new GUIStyle();
        public GUIStyle verticalScrollbar = new GUIStyle();
        public GUIStyle verticalScrollbarThumb = new GUIStyle();
    }

    public enum EventType { MouseDown, MouseUp, MouseDrag, Repaint, Layout, Used }

    public class Event
    {
        public static Event current = new Event();
        public EventType type = EventType.Repaint;
        public Vector2 mousePosition = new Vector2(400, 300);
        public void Use() => type = EventType.Used;
    }

    public static class GUIUtility
    {
        public static int hotControl;
        public static string systemCopyBuffer = "";
        // the stub's rectangles are already on the screen (no clipping groups): the points stay
        public static Vector2 GUIToScreenPoint(Vector2 p) => p;
        public static Vector2 ScreenToGUIPoint(Vector2 p) => p;
        public static void ScaleAroundPivot(Vector2 scale, Vector2 pivot)
        {
            if (!(scale.x > 0) || !(scale.y > 0))
                throw new InvalidOperationException("scaled to nothing");
        }
    }

    public static class GUI
    {
        public static Matrix4x4 matrix;
        public static bool enabled = true;
        public static GUISkin skin = new GUISkin();
        public static Color color = Color.white;
        public static int Textures;

        /// <summary>When set, every texture and cell label is written down (UiSmoke renders a preview).</summary>
        public static System.Collections.Generic.List<string> Record;

        public static void DrawTexture(Rect r, Texture t)
        {
            Record?.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "T|{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}",
                ((Texture2D)t).Name, r.x, r.y, r.width, r.height, color.r, color.g, color.b, color.a));
            if (t == null)
                throw new ArgumentNullException(nameof(t));
            if (r.width <= 0 || r.height <= 0)
                throw new InvalidOperationException("texture drawn into an empty rectangle");
            Textures++;
        }

        public static bool Button(Rect r, string t, GUIStyle s) => enabled && GUILayout.Click("[ячейка]" + t);

        /// <summary>A box in a style: the frames drawn as nine slices (the stub draws the background stretched).</summary>
        public static void Box(Rect r, string t, GUIStyle s)
        {
            if (s.normal.background == null)
                throw new InvalidOperationException("a box without a picture");
            DrawTexture(r, s.normal.background);
        }

        public static void DrawTextureWithTexCoords(Rect r, Texture t, Rect uv)
        {
            if (t == null)
                throw new ArgumentNullException(nameof(t));
            bool tiled = ((Texture2D)t).wrapMode == TextureWrapMode.Repeat;
            if (uv.width == 0 || uv.height <= 0 || (!tiled && (uv.y < -0.001f || uv.y + uv.height > 1.001f)))
                throw new InvalidOperationException("bad texture coordinates " + uv.x + "," + uv.y + "," + uv.width + "," + uv.height);
            Record?.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "S|{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}",
                ((Texture2D)t).Name, r.x, r.y, r.width, r.height, uv.x, uv.y, uv.width, uv.height, color.a));
            Textures++;
        }
        public static void Label(Rect r, string t, GUIStyle s)
        {
            if (t == null)
                throw new InvalidOperationException("null text");
            // where and how the text sits: the preview centres it like Unity does
            Record?.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "W|{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}",
                r.x, r.y, r.width, r.height, s?.alignment.ToString() ?? "UpperLeft", s != null && s.fontSize > 0 ? s.fontSize : 14,
                color.r, color.g, color.b, color.a, t.Replace("\n", "\\n")));
        }
    }

    public static class GUILayoutUtility
    {
        public static Rect GetRect(float w, float h, params GUILayoutOption[] o)
        {
            if (w <= 0 || h <= 0)
                throw new InvalidOperationException("empty layout rectangle");
            // a simple vertical layout, like Unity's: inside the area, under what came before, moved by the scroll
            var r = new Rect(GUILayout.X, GUILayout.Y - GUILayout.ScrollY, w, h);
            GUILayout.Y += h;
            return r;
        }

        /// <summary>The stub does not lay out buttons: a button-sized place under what came before.</summary>
        public static Rect GetLastRect() => new Rect(GUILayout.X, GUILayout.Y - GUILayout.ScrollY, 220, 34);
    }

    public class GUILayoutOption { }

    public static class GUILayout
    {
        public static Func<string, bool> Click = _ => false;
        public static int Depth;

        // where the next layout rectangle goes (only vertical stacking is modelled)
        public static float X, Y, ScrollY;
        static readonly Stack<(float x, float y, float scroll)> saved = new Stack<(float, float, float)>();

        public static void BeginArea(Rect r)
        {
            Depth++;
            saved.Push((X, Y, ScrollY));
            X = r.x;
            Y = r.y;
            ScrollY = 0;
        }

        public static void EndArea()
        {
            Depth--;
            (X, Y, ScrollY) = saved.Pop();
        }

        public static Vector2 BeginScrollView(Vector2 v, params GUILayoutOption[] o) { Depth++; saved.Push((X, Y, ScrollY)); ScrollY += v.y; return v; }
        public static Vector2 BeginScrollView(Vector2 v, GUIStyle h, GUIStyle w, params GUILayoutOption[] o) => BeginScrollView(v);

        public static void EndScrollView()
        {
            Depth--;
            (X, Y, ScrollY) = saved.Pop();
        }
        public static void Label(string t, params GUILayoutOption[] o) => Check(t);
        public static void Label(string t, GUIStyle s, params GUILayoutOption[] o) => Check(t);
        public static bool Button(string t, params GUILayoutOption[] o) { Check(t); return GUI.enabled && Click(t); }
        public static bool Toggle(bool v, string t, GUIStyle s, params GUILayoutOption[] o) { Check(t); return GUI.enabled && Click(t) ? !v : v; }
        public static string TextArea(string t, params GUILayoutOption[] o) { Check(t); return t; }
        public static void BeginHorizontal(params GUILayoutOption[] o) => Depth++;
        public static void EndHorizontal() => Depth--;
        public static void BeginVertical(params GUILayoutOption[] o) => Depth++;
        public static void EndVertical() => Depth--;
        public static void Space(float p) => Y += p;
        public static void FlexibleSpace() { }
        public static GUILayoutOption Width(float w) => null;
        public static GUILayoutOption Height(float h) => null;
        public static int Toolbar(int selected, string[] texts, params GUILayoutOption[] o) => selected;

        static void Check(string t)
        {
            if (t == null)
                throw new InvalidOperationException("null text on the screen");
        }
    }
}
