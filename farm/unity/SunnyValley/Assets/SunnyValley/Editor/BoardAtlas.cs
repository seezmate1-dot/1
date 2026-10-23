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
    /// Before every player build: packs the painted pictures (Resources/Art — the board's tiles, specials and
    /// obstacles, the boosters and currencies) into one sprite atlas, so the board and the HUD draw in a few batches
    /// instead of one per picture. Sprites keep loading by path (Resources.Load); the build swaps in the atlas.
    /// A packing problem only warns: the game works without the atlas, just with more draw calls.
    /// </summary>
    public sealed class BoardAtlas : IPreprocessBuildWithReport
    {
        const string AtlasPath = "Assets/SunnyValley/Art.spriteatlas";
        const string ArtFolder = "Assets/SunnyValley/Resources/Art";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            try
            {
                Ensure(report.summary.platform);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Атлас картинок не собран (игра работает и без него): " + e.Message);
            }
        }

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
                var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ArtFolder);
                if (folder == null)
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
                atlas.Add(new[] { folder });
                AssetDatabase.CreateAsset(atlas, AtlasPath);
                AssetDatabase.SaveAssets();
            }
            SpriteAtlasUtility.PackAtlases(new[] { atlas }, target);
        }
    }
}
