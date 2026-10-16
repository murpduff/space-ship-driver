using UnityEngine;

[ExecuteAlways]
public class PlanetManager : MonoBehaviour
{
    public Material atmMat;
    public float sphereRadius = 50000f;
    public float atmosHeight = 50f;
    public Vector3 planetPosition = Vector3.zero;
    public float scatterStrength;

    public Vector3 waveLengths = new Vector3(700, 530, 400); //wavelengths of red, green, blue light

    void OnValidate()
    {
        float scatterR = Mathf.Pow(400 / waveLengths.x, 4);
        float scatterG = Mathf.Pow(400 / waveLengths.y, 4);
        float scatterB = Mathf.Pow(400 / waveLengths.z, 4);
        Vector3 scatteringCoefficients = new Vector3(scatterR, scatterG, scatterB) * scatterStrength;

        atmMat.SetVector("_ScatteringCoefficients", scatteringCoefficients);

        // These values are now "Global" and visible to all shaders
        Shader.SetGlobalFloat("_SharedSphereRadius", sphereRadius);
        Shader.SetGlobalFloat("_SharedAtmosHeight", atmosHeight);
        Shader.SetGlobalVector("_SharedSpherePos", planetPosition);
    }
}