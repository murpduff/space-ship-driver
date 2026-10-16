using UnityEngine;

public class PlanetTerrainHandler : MonoBehaviour {
    [Header("Shader Inputs")]
    [SerializeField] public int resolution;
    [SerializeField] ComputeShader computeShader;
    [SerializeField] Material planetMaterial;

    [Header("NoiseGen Inputs")]
    [SerializeField] float frequency;
    [SerializeField] float amplitude;
    [SerializeField] int octaves;
    [SerializeField] int seed;

    public RenderTexture cubeMap;

    //Approximate area of a pixel: 81 km^2, 9x9 km

    void Start() {
        CreatePlanet();
    }
    void CreatePlanet() {
        CreateCubeMap();
        DispatchComputeShader();
        planetMaterial.SetTexture("_SplatMap", cubeMap);
    }

    void DispatchComputeShader() {
        int kernel = computeShader.FindKernel("CSMain");

        computeShader.SetTexture(kernel, "Result", cubeMap);
        computeShader.SetInt("_Resolution", resolution);

        computeShader.SetFloat("_Frequency", frequency);
        computeShader.SetFloat("_Amplitude", amplitude);
        computeShader.SetInt("_Octaves", octaves);
        computeShader.SetInt("_Seed", seed);

        computeShader.Dispatch(kernel, resolution/8, resolution/8, 6);
        GL.Flush();
    }

    void CreateCubeMap() {
        if(cubeMap != null) return;

        // Use a constructor that doesn't pre-initialize the dimension
        cubeMap = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        
        // Set these BEFORE calling .Create()
        cubeMap.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        cubeMap.volumeDepth = 6; // This MUST be 6 for a Cubemap
        cubeMap.enableRandomWrite = true;
        
        // Some versions of Unity require this to force the Array behavior
        cubeMap.useMipMap = false; 
        cubeMap.autoGenerateMips = false;
        cubeMap.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm;

        cubeMap.filterMode = FilterMode.Point;
        cubeMap.wrapMode = TextureWrapMode.Clamp;

        cubeMap.Create();
    }
}
