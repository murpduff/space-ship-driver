using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using System.Collections.Generic;
using System.Collections;

public struct ChunkObject{
    public GameObject chunkObj;
    public int LOD;
}

[BurstCompile]
public class SurfaceTerrainGeneration : MonoBehaviour { 
    private class ActiveChunk {
        public bool isBusy;
        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;
        public Vector2 GridPosition; //Flat space, this is relative to the center of the origin
        public NativeArray<float3> NativeVertices;
        public NativeArray<float3> NativeNormals;
        public NativeArray<int> NativeTriangles;
        public int LOD;
        public JobHandle Handle;
        public MeshCollider Collider;

        public void OnDestroy() {
            if(NativeVertices.IsCreated) NativeVertices.Dispose();
            if(NativeNormals.IsCreated) NativeNormals.Dispose();
            if(NativeTriangles.IsCreated) NativeTriangles.Dispose();
        }
    }

    [Header("Chunk Properties")]
    [SerializeField] public int chunkSize;
    [SerializeField] public int chunkScale;
    [SerializeField] public int pixelSize = 12; // pixelSize^2 = pixel on the map (= 12 for earth scale)
    [SerializeField] public Material terrainMat;
    [SerializeField] public Vector2 chunkGen; //placeHolder
    [SerializeField] public GameObject chunkPrefab;
    [SerializeField] public bool stichLODSeams; //Does not work very well (very sad)
    [SerializeField] public bool enableChunkSkirts;
    [SerializeField] public Vector3 offset;
    [SerializeField] int LOD;
    [SerializeField] SamplePlanetaryTerrain sampleTerrain;

    [Header("Terrain Properties")]
    [SerializeField] public float amplitude;
    [SerializeField] public float frequency;
    [SerializeField] public int octaves;
    [SerializeField] public float pixelFadePow;
    [SerializeField] public float powerAmplitude; //How steep mountains should be based on terrain height.
    [SerializeField] public float baseAmplitude;
    [SerializeField] public float baseMountainHeight;
    [SerializeField] public float oceanSteepness;
    [SerializeField] public float oceanThreshold;
    [SerializeField] public float oceanDepth;
    [SerializeField] public float pixelHeightAmplifier;
    [SerializeField] public float pixelHeightPower;

    [Header("Chunk Rendering")]
    [SerializeField] Transform player;
    [SerializeField] GameObject water;
    [SerializeField] public int viewDst; //Radius in which chunks can be seen
    [SerializeField] float chunkUpdateDst; //How far the player must travel to update chunk field
    [SerializeField] bool forceChunkUpdate; //Override all other generation requirments
    [SerializeField] float meshBoundsIncrease; //To account for curvature
    [SerializeField] bool applyLODEffect;
    [SerializeField] AnimationCurve LODByDst;
    [SerializeField] int maxActiveJobs;
    [SerializeField] int maxApplyPerFrame;

    public Vector3 initialWorldUV;
    int isUpdatingChunkField = 0;
    bool isGeneratingChunks = false;
    Coroutine activeGenerationRoutine;

    Vector3 prevPlayerPosition; //At chunk scale (1 unit is chunk width)
    public Vector2Int playerChunkCoord;

    Dictionary<Vector2Int, ChunkObject> activeChunks = new Dictionary<Vector2Int, ChunkObject>();
    List<Vector2Int> updatingNeighbor = new List<Vector2Int>();
    Stack<ChunkObject> chunkPool = new Stack<ChunkObject>(); //storing inactive chunks for later use
    public Vector2 originPosition; //reference for chunk positions

    Vector3[] vertices;
    int[] triangles;

    List<ActiveChunk> activeHeightJobs = new List<ActiveChunk>();
    [SerializeField] int jobsInFlight;

    // 12x12 of chunks is one pixel on the planet

