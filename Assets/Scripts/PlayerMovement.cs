using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] float walkSpeed;
    [SerializeField] float sprintSpeed;
    [SerializeField] float drag;
    [SerializeField] float jumpSpeed;
    [SerializeField] LayerMask ground;
    [SerializeField] Vector3 driverPosition;
    [SerializeField] Transform jetTransform;
    public bool drivingShip;
    PlayerCamera playerCam;
    public Vector2 moveInput;
    Vector2 horizontalVelocity;
    public bool jump;
    bool crouch;
    bool sprint;
    public bool inSpace = true;
    public bool jetMode = false; //this is probably temporary
    [SerializeField] float jetLift;
    [SerializeField] float jetSpeed;
    [SerializeField] float maxJetVelocity;
    [SerializeField] Vector3 velocity;
    [SerializeField] Vector2 maxDstFromOrigin;
    public float ejectForce;
    Camera mainCamera;
    float speed;

    public Rigidbody rb;
    void Start() {
        playerCam = FindAnyObjectByType<PlayerCamera>();
        mainCamera = Camera.main;
    }

    public void Spawn(Vector3 position) {
        rb.position = position;
        jetTransform.GetComponent<Rigidbody>().position = position;
    }

    void Update() {
        if (drivingShip) {
            transform.position = driverPosition;
            rb.linearVelocity = Vector3.zero;
        }else if(!jetMode){
            transform.rotation = Quaternion.LookRotation(playerCam.orientation);

            horizontalVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            if(horizontalVelocity.magnitude > walkSpeed) {
                horizontalVelocity = horizontalVelocity.normalized * walkSpeed;
                rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.y);
            }
        }

        if (jetMode) {
            transform.position = jetTransform.position;
            transform.parent = jetTransform;
            mainCamera.transform.parent = jetTransform;
            rb.isKinematic = true;
            
            // speed = jetSpeed;
            // if(rb.linearVelocity.magnitude > maxJetVelocity) {
            //     rb.linearVelocity = rb.linearVelocity.normalized * maxJetVelocity;
            // }

            // if(maxDstFromOrigin != Vector2.zero){ //if 0 then assume disabled
            //     if(Mathf.Abs(transform.position.x) > maxDstFromOrigin.x) {
            //         int dir = Mathf.RoundToInt(Mathf.Abs(transform.position.x) / transform.position.x);

            //         rb.position = new Vector3(0, transform.position.y, transform.position.z);
            //         ShiftTerrainPositions(new Vector2(-dir * maxDstFromOrigin.x, 0));
            //     }
            //     if(Mathf.Abs(transform.position.z) > maxDstFromOrigin.y) {
            //         int dir = Mathf.RoundToInt(Mathf.Abs(transform.position.z) / transform.position.z);

            //         rb.position = new Vector3(-transform.position.x, transform.position.y, 0);
            //         ShiftTerrainPositions(new Vector2(0, -dir * maxDstFromOrigin.y));
            //     }
            // }

            // void ShiftTerrainPositions(Vector2 offset) {
            //     SurfaceTerrainGeneration terrainGen = FindAnyObjectByType<SurfaceTerrainGeneration>();
            //     terrainGen.ShiftOriginPosition(offset);
            // }

        } else {
            if (sprint) {
                speed = sprintSpeed;
            }else {
                speed = walkSpeed;
            }
        }


    }
    // Update is called once per frame
    void FixedUpdate() {
        velocity = rb.linearVelocity;

        if (jetMode) {
            if (jump) {
                rb.AddForce(transform.up * jetLift);
            }
            if (crouch) {
                rb.AddForce(transform.up * -jetLift);
            }
            rb.AddForce((moveInput.y * mainCamera.transform.forward).normalized * speed);
        } else {
            if(Physics.Raycast(transform.position, -transform.up, 1.1f, ground)) {
                if (jump) {
                    rb.AddForce(transform.up * jumpSpeed, ForceMode.Impulse);
                }
            }
            rb.AddForce((transform.forward * moveInput.y + transform.right * moveInput.x).normalized * speed, ForceMode.Impulse);
            rb.AddForce(-new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z) * drag);
        }
    }

    void OnMove(InputValue value) {
        moveInput = value.Get<Vector2>();
    }

    void OnJump(InputValue value) {
        jump = value.isPressed;
    }
    void OnCrouch(InputValue value) {
        crouch = value.isPressed;
    }
    void OnSprint(InputValue value) {
        sprint = value.isPressed;
    }

    void OnEject(InputValue value) {
        if (value.isPressed && jetMode) {
            rb.isKinematic = false;
            jetMode = false;
            transform.parent = null;
            rb.AddForce(transform.up * ejectForce, ForceMode.Impulse);
        }
    }

    void OnDisengage(InputValue value) {
        if (value.isPressed) {
            if(drivingShip){
                drivingShip = false;
            }
        }
    }
}
