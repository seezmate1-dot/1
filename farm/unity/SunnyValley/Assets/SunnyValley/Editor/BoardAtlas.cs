using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace SunnyValley.EditorTools
{
    /// <summary>
    /// Before every player build: packs the board's painted pictures (Resources/Art: Tiles, Specials, Obstacles,
    /// Boosters, Currency — not the farm's large chapter pictures) into one sprite atlas, so the board and the HUD draw in a few batches
    /// instead of one per picture. Sprites keep loading by path (Resources.Load); the build swaps in the atlas.
    /// A packing problem only warns: the game works without the atlas, just with more draw calls.
    /// NOT run before builds for now: packing crashed the Unity 6.3 editor in batch mode on CI (SIGFPE in
    /// GetRowSize inside PackSpriteAtlases), which no try/catch can stop — only from the menu.
    /// </summary>
    public sealed class BoardAtlas
    {
        const string AtlasPath = "Assets/SunnyValley/Art.spriteatlas";
        static readonly string[] Folders =
        {
            "Assets/SunnyValley/Resources/Art/Tiles", "Assets/SunnyValley/Resources/Art/Specials", "Assets/SunnyValley/Resources/Art/Obstacles",
            "Assets/SunnyValley/Resources/Art/Boosters", "Assets/SunnyValley/Resources/Art/Currency",
        };

        [MenuItem("Солнечная долина/Собрать атлас картинок")]
        public static void FromMenu() => Ensure(EditorUserBuildSettings.activeBuildTarget);

        static void Ensure(BuildTarget target)
        {
            // the classic atlases, packed for builds only (the editor keeps the single pictures)
            if (EditorSettings.spritePackerMode != SpritePackerMode.BuildTimeOnlyAtlas && EditorSettings.spritePackerMode != SpritePackerMode.AlwaysOnAtlas)
                EditorSettings.spritePackerMode = SpritePackerMode.BuildTimeOnlyAtlas;
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
            if (atlas == null)
            {
                var folders = new System.Collections.Generic.List<UnityEngine.Object>();
                foreach (string path in Folders)
                {
                    var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (folder != null)
                        folders.Add(folder);
                }
                if (folders.Count == 0)
                    return;
                atlas = new SpriteAtlas();
                atlas.SetPackingSettings(new SpriteAtlasPackingSettings
                {
                    // uGUI images draw the sprite's whole rectangle: no tight packing, no rotation
                    enableRotation = false, enableTightPacking = false, padding = 4, blockOffset = 1,
                });
                atlas.SetTextureSettings(new SpriteAtlasTextureSettings
                {
                    readable = false, generateMipMaps = false, sRGB = true, filterMode = FilterMode.Bilinear,
                });
                atlas.SetIncludeInBuild(true);
                atlas.Add(folders.ToArray());
                AssetDatabase.CreateAsset(atlas, AtlasPath);
                AssetDatabase.SaveAssets();
            }
            SpriteAtlasUtility.PackAtlases(new[] { atlas }, target);
        }
    }
}