    void OnGUI() {
        GUI.Box(new Rect(10, 10, 250, 70), "Terrain Pipeline Stats");
        GUI.Label(new Rect(20, 50, 230, 20), $"Active Jobs (CPU): {activeHeightJobs.Count}");
    }
    void Start() {
        originPosition = Vector2.zero;

        isGeneratingChunks = false;
        forceChunkUpdate = true;
        isUpdatingChunkField = 0;
    }
    public void BeginChunkGeneration(float height) {
        //SetInitialMeshData();
        InitializeChunks();

        float val = (math.pow(height / 2f + 1 + baseMountainHeight, math.abs(powerAmplitude)) - 1) * (powerAmplitude / math.abs(powerAmplitude));
        val *= amplitude;
        player.GetComponent<PlayerMovement>().Spawn(new Vector3(0, val, 0));
        //player.GetComponent<PlayerMovement>().Spawn(new Vector3(0, Mathf.Pow(height * pixelHeightAmplifier, pixelHeightPower) + 2000, 0));

        //float val = (math.pow(height / 2f + 1 + bMHeight, math.abs(pow)) - 1) * (pow / math.abs(pow));
        //return new Vector3(offset.x, (val + height * minAmp) * amplitude + terrainElevation, offset.y);
        

        water.SetActive(true);
    }

    void Update() {
        Vector3 playerChunkPos = (player.position - new Vector3(originPosition.x, 0, originPosition.y)) / (chunkSize * chunkScale) ;
        playerChunkCoord = new Vector2Int(Mathf.FloorToInt(playerChunkPos.x), Mathf.FloorToInt(playerChunkPos.z));
        UpdateJobs();

        float playerDst = Vector3.Distance(playerChunkPos, prevPlayerPosition);
        if(isGeneratingChunks && (playerDst > chunkUpdateDst || forceChunkUpdate)){
            UpdateVisibleChunks();
            prevPlayerPosition = playerChunkPos;
            forceChunkUpdate = false;
        }

        water.transform.position = player.position;
    }

    void UpdateVisibleChunks() {
        List<Vector2Int> toRemove = new List<Vector2Int>();
        foreach(Vector2Int coord in activeChunks.Keys) {
            float dst = Vector2.Distance(coord, playerChunkCoord);
            if(dst > viewDst) {
                toRemove.Add(coord);
            }
        }

        foreach(Vector2Int coord in toRemove) {
            ChunkObject chunk = activeChunks[coord];
            chunkPool.Push(chunk);
            activeChunks.Remove(coord);
            chunk.chunkObj.SetActive(false);
        }
        
        if(activeGenerationRoutine != null) {
            StopCoroutine(activeGenerationRoutine);
        }
        activeGenerationRoutine = StartCoroutine(UpdateChunkField());
    }

    void UpdateJobs() {
        int appliedThisFrame = 0;
        int maxAppliesPerFrame = 2;
        List<ActiveChunk> toRemove = new List<ActiveChunk>();
        foreach(ActiveChunk v in activeHeightJobs) {
            if(appliedThisFrame >= maxAppliesPerFrame) {
                break;
            }

            if (v.Handle.IsCompleted) {
                v.Handle.Complete();

                ApplyToMesh(v);

                v.OnDestroy();
                toRemove.Add(v);

                appliedThisFrame++;
            }
        }

        foreach(ActiveChunk v in toRemove) {
            activeHeightJobs.Remove(v);
        }
    }

    void InitializeChunks() {
        for(int y = -viewDst; y <= viewDst; y++) {
            for(int x = -viewDst; x <= viewDst; x++) {
                ChunkObject chunk = new ChunkObject{chunkObj = Instantiate(chunkPrefab, new Vector3(x, 0, y) * chunkSize * chunkScale + offset, Quaternion.identity), LOD = LOD};
                chunk.chunkObj.transform.parent = transform;

                Mesh mesh = new Mesh();
                mesh.vertices = vertices;
                mesh.triangles = triangles;

                chunk.chunkObj.GetComponent<MeshFilter>().mesh = mesh;

                chunkPool.Push(chunk);
                chunk.chunkObj.SetActive(false);

                //CreateChunk(chunk, LOD, new Vector2(x, y));
                //sampleTerrain.GetHeight(chunk, LOD, new Vector2(x, y), initialPos);
            }
        }
        isGeneratingChunks = true;
    }

