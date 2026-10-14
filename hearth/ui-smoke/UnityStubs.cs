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

    public static class Time { public static float unscaledDeltaTime = 0.1f; }

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

    public static class Application { public static string persistentDataPath = Path.GetTempPath(); }

    public struct Vector2 { }

    public struct Vector3 { public Vector3(float x, float y, float z) { } }

    public struct Matrix4x4 { public static Matrix4x4 Scale(Vector3 v) => default; }

    public struct Rect { public Rect(float x, float y, float w, float h) { } }

    public static class Screen { public static int width = 1600, height = 900; }

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
        public static implicit operator GUIStyle(string s) => new GUIStyle();
    }

    public class GUISkin { public GUIStyle label = new GUIStyle(); }

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
