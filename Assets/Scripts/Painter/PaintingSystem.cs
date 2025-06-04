using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 核心绘制系统，提供基础的绘制功能接口
/// </summary>
public class PaintingSystem : MonoBehaviour
{
    [Header("Core Settings")]
    public Camera paintCamera;
    public bool isEnabled { get; private set; } = false;

    [Header("Paint Parameters")]
    private Color _paintColor = Color.white;
    private float _radius = 1f;
    private float _strength = 1f;
    private float _hardness = 1f;

    [Header("Layer Settings")]
    public LayerMask paintingLayer;

    public void SetEnabled(bool enabled)
    {
        isEnabled = enabled;
    }

    public void SetPaintColor(Color color)
    {
        _paintColor = color;
    }

    public void SetBrushSize(float size)
    {
        _radius = size;
    }

    public void SetBrushStrength(float strength)
    {
        _strength = strength;
    }

    public void SetBrushHardness(float hardness)
    {
        _hardness = hardness;
    }

    void Update()
    {
        if (!isEnabled || EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (Input.GetMouseButton(0))
        {
            Paint();
        }
        //TODO:加入选择鼠标绘制还是collision绘制的功能，参考collisionPainter
    }

    private void Paint()
    {
        Vector3 position = Input.mousePosition;
        if (paintCamera == null)
        {
            Debug.LogError("paintCamera is not assigned");
            return;
        }
        Ray ray = paintCamera.ScreenPointToRay(position);
        
        if (Physics.Raycast(ray, out RaycastHit hit, 100.0f, paintingLayer))
        {
            transform.position = hit.point;
            
            Paintable paintable = hit.collider.GetComponent<Paintable>();
            if (paintable != null)
            {
                PaintManager.instance.paint(paintable, hit.point, _radius, _hardness, _strength, _paintColor);
            }
        }
    }
} 