using UnityEngine;

public class TextureArraySettings : MonoBehaviour {
    public Texture2D[] diffuseTextures;
    public Texture2D[] normalTextures;
    public Material terrainMat;

    void Start() {
        if(diffuseTextures.Length == 0 || normalTextures.Length == 0) return;

        // Final param: 'true' for linear (Normal Maps), 'false' for sRGB (Diffuse)
        Texture2DArray diffuseArray = CreateArray(diffuseTextures, false);
        Texture2DArray normalArray = CreateArray(normalTextures, true);

        terrainMat.SetTexture("_DiffuseArray", diffuseArray);
        terrainMat.SetTexture("_NormalArray", normalArray);
    }

    Texture2DArray CreateArray(Texture2D[] sources, bool isLinear) {
        int width = sources[0].width;
        int height = sources[0].height;
        TextureFormat format = sources[0].format;
        int mipCount = sources[0].mipmapCount;

        Texture2DArray texArray = new Texture2DArray(width, height, sources.Length, format, mipCount > 1, isLinear) {
            anisoLevel = sources[0].anisoLevel,
            filterMode = sources[0].filterMode,
            wrapMode = sources[0].wrapMode
        };

        for (int i = 0; i < sources.Length; i++) {
            for (int mip = 0; mip < mipCount; mip++) {
                // Copy EVERY mipmap level, otherwise it turns black at a distance
                Graphics.CopyTexture(sources[i], 0, mip, texArray, i, mip);
            }
        }
        return texArray;
    }
}