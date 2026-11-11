using UnityEditor;

namespace SunnyValley.EditorTools
{
    /// <summary>
    /// Imports the painted pictures (Assets/SunnyValley/Resources/Art, and the HUD icons in Resources/Icons) as UI
    /// sprites: no mipmaps, clean transparency, at most 512 pixels — so a fresh checkout without .meta files builds
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
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            // the chapter 1 farm: zone backgrounds are 1600×1200 opaque pictures, buildings up to 1024
            importer.maxTextureSize = assetPath.StartsWith(Root + "Ch1/Backgrounds/") ? 2048 : assetPath.StartsWith(Root + "Ch1/") ? 1024 : 512;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
