using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 绘制工具的UI控制器，负责处理所有UI交互和参数设置
/// </summary>
public class PaintingToolUI : MonoBehaviour
{
    [Header("系统引用")]
    [SerializeField] private GameObject paintableCube; //TODO: 3D改各个面颜色
    [SerializeField] private ColorPickerUI colorPicker; // 颜色选择器引用

    [Header("UI组件")]
    [SerializeField] private GameObject paintingToolPanel;
    //TODO: 3D改各个面颜色

    [Header("选中状态颜色")]
    [SerializeField] private Color selectedToolColor = new Color(0.055f, 0.9f, 0.21f, 1f);    // 选中工具的颜色
    [SerializeField] private Color unselectedToolColor = new Color(0.23f, 0.23f, 0.23f, 1f);   // 未选中工具的颜色

    private Color _lastPaintColor = Color.black;           // 记录上次的绘制颜色，默认为黑色
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

        if (paintingToolPanel != null)
            paintingToolPanel.SetActive(true);
        if (paintableCube != null)
            paintableCube.SetActive(true);
    }

    private void InitializeComponents()
    {
        // 初始化ColorPicker
        if (colorPicker == null)
        {
            Debug.LogError("[PaintingToolUI] ColorPickerUI not found!");
            enabled = false;
            return;
        }
        colorPicker.onColorChanged.AddListener(SetPaintColor);

        //通过名字获取所有UI组件

        _isInitialized = true;
    }

    private void OnDisable()
    {
        if (paintingToolPanel != null)
            paintingToolPanel.SetActive(false);
        if (paintableCube != null)
            paintableCube.SetActive(false);
    }

    public void SetPaintColor(Color32 newColor)
    {
            _lastPaintColor = newColor;
    }

}