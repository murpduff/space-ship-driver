using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class JetMovement : MonoBehaviour {
    public PlayerCamera playerCam;
    public PlayerMovement playerMovement;
    public bool jetMode;
    public float thrust;
    public Vector2 torque;
    public float GForceEffect;

    [Header("Jet Stats")]
    public float maxJetHealth;
    public float minDmgImp;
    public float jetHealth;

    [Header("Lift Parameters")]
    public float maxLift;
    public float wingLiftSpeed;
    public float lift;
    public Vector3 linearDrag;
    public float wingLiftTorque;
    public float stabilityTorque;

    [Header("Input Parameters")]
    public Vector2 horizontalInput;
    public float verticalInput;
    
    [Header("Output")]
    public Rigidbody rb;
    public float velocity;
    public Vector3 acceleration;
    public TextMeshPro speedDisplay;
    public float jolt;
    Vector3 prevVel;
    Vector3 camOffset = Vector3.zero;

    public GameObject dead;

    void Start() {
        dead.SetActive(false);
        jetHealth = maxJetHealth;
    }
    void Update() {
        speedDisplay.text = Mathf.Round(velocity).ToString() + " km/h";

        if(jetHealth <= 0) {
            print("you have died");
            dead.SetActive(true);
            Application.Quit();
        }
    }

    void FixedUpdate() {
        if (playerMovement.jetMode) {
            //thrust
            if(horizontalInput.y >= 0.01){
                rb.AddForce(transform.forward * thrust);
            }

            //spinny
            rb.AddTorque(transform.up * horizontalInput.x * torque.x);
            rb.AddTorque(-transform.forward * horizontalInput.x * wingLiftTorque);

            //stabilize
            float currentRoll = transform.eulerAngles.z;
            if (currentRoll > 180) currentRoll -= 360;

            if(Mathf.Abs(horizontalInput.x) < 0.01f) rb.AddTorque(transform.forward * -currentRoll * stabilityTorque);

            //lift
            if(verticalInput > 0.01) {
                lift += wingLiftSpeed * Time.deltaTime;
                rb.AddTorque(transform.right * -torque.y);
            }else if(verticalInput < -0.01) {
                lift -= wingLiftSpeed * Time.deltaTime;
                rb.AddTorque(transform.right * torque.y);
            } else{
                lift -= wingLiftSpeed * Time.deltaTime;
                if(lift < 0) lift = 0;
            }
            if(lift > maxLift) lift = maxLift;
            if(lift < -maxLift) lift = -maxLift;

            rb.AddForce(transform.up * lift);

            CameraJolt();
        } else {
            playerCam.offset = new Vector3(0, 1, 0);
        }

        Vector3 lDragX = -transform.right * Vector3.Dot(transform.right, rb.linearVelocity) * linearDrag.x;
        Vector3 lDragY = -transform.up * Vector3.Dot(transform.up, rb.linearVelocity) * linearDrag.y;
        Vector3 lDragZ = -transform.forward * Vector3.Dot(transform.forward, rb.linearVelocity) * linearDrag.z;
        rb.AddForce(lDragX + lDragY + lDragZ);

        velocity = rb.linearVelocity.magnitude / 1000f * 3600;

        Vector3 acc = (rb.linearVelocity - prevVel) / Time.fixedDeltaTime;
        float accX = Mathf.Round(Vector3.Dot(transform.right, acc));
        float accY = Mathf.Round(Vector3.Dot(transform.up, acc));
        float accZ = Mathf.Round(Vector3.Dot(transform.forward, acc));
        acceleration = new Vector3(accX, accY, accZ);
        
        prevVel = rb.linearVelocity;
    }

    void OnCollisionEnter(Collision collision) {
        float impulse = collision.impulse.magnitude;
        if(impulse >= minDmgImp) {
            jetHealth -= impulse;
        }
    }

    void CameraJolt() {
        camOffset += -((acceleration / 9.8f) * GForceEffect + camOffset) * jolt;
        Vector3 camX = transform.right * camOffset.x;
        Vector3 camY = transform.up * camOffset.y + transform.up;
        Vector3 camZ = transform.forward * camOffset.z;
        playerCam.offset = camX + camY + camZ;
    }

    void OnHorizontalMovement(InputValue value) {
        horizontalInput = value.Get<Vector2>();
    }
    void OnVerticalMovement(InputValue value) {
        verticalInput = value.Get<float>();
    }
}
