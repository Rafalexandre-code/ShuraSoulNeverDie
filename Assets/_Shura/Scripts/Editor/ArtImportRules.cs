using UnityEditor;

namespace Shura.EditorTools
{
    /// <summary>
    /// Import rules for the pixel art and audio in Resources: crisp point-filtered pixels,
    /// no compression artefacts, readable so silhouettes and rotated weapons can be built at
    /// runtime; sound effects mono and decompressed on load (no first-play hitch).
    /// </summary>
    public sealed class ArtImportRules : AssetPostprocessor
    {
        const uint Version = 2;

        public override uint GetVersion() => Version;

        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Art/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.filterMode = UnityEngine.FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/Resources/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            bool music = assetPath.Contains("/Audio/Music/");
            importer.forceToMono = !music;
            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            s.loadType = UnityEngine.AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = UnityEngine.AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.7f : 0.8f;
            importer.defaultSampleSettings = s;
            importer.loadInBackground = false;
        }
    }
}
