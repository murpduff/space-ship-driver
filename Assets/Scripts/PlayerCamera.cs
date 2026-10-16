using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour {
    [SerializeField] float mouseSensitivity;
    [SerializeField] Transform player;
    [SerializeField] Transform ship;
    public Vector3 offset;
    [SerializeField] float maxInteractDst;
    [SerializeField] LayerMask interactable;
    public Vector3 orientation;
    PlayerMovement playerMovement;
    ShipMovement shipMovement;
    Vector2 lookInput;
    float xRotation = 0f;
    float yRotation = 0f;
    Camera mainCamera;

    void Start() {
        Cursor.lockState = CursorLockMode.Locked;
        playerMovement = FindAnyObjectByType<PlayerMovement>();
        shipMovement = FindAnyObjectByType<ShipMovement>();
        mainCamera = Camera.main;
    }

    void Update() {
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        xRotation -= mouseY;
        yRotation += mouseX;
        xRotation = Mathf.Clamp(xRotation, -89f, 89f);
        transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);
        //transform.Rotate(Vector3.up * mouseX);

        orientation = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;

        transform.position = player.position + offset;

        MainCameraView();
    }

    void MainCameraView() {
        mainCamera.transform.localRotation = transform.rotation;
        if(playerMovement.inSpace){
            mainCamera.transform.parent = ship;
            mainCamera.transform.localPosition = Vector3.zero;
        } else {
            mainCamera.transform.parent = null;
            mainCamera.transform.position = transform.position;
        }
    }

    void OnLook(InputValue value) {
        lookInput = value.Get<Vector2>();
    }

    void OnInteract(InputValue value) {
        if (value.isPressed) {
            RaycastHit hit;
            if(Physics.Raycast(transform.position, transform.forward, out hit, maxInteractDst, interactable)) {
                if(hit.transform.name == "Chair") {
                    playerMovement.drivingShip = true;
                }
                if(hit.transform.name == "StabilizeButton") {
                    shipMovement.stabilization = !shipMovement.stabilization;
                }
            }
        }
    }
}
