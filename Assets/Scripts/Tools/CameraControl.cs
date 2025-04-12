using UnityEngine;

public class CameraControl : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float rotateSpeed = 100f;
    public float dragSpeed = 20f;
    public float zoomSpeed = 2f;

    private Vector3 dragOrigin;
    private bool isDragging = false;

    void Update()
    {
        HandleMovement();
        HandleMouseDrag();
        HandleZoom();
    }

    void HandleMovement()
    {
        Vector3 forwardMovement = Input.GetAxis("Vertical") * transform.forward;
        Vector3 rightMovement = Input.GetAxis("Horizontal") * transform.right;


        Vector3 verticalMovement = Vector3.zero;

        if (Input.GetKey(KeyCode.Space))
        {
            verticalMovement = Vector3.up * moveSpeed * 20f * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.LeftShift))
        {
            verticalMovement = Vector3.down * moveSpeed * 20f * Time.deltaTime;
        }

        Vector3 move = (forwardMovement + rightMovement + verticalMovement) * moveSpeed * Time.deltaTime;
        transform.position += move;

        transform.position += move;
    }

    void HandleMouseDrag()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            dragOrigin = Input.mousePosition;
        }

        if (Input.GetMouseButton(0) && isDragging)
        {
            Vector3 currentMousePos = Input.mousePosition;
            Vector3 difference = dragOrigin - currentMousePos;

            float rotationX = difference.y * dragSpeed * Time.deltaTime;
            float rotationY = -difference.x * dragSpeed * Time.deltaTime;

            transform.Rotate(Vector3.up, rotationY, Space.World);
            transform.Rotate(Vector3.right, rotationX);

            dragOrigin = currentMousePos;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
        transform.Translate(Vector3.forward * scroll, Space.Self);
    }
}
