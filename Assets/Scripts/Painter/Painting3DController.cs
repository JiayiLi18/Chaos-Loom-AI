using System.Collections;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class Painting3DController : MonoBehaviour
{
    public GameObject[] linePrefabs; // 新线条的预制件
    private LineRenderer currentLine; // 当前正在绘制的线条
    [SerializeField] Color currentColor = Color.white; // Current color for the line
    [SerializeField] Color selectedColor = Color.yellow; // Color for selected line

    private Vector3 previousPosition;
    private bool isDrawing = false;
    private LineRenderer selectedLine = null; // 当前选中的线条

    public bool canDraw = true;
    [SerializeField] float minDistance = 0.1f; // Minimum distance between points
    [SerializeField] float lineWidth = 0.1f; // Width of the line
    [SerializeField] float distanceToCam = 1.5f;
    [SerializeField] int maxLineNumber = 100; //to restrict the number of lines, in case too many lines
    [SerializeField] int lineCount = 0;
    [SerializeField] GameObject lineParent;

    public GameObject penObject;
    public event Action OnDrawingStopped; // Event to notify when drawing stops

    void Update()
    {   //if player pick up the pencil
        if (canDraw)
        {
            // 如果鼠标悬停在UI上，则不进行绘画
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            // 左键绘画
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

            // 右键选择和删除线条
            if (Input.GetMouseButton(1))
            {
                SelectLine();
            }
            else if (Input.GetMouseButtonUp(1))
            {
                DeleteSelectedLine();
            }

            //Adjust distance to draw
            if (Input.GetKey(KeyCode.Q))
            {
                distanceToCam += Time.deltaTime*2f;
                distanceToCam = Math.Clamp(distanceToCam, 0.7f, 3.5f);
            }
            if (Input.GetKey(KeyCode.E))
            {
                distanceToCam -=Time.deltaTime*2f;
                distanceToCam = Math.Clamp(distanceToCam, 0.7f, 3.5f);
            }


            if (penObject != null)
            {
                Vector3 mousePos = Input.mousePosition;
                mousePos.z = distanceToCam;
                Debug.Log("Mouse Position: " + mousePos);

                Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);
                Debug.Log("World Position: " + worldPos);

                penObject.transform.position=worldPos; // 确保使用世界坐标
            }
        }
    }

    void SelectLine()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = distanceToCam;
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);

        // 获取所有线条
        LineRenderer[] allLines = lineParent.GetComponentsInChildren<LineRenderer>();
        float closestDistance = float.MaxValue;
        LineRenderer closestLine = null;

        foreach (LineRenderer line in allLines)
        {
            // 检查线条的每个点
            for (int i = 0; i < line.positionCount; i++)
            {
                Vector3 point = line.GetPosition(i);
                float distance = Vector3.Distance(worldPos, point);

                // 如果点距离鼠标位置小于线条宽度，认为选中了这条线
                if (distance < lineWidth * 2)
                {
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestLine = line;
                    }
                }
            }
        }

        // 如果找到了最近的线条
        if (closestLine != null)
        {
            // 检查是否超过最大选择距离
            if (closestDistance > lineWidth * 5) // 可以调整这个倍数来改变最大选择距离
            {
                // 如果超过最大距离，取消选择
                if (selectedLine != null)
                {
                    selectedLine.startColor = currentColor;
                    selectedLine.endColor = currentColor;
                    selectedLine = null;
                }
                return;
            }

            // 取消之前选中的线条高亮
            if (selectedLine != null)
            {
                selectedLine.startColor = currentColor;
                selectedLine.endColor = currentColor;
            }

            // 高亮新选中的线条
            selectedLine = closestLine;
            selectedLine.startColor = selectedColor;
            selectedLine.endColor = selectedColor;
        }
        else
        {
            // 如果没有找到任何线条，取消当前选择
            if (selectedLine != null)
            {
                selectedLine.startColor = currentColor;
                selectedLine.endColor = currentColor;
                selectedLine = null;
            }
        }
    }

    void DeleteSelectedLine()
    {
        if (selectedLine != null)
        {
            Destroy(selectedLine.gameObject);
            lineCount--;
            selectedLine = null;
        }
    }

    void StartDrawing()
    {
        // 检查是否达到最大线条数量限制
        if (lineCount >= maxLineNumber)
        {
            Debug.Log("已达到最大线条数量限制！");
            return;
        }

        /*Vector2 mousePos = Input.mousePosition; // 获取鼠标的屏幕坐标
        if (!areaHandler.IsPointInRect(mousePos)) // 如果不在 Rect 内，则不开始新线条
        {
            Debug.Log("Mouse is not in the capture area.");
            Debug.Log(mousePos);
            return; 
        }*/

        // 实例化一个新的线条对象
        int n = UnityEngine.Random.Range(0, linePrefabs.Length);
        GameObject linePrefab = linePrefabs[n];
        GameObject newLine = Instantiate(linePrefab, Vector3.zero, Quaternion.identity, lineParent.transform);
        currentLine = newLine.GetComponent<LineRenderer>();
        currentLine.startWidth = lineWidth;//control the width
        currentLine.startColor = currentColor;
        currentLine.endColor = currentColor;

        lineCount++;

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
        lineCount = 0; // 重置线条计数
    }

    void AddPoint(Vector3 point)
    {
        currentLine.positionCount++;
        currentLine.SetPosition(currentLine.positionCount - 1, point);
    }

    Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePosition = Input.mousePosition;
        // 将鼠标位置设置为摄像机前方固定距离
        mousePosition.z = distanceToCam;
        // 将屏幕坐标转换为世界坐标
        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mousePosition);
        return worldPosition;
    }

    // Add new method to change color
    public void SetLineColor(Color newColor)
    {
        currentColor = newColor;
        if (currentLine != null)
        {
            currentLine.startColor = newColor;
            currentLine.endColor = newColor;
        }
    }

    /*void ConvertLineToMesh()
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
    }*/

}
