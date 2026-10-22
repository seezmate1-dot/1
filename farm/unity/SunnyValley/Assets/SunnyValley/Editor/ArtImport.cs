using UnityEditor;

namespace SunnyValley.EditorTools
{
    /// <summary>
    /// Imports the painted pictures (Assets/SunnyValley/Resources/Art) as UI sprites: no mipmaps, clean transparency,
    /// at most 512 pixels — so a fresh checkout without .meta files builds the same everywhere.
    /// </summary>
    public sealed class ArtImport : AssetPostprocessor
    {
        const string Root = "Assets/SunnyValley/Resources/Art/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 512;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
