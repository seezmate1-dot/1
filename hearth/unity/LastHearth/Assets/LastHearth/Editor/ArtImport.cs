using UnityEditor;

namespace LastHearth.EditorTools
{
    /// <summary>
    /// Import settings of the rendered art (Resources/Art): mip maps with trilinear filtering, so art
    /// drawn smaller than it was rendered (icons, buttons, characters, rooms on a phone) stays smooth
    /// instead of jagged (the frames of a sprite sheet have wide empty margins, so they do not bleed);
    /// transparent edges kept clean, high quality compression, the map in full size; and sounds that
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
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.alphaIsTransparency = true;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.filterMode = UnityEngine.FilterMode.Trilinear;
            importer.anisoLevel = 2;
            importer.maxTextureSize = 4096;
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
