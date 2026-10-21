using UnityEditor;
using UnityEngine;

// Game illustrations are drawn directly by IMGUI at a fixed screen size.
public sealed class MonsterTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Resources/Monsters/")&&!assetPath.StartsWith("Assets/Resources/Food/"))return;
        var texture=(TextureImporter)assetImporter;
        texture.textureType=TextureImporterType.Default;
        texture.sRGBTexture=true;
        texture.alphaSource=TextureImporterAlphaSource.FromInput;
        texture.alphaIsTransparency=true;
        texture.mipmapEnabled=false;
        texture.isReadable=false;
        texture.npotScale=TextureImporterNPOTScale.None;
        texture.maxTextureSize=2048;
        texture.textureCompression=TextureImporterCompression.Uncompressed;
        texture.wrapMode=TextureWrapMode.Clamp;
        texture.filterMode=FilterMode.Bilinear;
    }
}