    //THIS IS THE PRIMARY CHUNK GENERATION
    IEnumerator UpdateChunkField() {
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        isUpdatingChunkField++;

        //Spiral iteration to spawn chunks closest to the player
        int x = 0;
        int y = 0;
        int dx = 0;
        int dy = -1;

        int maxSteps = (viewDst * 2 + 1) * (viewDst * 2 + 1);
        updatingNeighbor.Clear();

        for(int i = 0; i < maxSteps; i++) {
            sw.Start();

            if(x == y || (x < 0 && x == -y) || (x > 0 && x == 1 - y)) {
                int temp = dx;
                dx = -dy;
                dy = temp;
            }
            
            Vector2Int chunkCoord = playerChunkCoord + new Vector2Int(x, y);
            float dst = Vector2.Distance(chunkCoord, playerChunkCoord);

            while(jobsInFlight >= maxActiveJobs) {
                yield return null;
            }
            
            if(activeChunks.TryGetValue(chunkCoord, out ChunkObject existingChunk)) {
                int targetLOD = GetLOD(chunkCoord);
                if(existingChunk.LOD != targetLOD) {
                    if(stichLODSeams){
                        Vector2Int[] neighborCoords = new Vector2Int[]{Vector2Int.zero, Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left};
                        for(int n = 0; n < 5; n++){
                            if(activeChunks.ContainsKey(chunkCoord + neighborCoords[n]) && !updatingNeighbor.Contains(chunkCoord + neighborCoords[n])){
                                sampleTerrain.GetHeight(activeChunks[chunkCoord + neighborCoords[n]], chunkCoord + neighborCoords[n], initialWorldUV);
                                updatingNeighbor.Add(chunkCoord + neighborCoords[n]);
                                jobsInFlight++;
                            }
                        }
                    } else {
                        sampleTerrain.GetHeight(activeChunks[chunkCoord], chunkCoord, initialWorldUV);
                        jobsInFlight++;
                    }
                    existingChunk.LOD = targetLOD;

                    activeChunks[chunkCoord] = existingChunk;
                }
            } else if(dst < viewDst) {
                ChunkObject chunk = chunkPool.Pop();
                chunk.chunkObj.SetActive(true);
                chunk.chunkObj.transform.position = new Vector3(chunkCoord.x, 0, chunkCoord.y) * chunkSize * chunkScale + new Vector3(originPosition.x, 0, originPosition.y);
                chunk.LOD = GetLOD(chunkCoord);

                jobsInFlight++;
                activeChunks.Add(chunkCoord, chunk);

                sampleTerrain.GetHeight(chunk, chunkCoord, initialWorldUV);
            }

            x += dx;
            y += dy;

            if(sw.Elapsed.TotalMilliseconds > 2.0) {
                yield return null;
                sw.Restart();
            }

        }

        isUpdatingChunkField--;
    }

