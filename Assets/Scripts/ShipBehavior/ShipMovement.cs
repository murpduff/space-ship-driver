using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class ShipMovement : MonoBehaviour
{   
    [SerializeField] public Planet targetPlanet;
    [SerializeField] float shipSpeed;
    [SerializeField] float turnSpeed;
    [SerializeField] public bool stabilization;
    [SerializeField] public bool setDirToTarget;
    [SerializeField] float rotationStabilize;
    [SerializeField] float rotationDamping;
    PlayerMovement playerMovement;
    [System.NonSerialized] public Rigidbody rb;
    public Vector3 acceleration;
    public Vector3 worldPos; // 1 : 1000, worldPos : worldTransform
    Vector3 lastVelocity;
    Vector3 moveInput;
    float thrust;
    int reverse;

    void Start() {
        rb = GetComponent<Rigidbody>();
        playerMovement = FindAnyObjectByType<PlayerMovement>();
    }

    void Update() {
        reverse = 1;
        if(moveInput.z == -1) {
            reverse = -1;
        }

        worldPos = transform.position / 1000f;
    }
    void FixedUpdate() {
        acceleration = (rb.linearVelocity - lastVelocity) / Time.fixedDeltaTime;

        if(stabilization) {
            //rb.AddTorque(-rb.angularVelocity * rotationStabilize);
            ApplyTorqueToLookAt(rb.linearVelocity.normalized, rb, rotationStabilize, rotationDamping);
        }
        if (setDirToTarget) {
           ApplyTorqueToLookAt((targetPlanet.worldPos - worldPos).normalized, rb, rotationStabilize, rotationDamping); 
        }

        if(!playerMovement.drivingShip) return;

        rb.AddRelativeTorque(moveInput * turnSpeed);
        rb.AddRelativeForce(Vector3.forward * thrust * shipSpeed);

        lastVelocity = rb.linearVelocity;
    }

    void ApplyTorqueToLookAt(Vector3 targetDirection, Rigidbody rb, float torqueForce, float damping) {
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        Quaternion deltaRotation = targetRotation * Quaternion.Inverse(rb.rotation);

        deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
        if(angle > 180f) angle -= 360f;
        if(Mathf.Abs(angle) > 0.01f) {
            Vector3 torque = axis.normalized * angle * torqueForce;
            rb.AddTorque(torque - rb.angularVelocity * damping);
        }
    }

    public void ToggleStabilization() {
        stabilization = !stabilization;
    }

    void OnRotate(InputValue value) {
        moveInput = value.Get<Vector3>();
    }
    void OnThrust(InputValue value) {
       thrust = value.Get<float>(); 
    }
    void OnReset() {
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}
