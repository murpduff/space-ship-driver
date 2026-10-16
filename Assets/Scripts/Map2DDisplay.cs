using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class Map2DDisplay : MonoBehaviour{
    [SerializeField] GameObject radarPoint;
    [SerializeField] Vector2 imageDimensions;
    [SerializeField] Transform shipModel;
    public float fadeThreshold;
    Vector3 directionToTarget;
    Vector3 mapNormal; 
    Vector3 mapRight;
    ShipMovement shipMovement;
    Planet[] planets;
    Planet[] relevantPlanets;
    RectTransform[] relevantPoints;
    float mapScale;
    void Start() {
        shipMovement = FindAnyObjectByType<ShipMovement>();
        planets = FindObjectsByType<Planet>(FindObjectsSortMode.None);
        SetMapDisplay();
    }

    void Update() {
        SetPointPositions();
        SetShipRotationVector();
    }

    void SetMapDisplay() {
        directionToTarget = (shipMovement.targetPlanet.worldPos - shipMovement.worldPos).normalized;
        mapNormal = Vector3.Cross(directionToTarget, shipMovement.transform.right).normalized;
        mapRight = Vector3.Cross(mapNormal, directionToTarget);

        mapScale = Vector3.Distance(shipMovement.targetPlanet.worldPos, shipMovement.worldPos);

        List<Planet> planetList = new List<Planet>();
        List<RectTransform> points = new List<RectTransform>();
        for(int i = 0; i < planets.Length; i++) {
            float visibility = GetPlanetVisibility(planets[i].worldPos, shipMovement.worldPos, mapNormal);
            if (visibility > 0f) {
                RectTransform point = Instantiate(radarPoint, transform).GetComponent<RectTransform>();
                point.GetComponent<Image>().color = new Color(visibility, visibility, visibility, visibility);
                planetList.Add(planets[i]);
                points.Add(point);
            }
        }
        relevantPlanets = planets.ToArray();
        relevantPoints = points.ToArray();
    }

    void SetPointPositions() {
        for(int i = 0; i < relevantPlanets.Length; i++) {
            Vector3 relativePos = relevantPlanets[i].worldPos - shipMovement.worldPos;

            float xPos = Vector3.Dot(relativePos, directionToTarget);
            float yPos = Vector3.Dot(relativePos, mapRight);
            relevantPoints[i].anchoredPosition = new Vector2(xPos, yPos) / mapScale * imageDimensions;

            // Getting the planet position on the map: 
            // X-axis: Vector3 targetDirection
            // Y-axis: Vector3.CrossProduct(mapNormal, targetDirection)
        }
    }

    void SetShipRotationVector() {
        Vector3 dir = shipMovement.transform.forward;
        float x = Vector3.Dot(dir, directionToTarget);
        float y = Vector3.Dot(dir, mapNormal);
        float z = Vector3.Dot(dir, mapRight);

        shipModel.transform.rotation = Quaternion.LookRotation(new Vector3(x, y, z).normalized, Vector3.up);
    }

    float GetPlanetVisibility(Vector3 planetPos, Vector3 playerPos, Vector3 mapNormal) {
        float distanceToPlane = Mathf.Abs(Vector3.Dot(mapNormal, planetPos - playerPos));

        return Mathf.Clamp01(1.0f - (distanceToPlane / fadeThreshold));
    }
}
