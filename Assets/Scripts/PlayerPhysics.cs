using UnityEngine;

public class PlayerPhysics : MonoBehaviour
{
    [SerializeField] float gravityScale;
    [SerializeField] float mass;
    [SerializeField] Transform spaceShip;
    [SerializeField] float maxVel;
    [SerializeField] LayerMask shipLayer;
    [SerializeField] float safety;
    Vector3 velocity;
    Rigidbody rb;

    void Start() {
        rb = GetComponent<Rigidbody>();
    }

    void Update() {
        if(Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 10f)) {
            Debug.DrawLine(transform.position, hit.point, Color.green);
        } else {
            Debug.DrawLine(transform.position, transform.position + Vector3.down, Color.red);
        }
    }

    void FixedUpdate() {
        //gravity
        AddForce(-spaceShip.transform.up * gravityScale);
        rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
        Collision();
    }

    void AddForce(Vector3 force) {
        velocity += force * mass * Time.fixedDeltaTime;
    }

    void Collision() {
        RaycastHit hit;
        bool bruh = Physics.CapsuleCast(transform.position + Vector3.up * 0.5f, transform.position + Vector3.down * 0.5f, 0.5f, velocity.normalized, out hit, velocity.magnitude * Time.fixedDeltaTime, shipLayer);
        if(bruh) {
            rb.MovePosition(rb.position + (velocity.normalized * (hit.distance - safety)));
            velocity = Vector3.ProjectOnPlane(velocity, hit.normal);
        }
        print(velocity.magnitude);
    }
}

