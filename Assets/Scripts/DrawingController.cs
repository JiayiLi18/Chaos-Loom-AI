using System.Collections;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class DrawingController : MonoBehaviour
{
    public GameObject[] linePrefabs; // 新线条的预制件
    private LineRenderer currentLine; // 当前正在绘制的线条

    private Vector3 previousPosition;
    private bool isDrawing = false;
    [SerializeField] float minDistance = 0.1f; // Minimum distance between points
    [SerializeField] float lineWidth = 0.1f; // Width of the line

    public AreaHandler areaHandler;
    public event Action OnDrawingStopped; // Event to notify when drawing stops

    void Update()
    {
        // 如果鼠标悬停在UI上，则不进行绘画
        if (EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (Input.GetMouseButtonDown(0)) // Start drawing
        {
            StartDrawing();
        }
        else if (Input.GetMouseButton(0)) // Continue drawing
        {
            ContinueDrawing();
        }
        else if (Input.GetMouseButtonUp(0)) // Stop drawing
        {
            StopDrawing();
        }
    }

    void StartDrawing()
    {
        Vector2 mousePos = Input.mousePosition; // 获取鼠标的屏幕坐标
        if (!areaHandler.IsPointInRect(mousePos))
        {
            Debug.Log("Mouse is not in the capture area.");
            Debug.Log(mousePos);
            return; // 如果不在 Rect 内，则不开始新线条
        }

        // 实例化一个新的线条对象
        int n = UnityEngine.Random.Range(0, linePrefabs.Length);
        GameObject linePrefab = linePrefabs[n];
        GameObject newLine = Instantiate(linePrefab, Vector3.zero, Quaternion.identity);
        currentLine = newLine.GetComponent<LineRenderer>();
        currentLine.startWidth = lineWidth;

        isDrawing = true;
        Vector3 mousePosition = GetMouseWorldPosition();
        previousPosition = mousePosition;
        // 初始化线条
        currentLine.positionCount = 0;
    }

    void ContinueDrawing()
    {
        if (!isDrawing) return;

        Vector3 mousePosition = GetMouseWorldPosition();
        if (Vector3.Distance(mousePosition, previousPosition) > minDistance) // Use the minDistance variable
        {
            AddPoint(mousePosition);
            previousPosition = mousePosition;
        }
    }

    void StopDrawing()
    {
        isDrawing = false;
        OnDrawingStopped?.Invoke(); // Trigger the event
        //ConvertLineToMesh();
    }

    public void ClearCanvas()
    {
        // 清空所有线条
        foreach (var line in GameObject.FindGameObjectsWithTag("DrawnLine"))
        {
            Destroy(line);
        }
    }

    void AddPoint(Vector3 point)
    {
        currentLine.positionCount++;
        currentLine.SetPosition(currentLine.positionCount - 1, point);
    }

    Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePosition = Input.mousePosition;
        mousePosition.z = 10; // Distance from the camera
        return Camera.main.ScreenToWorldPoint(mousePosition);
    }

    void ConvertLineToMesh()
    {
        if (currentLine.positionCount < 2) return; // Need at least 2 points to create a line

        GameObject lineObject = currentLine.gameObject;
        MeshFilter meshFilter = lineObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = lineObject.AddComponent<MeshRenderer>();
        MeshCollider meshCollider = lineObject.AddComponent<MeshCollider>();

        // Generate a mesh from the Line Renderer's points
        Mesh lineMesh = new Mesh();
        // The mesh is empty, need to assign vertices, triangles, etc. Need fact check.
        currentLine.BakeMesh(lineMesh, Camera.main, true); // Bake the Line Renderer into a mesh
        meshFilter.mesh = lineMesh;
        meshCollider.sharedMesh = lineMesh;

        // Assign a material (optional)
        meshRenderer.material = currentLine.material;

        // Optionally, add a Rigidbody for physics
        Rigidbody rb = lineObject.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true; // Set to false if you want the line to be affected by physics
    }
}
