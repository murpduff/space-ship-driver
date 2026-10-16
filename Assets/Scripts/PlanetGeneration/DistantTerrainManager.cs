using UnityEngine;

public class DistantTerrainManager : MonoBehaviour {
    public Mesh sharedChunkMesh;
    public Material distantMaterial;

    public ComputeBuffer matrixBuffer;
    public Matrix4x4[] matrices;

    private const int MAX_CHUNKS = 2048;
    private int activeCount = 0;

    void Start() {
        matrixBuffer = new ComputeBuffer(MAX_CHUNKS, sizeof(float) * 16);
        matrices = new Matrix4x4[MAX_CHUNKS];
    }

    void Update() {
        if(activeCount == 0) return;

        distantMaterial.SetBuffer("_MatrixBuffer", matrixBuffer);

        Graphics.DrawMeshInstancedProcedural(sharedChunkMesh, 0, distantMaterial, new Bounds(Vector3.zero, Vector3.one * 50000), activeCount);
    }

    void OnDisable() {
        matrixBuffer?.Release();
    }
}