    public void CreateChunk(ChunkObject chunk, Vector2Int position, Color32[] planetMap) {
        //Debug.Log("1. StartChunkGeneration called for: " + chunkObject.name);

        // if (activeHeightJobs.ContainsKey(position)) {
        //     print("already in process");
        //     return;
        // }

        // if (activeHeightJobs.TryGetValue(position, out ActiveChunk existingJob)) {
        //     // We must stop and clean up the old one before starting a new one
        //     existingJob.Handle.Complete(); 
        //     existingJob.NativeVertices.Dispose();
        //     existingJob.NativeTriangles.Dispose();
        //     existingJob.NativeNormals.Dispose();
        //     // Discard the old abort signal too if you allocated it
            
        //     activeHeightJobs.Remove(position);
        // }
        if(activeHeightJobs.Count > maxActiveJobs) return;

        int jobLOD = GetLOD(position);
        
        int neighborMask = 0;//GetNeighborMask(position, jobLOD);

        MeshFilter mf = chunk.chunkObj.GetComponent<MeshFilter>();
        MeshRenderer mr = chunk.chunkObj.GetComponent<MeshRenderer>();
        MeshCollider mc = chunk.chunkObj.GetComponent<MeshCollider>();

        //Adding the extra tiles around the border [][0][]
        int vertSkirts = enableChunkSkirts ? 2 : 0;

        NativeArray<float3> nVerts = new NativeArray<float3>((chunkSize / jobLOD + 1 + vertSkirts) * (chunkSize / jobLOD + 1 + vertSkirts), Allocator.Persistent);
        NativeArray<float3> nNormals = new NativeArray<float3>(nVerts.Length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        NativeArray<int> nTris = new NativeArray<int>((chunkSize / jobLOD + vertSkirts) * (chunkSize / jobLOD + vertSkirts) * 6, Allocator.Persistent);
        NativeArray<Color32> nPlanetMap = new NativeArray<Color32>(planetMap.Length, Allocator.Persistent);

        NativeArray<bool> abort = new NativeArray<bool>(1, Allocator.Persistent); //these are for later but right now don't do anything
        abort[0] = false;

        for(int i = 0; i < planetMap.Length; i++) {
            nPlanetMap[i] = planetMap[i];
        }

        HeightJob job = new HeightJob {
            abortSignal = abort,
            vertices = nVerts,
            triangles = nTris,
            normals = nNormals,
            position = position,
            pixelGrid = nPlanetMap,
            chunkSize = chunkSize,
            chunkScale = chunkScale,
            pixelSize = pixelSize,
            LOD = jobLOD,
            frequency = frequency,
            amplitude = amplitude,
            octaves = octaves,
            fadePow = pixelFadePow,
            powAmp = powerAmplitude,
            bMHeight = baseMountainHeight,
            oceanDepth = oceanDepth,
            oceanSteepness = oceanSteepness,
            oceanThreshold = oceanThreshold,
            pixelAmp = pixelHeightAmplifier,
            pixelPow = pixelHeightPower,
            minAmp = baseAmplitude,
            initialWorldUV = initialWorldUV,
            neighborMask = neighborMask,
            enableChunkSkirts = enableChunkSkirts
        };

        //Debug.Log("2. Scheduling Job...");

        JobHandle handle = job.Schedule(nVerts.Length, 64);

        activeHeightJobs.Add(new ActiveChunk {
            isBusy = true, //this also does nothing right now
            MeshFilter = mf,
            MeshRenderer = mr,
            GridPosition = position,
            NativeVertices = nVerts,
            NativeTriangles = nTris,
            NativeNormals = nNormals,
            LOD = jobLOD,
            Handle = handle,
            Collider = mc
        });

        // int GetNeighborMask(Vector2Int coord, int myLOD) {
        //     int mask = 0;
        //     if(GetLOD(coord + Vector2Int.up) > myLOD) {
        //         mask |= (int)NeighborLODMask.North;
        //     }
        //     if(GetLOD(coord + Vector2Int.right) > myLOD) {
        //         mask |= (int)NeighborLODMask.East;
        //     }
        //     if(GetLOD(coord + Vector2Int.down) > myLOD) {
        //         mask |= (int)NeighborLODMask.South;
        //     }
        //     if(GetLOD(coord + Vector2Int.left) > myLOD) {
        //         mask |= (int)NeighborLODMask.West;
        //     }

        //     return mask;
        // }
    }

    void ApplyToMesh(ActiveChunk chunk){
        Mesh mesh = chunk.MeshFilter.mesh;
        mesh.Clear();

        Vector3[] newVerts = new Vector3[chunk.NativeVertices.Length];
        Vector3[] newNormals = new Vector3[newVerts.Length];

        for(int i = 0; i < chunk.NativeVertices.Length; i++) {
            newVerts[i] = chunk.NativeVertices[i];
            newNormals[i] = chunk.NativeNormals[i];
        }

        int[] newTris = new int[chunk.NativeTriangles.Length];
        for(int i = 0; i < chunk.NativeTriangles.Length; i++) newTris[i] = chunk.NativeTriangles[i];

        mesh.vertices = newVerts;
        mesh.normals = newNormals;
        mesh.triangles = newTris;

        mesh.RecalculateBounds();
        Bounds bounds = mesh.bounds;
        bounds.extents += new Vector3(0, meshBoundsIncrease, 0);
        mesh.bounds = bounds;

        if(chunk.Collider != null) {
            chunk.Collider.enabled = false;
            if(chunk.LOD == 1){
                chunk.Collider.enabled = true;
                chunk.Collider.sharedMesh = mesh;
                chunk.MeshRenderer.receiveShadows = true;
            } else {
                chunk.MeshRenderer.receiveShadows = false;
            }
        }
        jobsInFlight--;
    }

    int GetLOD(Vector2Int coord) {
        if(!applyLODEffect) return LOD;

        float dst = Vector2.Distance(coord, playerChunkCoord);

        if(dst > viewDst) {
            dst = viewDst;
        }

        float v = LODByDst.Evaluate(dst / viewDst);

        return (int)Mathf.Pow(2, (int)(v * Mathf.Log(chunkSize, 2)));
    }

    public void ShiftOriginPosition(Vector2 shift) {
        foreach(ChunkObject chunk in activeChunks.Values) {
            chunk.chunkObj.transform.position += new Vector3(shift.x, 0, shift.y);
        }
    }
}

[System.Flags]
public enum NeighborLODMask : byte{
    None = 0,
    North = 1 << 0,
    East = 1 << 1,
    South = 1 << 2,
    West = 1 << 3
}

//Idea to avoid LOD seams: For chunks with neighbors with LOD difference, skip noise values for edge vertices, and interpolate them to match the lower LOD

[BurstCompile]
public struct HeightJob : IJobParallelFor {
    [ReadOnly] public NativeArray<bool> abortSignal;
    public NativeArray<float3> vertices;
    [NativeDisableParallelForRestriction]
    public NativeArray<int> triangles;
    public NativeArray<float3> normals;
    [ReadOnly] public NativeArray<Color32> pixelGrid;
    public Vector2 position;
    public int chunkSize;
    public float chunkScale;
    public int pixelSize;
    public int LOD;
    public float frequency;
    public float amplitude;
    public float minAmp; //amplitude applied to general noise layer (the one applied to all terrain types regardless of pow)
    public int octaves;
    public float fadePow;
    public float powAmp;
    public float bMHeight; //baseMountainHeight
    public float oceanSteepness;
    public float oceanThreshold;
    public float oceanDepth;
    public float pixelAmp;
    public float pixelPow;
    public Vector3 initialWorldUV;
    public int neighborMask;
    public bool enableChunkSkirts;
    Vector2 pixelOffset;
    public void Execute(int index) {
        if(abortSignal[0]) return;
        int res = chunkSize / LOD;
    int vertexSide = res + 1 + (enableChunkSkirts ? 2 : 0);
    int skirtsEnable = enableChunkSkirts ? 1 : 0;

    // Calculate X and Y from 0 to vertexSide-1
    int ix = index % vertexSide;
    int iy = index / vertexSide;

    // Convert to "Grid Coordinates" where 0,0 is the start of the visible chunk
    int x = ix - skirtsEnable;
    int y = iy - skirtsEnable;

    // 1. TRIANGLE GENERATION
    // Only generate triangles for quads. 
    // We stop 1 vertex before the right/top edge of our expanded grid.
    if (ix < vertexSide - 1 && iy < vertexSide - 1) {
        int triInd = (ix + iy * (vertexSide - 1)) * 6;
        
        // Ensure triInd doesn't exceed triangles.Length
        if (triInd + 5 < triangles.Length) {
            triangles[triInd + 0] = index;
            triangles[triInd + 1] = index + vertexSide;
            triangles[triInd + 2] = index + 1;
            triangles[triInd + 3] = index + vertexSide;
            triangles[triInd + 4] = index + vertexSide + 1;
            triangles[triInd + 5] = index + 1;
        }
    }

    // 2. VERTEX POSITIONING
    // Is this a skirt vertex? (The outer border)
    bool isSkirt = (x < 0 || x > res || y < 0 || y > res);
    
    // Clamp coordinates for Noise sampling so the skirt 
    // pulled down from the EXACT edge height
    int sampledX = math.clamp(x, 0, res);
    int sampledY = math.clamp(y, 0, res);

    Vector3 vert = NoiseGen(WorldX(sampledX, 0), WorldY(sampledY, 0), frequency, amplitude, octaves, new Vector2Int(x, y), Vector2Int.zero, 0);
    
    // Position the vertex
    float h = vert.y;
    if (isSkirt) {
        h -= 500f; // Drop the skirt down
    }

    vertices[index] = new Vector3(x * chunkScale * LOD, h, y * chunkScale * LOD);

        // int skirtsEnable = enableChunkSkirts ? 1 : 0;

        // int x = mathMod(index, chunkSize / LOD + 1) - skirtsEnable;
        // int y = index / (chunkSize / LOD + 1) - skirtsEnable;

        // pixelOffset = new Vector2((initialWorldUV * chunkSize * pixelSize).x, (initialWorldUV * chunkSize * pixelSize).y);

        // if(x <= chunkSize / LOD && y <= chunkSize / LOD) {
        //     int triInd = (index - y) * 6;
            
        //     triangles[triInd + 0] = CoordToInd(new Vector2Int(x + 0, y + 0), skirtsEnable);
        //     triangles[triInd + 1] = CoordToInd(new Vector2Int(x + 0, y + 1), skirtsEnable);
        //     triangles[triInd + 2] = CoordToInd(new Vector2Int(x + 1, y + 0), skirtsEnable);
        //     triangles[triInd + 3] = CoordToInd(new Vector2Int(x + 0, y + 1), skirtsEnable); 
        //     triangles[triInd + 4] = CoordToInd(new Vector2Int(x + 1, y + 1), skirtsEnable);
        //     triangles[triInd + 5] = CoordToInd(new Vector2Int(x + 1, y + 0), skirtsEnable);
        // }
        
        // if(x < 0 || x > (chunkSize / LOD) || y < 0 || y > (chunkSize / LOD)){
        //     vertices[index] = new Vector3(x * chunkScale * LOD, -10000, y * chunkScale * LOD);
        // } else {
        //     vertices[index] = new Vector3(x * chunkScale * LOD, 0, y * chunkScale * LOD);
        // }

        // Vector3 vert = NoiseGen(WorldX(x, 0), WorldY(y, 0), frequency, amplitude, octaves, new Vector2Int(x, y), Vector2Int.zero, 0);
        // float h = vert.y;

        //Calculating the normal based on noise gradient in all directions
        float eps = 1f;
        float hL = NoiseGen(WorldX(x, -eps), WorldY(y, 0), frequency, amplitude, octaves, new Vector2Int(x, y), Vector2Int.zero, 0).y;
        float hR = NoiseGen(WorldX(x, +eps), WorldY(y, 0), frequency, amplitude, octaves, new Vector2Int(x, y), Vector2Int.zero, 0).y;
        float hD = NoiseGen(WorldX(x, 0), WorldY(y, -eps), frequency, amplitude, octaves, new Vector2Int(x, y), Vector2Int.zero, 0).y;
        float hU = NoiseGen(WorldX(x, 0), WorldY(y, +eps), frequency, amplitude, octaves, new Vector2Int(x, y), Vector2Int.zero, 0).y;

        eps *= chunkScale * LOD;

        Vector3 gL = new Vector3(eps, h - hL, 0);
        Vector3 gR = new Vector3(eps, hR - h, 0);
        Vector3 gD = new Vector3(0, h - hD, eps);
        Vector3 gU = new Vector3(0, hU - h, eps);
        
        Vector3 slopeRight = (gL + gR).normalized;
        Vector3 slopeForward = (gD + gU).normalized;
        float3 normal = Vector3.Cross(slopeForward, slopeRight).normalized;
        
        normals[index] = normal;

        Vector3 v = vertices[index];
        v += vert;
        vertices[index] = v;
    }

