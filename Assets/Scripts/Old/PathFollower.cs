using UnityEngine;

public class PathFollower : MonoBehaviour
{
    public LineRenderer path; // Reference to the Line Renderer
    public float speed = 5f; // Speed of movement
    public float rotationSpeed = 10f; // Speed of rotation

    private int currentPointIndex = 0; // Current point on the path
    private Vector3 targetPosition; // Next position to move towards
    private bool isMoving = false;

    void Start()
    {
        // Find the DrawingController in the scene and subscribe to its event
        DrawingController drawingController = FindFirstObjectByType<DrawingController>();
        if (drawingController != null)
        {
            drawingController.OnDrawingStopped += StartMoving;
        }
    }

    void Update()
    {
        if (!isMoving) return;

        // Move the object towards the target position
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        // Rotate the object to face the direction of movement
        if (transform.position != targetPosition)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Check if the object has reached the target position
        if (transform.position == targetPosition)
        {
            // Move to the next point on the path
            currentPointIndex++;
            if (currentPointIndex < path.positionCount)
            {
                targetPosition = path.GetPosition(currentPointIndex);
            }
            else
            {
                // Stop moving when the end of the path is reached
                isMoving = false;
            }
        }
    }

    // Call this method to start moving the object along the path
    public void StartMoving()
    {
        if (path.positionCount > 0)
        {
            currentPointIndex = 0;
            targetPosition = path.GetPosition(0);
            isMoving = true;
        }
    }
}