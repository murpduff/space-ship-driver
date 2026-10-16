using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class SamplePlanetaryTerrain : MonoBehaviour {
    public Vector3 entranceDir; //world
    public int sampleSize;
    Vector3 localDir;
    Vector3 posInTex;
    public Planet planet;
    PlanetTerrainHandler terrainHandler;
    [SerializeField] bool trigger = false;
    [SerializeField] bool generateChunks;
    bool generateStart = false;
    public Material testMat;
    Texture2D resultTexture;
    public Color32[] pixelGrid;
    public GameObject terrainGenerator;
    public SurfaceTerrainGeneration sTerrainGen;

    void Start() {
        Screen.SetResolution(480, 360, FullScreenMode.MaximizedWindow);
        terrainHandler = planet.GetComponent<PlanetTerrainHandler>();
        generateStart = false;
    }

    void Update() {
        if (trigger) {
            //GetHeight();
            localDir = planet.transform.InverseTransformDirection(entranceDir);
            Vector3 initialPos = GetPointOnTex(localDir);

            if(generateChunks){
                sTerrainGen.initialWorldUV = initialPos;
                GetHeight(new ChunkObject(), Vector3.zero, initialPos);
            }else{
                GetHeight(new ChunkObject(), Vector3.zero, initialPos);
            }

            trigger = false;
        }
    }
    public void GetHeight(ChunkObject chunk, Vector2 pos, Vector3 address) {
        int startX = (int)(address.x * terrainHandler.resolution) - sampleSize / 2 + Mathf.FloorToInt(pos.x / sTerrainGen.pixelSize);
        startX = Mathf.Clamp(startX, sampleSize / 2, terrainHandler.resolution - sampleSize / 2);

        int startY = (int)(address.y * terrainHandler.resolution) - sampleSize / 2 + Mathf.FloorToInt(pos.y / sTerrainGen.pixelSize);
        startY = Mathf.Clamp(startY, sampleSize / 2, terrainHandler.resolution - sampleSize / 2);

        AsyncGPUReadback.Request(terrainHandler.cubeMap, 0, startX, sampleSize, startY, sampleSize, (int)address.z, 1, (request) => {
            OnCompleteReadback(request, chunk, pos);
        });

        //print(new Vector3(address.x * terrainHandler.resolution, address.y * terrainHandler.resolution, address.z));
    }
    

    void OnCompleteReadback(AsyncGPUReadbackRequest request, ChunkObject chunk, Vector2 pos) {
        if (request.hasError) {
            Debug.LogError("GPU Readback error.");
            return;
        }

        var data = request.GetData<Color32>();

        if(resultTexture == null) resultTexture = new Texture2D(sampleSize, sampleSize, TextureFormat.RGBA32, false);
        resultTexture.SetPixels32(data.ToArray());
        resultTexture.filterMode = FilterMode.Point;
        resultTexture.Apply();

        pixelGrid = data.ToArray();

        //Debug.Log($"Sampled Planet Data: {pixelGrid[sampleSize * sampleSize / 2]}");

        testMat.mainTexture = resultTexture;

        if(generateChunks && generateStart){
            sTerrainGen.CreateChunk(chunk, new Vector2Int((int)pos.x, (int)pos.y), pixelGrid);
        }

        if (!generateStart) {
            float height = pixelGrid[5].r / 255f;
            generateStart = true;
            sTerrainGen.BeginChunkGeneration(height);
        }
    }

    Vector3 GetPointOnTex(Vector3 localDir) {
        Vector3 absDir = Abs(localDir);
        float maxAxis = Mathf.Max(absDir.x, Mathf.Max(absDir.y, absDir.z));
        Vector2 uv = Vector2.zero;
        int index = 0;

        if (maxAxis == absDir.x) {
            index = localDir.x > 0 ? 0 : 1; // +X, -X
            uv = localDir.x > 0 ? new Vector2(-localDir.z, -localDir.y) : new Vector2(localDir.z, -localDir.y);
        } 
        else if (maxAxis == absDir.y) {
            index = localDir.y > 0 ? 2 : 3; // +Y, -Y
            uv = localDir.y > 0 ? new Vector2(localDir.x, localDir.z) : new Vector2(localDir.x, -localDir.z);
        } 
        else {
            index = localDir.z > 0 ? 4 : 5; // +Z, -Z
            uv = localDir.z > 0 ? new Vector2(localDir.x, -localDir.y) : new Vector2(-localDir.x, -localDir.y);
        }

        uv = uv / maxAxis * 0.5f + new Vector2(0.5f, 0.5f);

        return new Vector3(uv.x, uv.y, index);
    }

    Vector3 Abs(Vector3 v) {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
