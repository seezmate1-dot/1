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
        public GameObject gameObject => null;
    }

    public class MonoBehaviour : Component { }

    public class GameObject : Object
    {
        public GameObject(string n) { }
        public T AddComponent<T>() where T : Component => null;
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
        public static void Save() { }
    }

    public class TextAsset : Object { public string text = ""; }

    public static class Resources
    {
        public static string Folder = "";

        public static T Load<T>(string p) where T : Object
        {
            object asset = new TextAsset { text = File.ReadAllText(Path.Combine(Folder, p + ".json")) };
            return (T)asset;
        }
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
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
    }

    public struct Vector3 { public Vector3(float x, float y, float z) { } }

    public struct Matrix4x4 { public static Matrix4x4 Scale(Vector3 v) => default; }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public float yMax => y + height;
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
    }

    public class GUIStyle
    {
        public GUIStyle() { }
        public GUIStyle(GUIStyle o) { }
        public bool richText;
        public bool wordWrap;
        public int fontSize;
        public float fixedWidth;
        public RectOffset padding;
        public static implicit operator GUIStyle(string s) => new GUIStyle();
    }

    public class GUISkin
    {
        public GUIStyle label = new GUIStyle();
        public GUIStyle button = new GUIStyle();
        public GUIStyle verticalScrollbar = new GUIStyle();
        public GUIStyle verticalScrollbarThumb = new GUIStyle();
    }

    public enum EventType { MouseDown, MouseUp, MouseDrag, Repaint, Layout, Used }

    public class Event
    {
        public static Event current = new Event();
        public EventType type = EventType.Repaint;
        public void Use() => type = EventType.Used;
    }

    public static class GUIUtility { public static int hotControl; }

    public static class GUI
    {
        public static Matrix4x4 matrix;
        public static bool enabled = true;
        public static GUISkin skin = new GUISkin();
    }

    public class GUILayoutOption { }

    public static class GUILayout
    {
        public static Func<string, bool> Click = _ => false;
        public static int Depth;

        public static void BeginArea(Rect r) => Depth++;
        public static void EndArea() => Depth--;
        public static Vector2 BeginScrollView(Vector2 v, params GUILayoutOption[] o) { Depth++; return v; }
        public static void EndScrollView() => Depth--;
        public static void Label(string t, params GUILayoutOption[] o) => Check(t);
        public static void Label(string t, GUIStyle s, params GUILayoutOption[] o) => Check(t);
        public static bool Button(string t, params GUILayoutOption[] o) { Check(t); return GUI.enabled && Click(t); }
        public static bool Toggle(bool v, string t, GUIStyle s, params GUILayoutOption[] o) { Check(t); return GUI.enabled && Click(t) ? !v : v; }
        public static void BeginHorizontal(params GUILayoutOption[] o) => Depth++;
        public static void EndHorizontal() => Depth--;
        public static void BeginVertical(params GUILayoutOption[] o) => Depth++;
        public static void EndVertical() => Depth--;
        public static void Space(float p) { }
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
