using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class TextureArrayCreator {
    [MenuItem("Assets/Create/Smart Texture Array")]
    static void CreateArray() {
        Object[] selected = Selection.objects;
        List<Texture2D> texs = new List<Texture2D>();

        foreach (Object obj in selected) {
            string path = AssetDatabase.GetAssetPath(obj);
            if (Directory.Exists(path)) {
                string[] files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                foreach (string file in files) {
                    if (file.EndsWith(".meta")) continue;
                    Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(file);
                    if (t != null) texs.Add(t);
                }
            } else {
                Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (t != null) texs.Add(t);
            }
        }

        if (texs.Count == 0) return;

        // --- AUTOMATION LOGIC ---
        string firstTexPath = AssetDatabase.GetAssetPath(texs[0]);
        TextureImporter importer = AssetImporter.GetAtPath(firstTexPath) as TextureImporter;
        
        // If it's a Normal Map, it MUST be linear. 
        // If it's a Default/Diffuse map, it should usually be sRGB (linear = false).
        bool forceLinear = false;
        if (importer != null) {
            if (importer.textureType == TextureImporterType.NormalMap) {
                forceLinear = true;
                Debug.Log("Detected Normal Maps: Applying Linear Color Space.");
            } else {
                forceLinear = false;
                Debug.Log("Detected Diffuse Maps: Applying sRGB Color Space.");
            }
        }

        int w = texs[0].width;
        int h = texs[0].height;
        TextureFormat fmt = texs[0].format;

        // Create the array with our detected color space
        Texture2DArray array = new Texture2DArray(w, h, texs.Count, fmt, true, forceLinear);
        array.filterMode = texs[0].filterMode;
        array.wrapMode = texs[0].wrapMode;

        for (int i = 0; i < texs.Count; i++) {
            for (int m = 0; m < texs[i].mipmapCount; m++) {
                Graphics.CopyTexture(texs[i], 0, m, array, i, m);
            }
        }

        string savePath = EditorUtility.SaveFilePanelInProject("Save Texture Array", "NewArray", "asset", "Save Array");
        if (!string.IsNullOrEmpty(savePath)) {
            AssetDatabase.CreateAsset(array, savePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Success!");
        }
    }
}