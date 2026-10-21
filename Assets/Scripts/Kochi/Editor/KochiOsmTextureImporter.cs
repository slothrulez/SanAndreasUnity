using UnityEditor;

namespace SanAndreasUnity.Kochi.Editor
{
    /// Keeps the OpenStreetMap ground and minimap textures sharp and readable (the minimap samples pixels).
    public class KochiOsmTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Kochi/OSM/"))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = assetPath.EndsWith("ground.png");
            importer.isReadable = assetPath.EndsWith("minimap.png");
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.textureCompression = assetPath.EndsWith("minimap.png")
                ? TextureImporterCompression.Uncompressed
                : TextureImporterCompression.Compressed;
        }
    }
}
