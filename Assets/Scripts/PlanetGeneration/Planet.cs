using UnityEngine;

public class Planet : MonoBehaviour {
    //1 : 25,500 scale
    //acceleration calculation g0 * (R/r)^2
    [SerializeField] float radius; //Unity units
    [SerializeField] float surfaceGravitation;
    [SerializeField] float rotationSpeed;
    Transform spaceShip;
    Rigidbody shipRb;
    public float gravity;
    public Vector3 worldPos; // 1 : 1000, worldPos : worldTransform
    void OnValidate() {
        transform.position = worldPos * 1000f;
    }

    void Start() {
        spaceShip = FindAnyObjectByType<ShipMovement>().transform;
        shipRb = spaceShip.GetComponent<Rigidbody>();
        transform.position = worldPos * 1000f;
    }

    void Update() {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
    }

    void FixedUpdate() {
        Vector3 difference = transform.position - spaceShip.position;
        float sqrDistance = difference.sqrMagnitude;
        gravity = surfaceGravitation * radius * radius / sqrDistance;

        if(gravity > 0.01f){
            shipRb.AddForce(difference.normalized * gravity * shipRb.mass);
        }
    }
}
