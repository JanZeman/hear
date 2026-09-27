using UnityEditor;

namespace Hear.Editor
{
    /// <summary>
    /// Auto-configures any texture whose filename ends in "_Normal" (the naming convention used by
    /// the Viking Boat world's PBR texture set, see VikingBoatPresentation) as a proper Normal Map
    /// import type. Without this, Unity treats it as a plain color texture and the encoding Unity's
    /// normal-map shaders expect (DXT5nm-style) is wrong, making lighting look subtly broken - not
    /// something fixable from a runtime script, since it's an import-time setting with no Editor
    /// GUI available in this headless pipeline to set by hand.
    /// </summary>
    public sealed class NormalMapImportProcessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.EndsWith("_Normal.png")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.NormalMap;
        }
    }
}