    //input a localPosition
    float WorldX(int x, float offset){ 
        return ((x + offset) * LOD) + position.x * chunkSize + pixelOffset.x;
    }
    float WorldY(int y, float offset) {
        return ((y + offset) * LOD) + position.y * chunkSize + pixelOffset.y;
    }

    Vector2 WorldToLoc(Vector2 v) {
        return new Vector2(v.x - position.x * chunkSize, v.y - position.y * chunkSize);
    }

    int CoordToInd(Vector2Int v, int skirts) {
        return (v.x + skirts) + (chunkSize / LOD + 1) * (v.y + skirts);
    }

    Vector3 NoiseGen(float x, float y, float frequency, float amplitude, int octaves, Vector2Int vertCoord, Vector2 offset, int recurLvl) {
        if(recurLvl > 5) return Vector3.zero;

        float height = 0;
        int edge = SetEdgeDir(chunkSize, LOD);

        //For LOD seams
        // if(edge == 0 && ((neighborMask & (int)NeighborLODMask.North) != 0)) {
        //     if(vertCoord.x % 2 == 1) {
        //         //return new Vector3(0, -1000f, 0);
        //         return NoiseGen(x + LOD, y, frequency, amplitude, octaves, vertCoord + Vector2Int.right, Vector2.right * LOD * chunkScale, recurLvl++);
        //     }
        // }
        // if(edge == 1 && ((neighborMask & (int)NeighborLODMask.East) != 0)) {
        //     if(vertCoord.y % 2 == 1) {
        //         //return new Vector3(0, -1000f, 0);
        //         return NoiseGen(x, y - LOD, frequency, amplitude, octaves, vertCoord + Vector2Int.down, Vector2.down * LOD * chunkScale, recurLvl++);
        //     }
        // }
        // if(edge == 2 && ((neighborMask & (int)NeighborLODMask.South) != 0)) {
        //     if(vertCoord.x % 2 == 1) {
        //         //return new Vector3(0, -1000f, 0);
        //         return NoiseGen(x - LOD, y, frequency, amplitude, octaves, vertCoord + Vector2Int.left, Vector2.left * LOD * chunkScale, recurLvl++);
        //     }
        // }
        // if(edge == 3 && ((neighborMask & (int)NeighborLODMask.West) != 0)) {
        //     if(vertCoord.y % 2 == 1) {
        //         //return new Vector3(0, -1000f, 0);
        //         return NoiseGen(x, y + LOD, frequency, amplitude, octaves, vertCoord + Vector2Int.up, Vector2.up * LOD * chunkScale, recurLvl++);
        //     }
        // }

        float freq = frequency;
        float amp = 1;

        float pixelHeight = GetPlanetaryHeight(new Vector2(x - pixelOffset.x, y - pixelOffset.y), true); //Method needs worldposition relative to starting pos, not planet
        float terrainElevation = math.pow(pixelHeight * pixelAmp, pixelPow);
        float oceanAmplifier = -(1 / (math.pow(pixelHeight - oceanThreshold, oceanSteepness) + oceanDepth));
        float pow = pixelHeight * powAmp + oceanAmplifier;
        
        float totAmp = 0;
        for(int i = 0; i < octaves; i++) {
            float v = noise.snoise(new Vector2(x * freq, y * freq)) * amp;
            height += v;
            
            totAmp += amp;
            freq *= 2;
            amp /= 2;
        }
        height /= totAmp; //return the average height of all terrain stacking

        float val = (math.pow(height / 2f + 1 + bMHeight, math.abs(pow)) - 1) * (pow / math.abs(pow));

        return new Vector3(offset.x, (val + height * minAmp) * amplitude + terrainElevation, offset.y);

        int SetEdgeDir(int chunkSize, int LOD) {
            int edge = -1; //-1: none, 0: north, 1: east, 2: south, 3: west
            if(vertCoord.y - 1 >= (chunkSize / LOD) - 1) {
                edge = 0;
            }else if(vertCoord.x - 1 >= (chunkSize / LOD) - 1) {
                edge = 1;
            }else if(vertCoord.y <= 0) {
                edge = 2;
            }else if(vertCoord.x <= 0) {
                edge = 3;
            }
            return edge;
        }
    }

