using UnityEditor;

namespace SunnyValley.EditorTools
{
    /// <summary>
    /// Imports the painted pictures (Assets/SunnyValley/Resources/Art, and the HUD icons in Resources/Icons) as UI
    /// sprites: no mipmaps, clean transparency, at most 512 pixels (the farm's map art up to 1024) — so a fresh checkout without .meta files builds
    /// the same everywhere.
    /// </summary>
    public sealed class ArtImport : AssetPostprocessor
    {
        const string Root = "Assets/SunnyValley/Resources/Art/";
        const string Icons = "Assets/SunnyValley/Resources/Icons/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root) && !assetPath.StartsWith(Icons))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            // the farm's map art is drawn large and shown small at most zooms: mip levels keep it from shimmering
            importer.mipmapEnabled = assetPath.StartsWith(Root + "Farm/") || assetPath.StartsWith(Root + "Ch1/Backgrounds/");
            importer.alphaIsTransparency = true;
            // the chapter 1 farm: zone backgrounds are 3200×2400 opaque pictures (two pixels per map unit, sharp on a
            // phone's dense screen), buildings up to 1024
            // the working farm's things are drawn at 3–4 pixels per map unit, up to 1024 on the long side
            importer.maxTextureSize = assetPath.StartsWith(Root + "Ch1/Backgrounds/") ? 4096
                : assetPath.StartsWith(Root + "Ch1/") || assetPath.StartsWith(Root + "Farm/") ? 1024 : 512;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
