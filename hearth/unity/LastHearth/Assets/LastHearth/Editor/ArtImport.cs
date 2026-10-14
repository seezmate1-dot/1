using UnityEditor;

namespace LastHearth.EditorTools
{
    /// <summary>
    /// Import settings of the rendered art (Resources/Art): no mip maps (frames of a sprite sheet would
    /// bleed into each other), transparent edges kept clean, high quality compression, and sounds that
    /// stream the music instead of holding it in memory.
    /// </summary>
    public sealed class ArtImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Art/") && !assetPath.EndsWith("/Icon.png"))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.npotScale = TextureImporterNPOTScale.None;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/Resources/Audio/"))
                return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            bool music = assetPath.EndsWith("Hearth.ogg") || assetPath.EndsWith("Battle.ogg") || assetPath.EndsWith("Fire.ogg");
            settings.loadType = music ? UnityEngine.AudioClipLoadType.Streaming : UnityEngine.AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = UnityEngine.AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
        }
    }
}