    float GetPlanetaryHeight(Vector2 worldPosition, bool useSmoothing) { //world position is relative to the chunk generation origin
        if(useSmoothing){
            Vector2 posInChunk = WorldToLoc(worldPosition); // 0 - chunkSize (chunkSize > 1)
            Vector2 chunkPos = new Vector2(mathMod((int)position.x, pixelSize), mathMod((int)position.y, pixelSize));
            Vector2 posInPixel = (posInChunk + chunkPos * chunkSize) / (chunkSize * pixelSize); //0 - 1, within pixel range

            //texture smoothing for smooth transitions into different pixels of the planetTex
            //sample size is 3x3 always. May change later but probably not. Sample size is only different for testing in the SampleTerrain script
            float netMag = 0;
            float height = 0;
            for(int y = 0; y < 3; y++) {
                for(int x = 0; x < 3; x++) {
                    Vector2 samplePos = new Vector2(x - 0.5f, y - 0.5f);
                    int ind = x + 3 * y;
                    float dst = math.clamp(Vector2.Distance(posInPixel, samplePos), 0f, 1f);
                    float mag = math.pow(-dst + 1, fadePow);

                    height += mag * (pixelGrid[ind].r / 255f);
                    netMag += mag;
                }
            }
            if(netMag > 0){
                height /= netMag;
            }
            return height;
        } else {
            return pixelGrid[4].r / 255f;
        }
    }

    public static int mathMod(int n, int m){
        return ((n % m) + m) % m;
    }
}

