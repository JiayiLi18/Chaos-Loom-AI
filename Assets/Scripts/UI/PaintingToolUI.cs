using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 绘制工具的UI控制器，负责处理所有UI交互和参数设置
/// </summary>
public class PaintingToolUI : MonoBehaviour
{
    [Header("系统引用")]
    private PaintingSystem paintingSystem;    // 引用核心绘制系统

    [Header("UI组件")]
    [SerializeField] private GameObject paintingToolPanel;
    //public RawImage previewImage; // 预览图，被runtimeVoxelTypeCreator调用作为最终的绘制结果储存
    [SerializeField] private Transform brushSizePreview;
    private Slider brushSizeSlider;           // 画笔大小滑块
    [SerializeField] private float minPreviewBrushSize = 0.2f;        // 预览最小画笔大小
    [SerializeField] private float maxPreviewBrushSize = 1.2f;          // 预览最大画笔大小
    [SerializeField] private float minActualBrushSize = 0.1f;        // 实际最小画笔大小
    [SerializeField] private float maxActualBrushSize = 2.0f;        // 实际最大画笔大小

    [Header("工具按钮")]
    private Button brushButton;               // 画笔按钮
    private Button eraserButton;              // 橡皮擦按钮

    [Header("选中状态颜色")]
    [SerializeField] private Color selectedToolColor = new Color(0.055f, 0.9f, 0.21f, 1f);    // 选中工具的颜色
    [SerializeField] private Color unselectedToolColor = new Color(0.23f, 0.23f, 0.23f, 1f);   // 未选中工具的颜色

    private bool _isEraser = false;          // 是否为橡皮擦模式
    private Color _lastPaintColor;           // 记录上次的绘制颜色
    private bool _isInitialized = false;

    void Start()
    {
        InitializeComponents();
    }

    private void OnEnable()
    {
        if (!_isInitialized)
        {
            InitializeComponents();
        }

        if (paintingSystem != null)
        {
            paintingSystem.enabled = true;
        }
        if (paintingToolPanel != null)
            paintingToolPanel.SetActive(true);

        UpdateToolSelected(_isEraser);
    }

    private void InitializeComponents()
    {
        if (paintingSystem == null)
        {
            paintingSystem = FindAnyObjectByType<PaintingSystem>();
            if (paintingSystem == null)
            {
                enabled = false;
                return;
            }
        }
        //通过名字获取所有UI组件
        //previewImage = paintingToolPanel.transform.Find("PreviewImage").GetComponent<RawImage>();
        brushButton = paintingToolPanel.transform.Find("BrushButton").GetComponent<Button>();
        eraserButton = paintingToolPanel.transform.Find("EraserButton").GetComponent<Button>();
        brushSizeSlider = paintingToolPanel.transform.Find("BrushSizeSlider").GetComponent<Slider>();
        if (brushSizePreview == null)
        {
            brushSizePreview = paintingToolPanel.transform.Find("BrushSizePreview");
        }

        // 初始化画笔大小滑块
        if (brushSizeSlider != null)
        {
            brushSizeSlider.onValueChanged.AddListener(OnBrushSizeChanged);
            // 立即应用当前画笔大小
            if (paintingSystem != null)
            {
                paintingSystem.SetBrushSize(brushSizeSlider.value);
            }
        }

        // 初始化工具按钮
        if (brushButton != null)
        {
            brushButton.onClick.AddListener(OnBrushSelected);
        }
        if (eraserButton != null)
        {
            eraserButton.onClick.AddListener(OnEraserSelected);
        }

        // 默认选择画笔工具
        _isEraser = false;
        //选择当前_isEraser对应状态的按钮
        UpdateToolSelected(_isEraser);
        _isInitialized = true;
    }

    private void OnDisable()
    {
        if (paintingSystem != null)
        {
            paintingSystem.SetEnabled(false);
            paintingSystem.enabled = false;
        }
        if (paintingToolPanel != null)
            paintingToolPanel.SetActive(false);
    }

    private void OnBrushSizeChanged(float value)
    {
        if (paintingSystem != null)
        {
            // 将0-1的slider值映射到实际画笔大小范围
            float actualSize = Mathf.Lerp(minActualBrushSize, maxActualBrushSize, value);
            paintingSystem.SetBrushSize(actualSize);
        }
        // 将0-1的slider值映射到预览大小范围
        if (brushSizePreview != null)
        {
            float previewScale = Mathf.Lerp(minPreviewBrushSize, maxPreviewBrushSize, value);
            brushSizePreview.localScale = new Vector3(previewScale, previewScale, previewScale);
        }
    }

    public void SetPaintColor(Color32 newColor)
    {
        if (!_isEraser)
        {
            _lastPaintColor = newColor;
            if (paintingSystem != null)
            {
                paintingSystem.SetPaintColor(newColor);
            }
        }
    }

    private void OnBrushSelected()
    {
        _isEraser = false;
        UpdateToolVisuals(false);
        if (paintingSystem != null)
        {
            paintingSystem.SetEnabled(true);
            paintingSystem.SetPaintColor(_lastPaintColor);
            float actualSize = Mathf.Lerp(minActualBrushSize, maxActualBrushSize, brushSizeSlider.value);
            paintingSystem.SetBrushSize(actualSize);
        }
    }

    private void OnEraserSelected()
    {
        _isEraser = true;
        UpdateToolVisuals(true);
        if (paintingSystem != null)
        {
            paintingSystem.SetEnabled(true);
            paintingSystem.SetPaintColor(new Color(0, 0, 0, 0));
        }

    }

    private void UpdateToolVisuals(bool isEraser)
    {
        if (brushButton.image != null)
        {
            brushButton.image.color = !isEraser ? selectedToolColor : unselectedToolColor;
        }
        if (eraserButton.image != null)
        {
            eraserButton.image.color = isEraser ? selectedToolColor : unselectedToolColor;
        }
        if (brushSizePreview != null)
        {
            float previewScale = Mathf.Lerp(minPreviewBrushSize, maxPreviewBrushSize, brushSizeSlider.value);
            brushSizePreview.localScale = new Vector3(previewScale, previewScale, previewScale);
        }

    }
    private void UpdateToolSelected(bool isEraser)
    {
        if (isEraser)
        {
            OnEraserSelected();
        }
        else
        {
            OnBrushSelected();
        }
    }
}