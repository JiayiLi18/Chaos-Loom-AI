using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
public class ColorPickerUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject colorPickerPanel;
    private Slider hueSlider;        // name: hue 色相滑块
    private Slider saturationSlider; // name: saturation 饱和度滑块
    private Slider brightSlider;      // name: brightness 明度滑块
    [SerializeField] private Image colorPreview;      // name: colorPreview 颜色预览图片
    private Image saturationBackground; // name: background 饱和度滑块的背景图片

    [Header("Events")]
    public UnityEvent<Color32> onColorChanged; // 当颜色改变时触发的事件

    private float _hue = 0f;        // 0-1
    private float _saturation = 1f;  // 0-1
    private float _value = 1f;       // 0-1
    private bool _isInitialized = false;

    private void OnEnable()
    {
        if (!_isInitialized)
        {
            InitializeComponents();
        }
        if (colorPickerPanel != null)
        colorPickerPanel.SetActive(true);
    }

    private void InitializeComponents()
    {
        if (colorPickerPanel == null)
        {
            Debug.LogError("[ColorPickerTool] Color picker panel is not assigned!");
            enabled = false;
            return;
        }
        else
        {
            // 确保每个组件都被正确地找到
            if (hueSlider == null)
            {
                hueSlider = colorPickerPanel.transform.Find("hue").GetComponent<Slider>();
            }
            if (saturationSlider == null)
            {
                saturationSlider = colorPickerPanel.transform.Find("saturation").GetComponent<Slider>();
            }
            if (brightSlider == null)
            {
                brightSlider = colorPickerPanel.transform.Find("brightness").GetComponent<Slider>();
            }
            if (saturationSlider != null)
            {
                saturationBackground = saturationSlider.transform.Find("Background").GetComponent<Image>();
            }
            if (colorPreview == null)
            {
                colorPreview = colorPickerPanel.transform.Find("colorPreview").GetComponent<Image>();
            }

            // 设置滑块的初始值
            if (hueSlider != null)
            {
                hueSlider.value = _hue;
                hueSlider.onValueChanged.AddListener(OnHueChanged);
            }

            if (saturationSlider != null)
            {
                saturationSlider.value = _saturation;
                saturationSlider.onValueChanged.AddListener(OnSaturationChanged);
            }

            if (brightSlider != null)
            {
                brightSlider.value = _value;
                brightSlider.onValueChanged.AddListener(OnValueChanged);
            }

            UpdateColor();
            UpdateSaturationBackground();

            _isInitialized = true;
        }
    }

    private void OnDisable()
    {
        // 保持事件监听，因为可能会被重新启用
        if (colorPickerPanel != null)
        {
            colorPickerPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (hueSlider != null)
            hueSlider.onValueChanged.RemoveListener(OnHueChanged);
        if (saturationSlider != null)
            saturationSlider.onValueChanged.RemoveListener(OnSaturationChanged);
        if (brightSlider != null)
            brightSlider.onValueChanged.RemoveListener(OnValueChanged);
    }

    private void OnHueChanged(float value)
    {
        _hue = value;
        UpdateColor();
        UpdateSaturationBackground();
    }

    private void OnSaturationChanged(float value)
    {
        _saturation = value;
        UpdateColor();
    }

    private void OnValueChanged(float value)
    {
        _value = value;
        UpdateColor();
    }

    private void UpdateSaturationBackground()
    {
        if (saturationBackground != null)
        {
            saturationBackground.color = Color.HSVToRGB(_hue, 1f, 1f);
        }
    }

    private void UpdateColor()
    {
        Color newColor = Color.HSVToRGB(_hue, _saturation, _value);

        // 更新预览图片
        if (colorPreview != null)
        {
            colorPreview.color = newColor;
        }

        // 触发颜色改变事件
        onColorChanged?.Invoke(newColor);
    }

    // 获取当前选择的颜色
    public Color32 GetCurrentColor()
    {
        return Color.HSVToRGB(_hue, _saturation, _value);
    }

    // 设置当前颜色
    public void SetColor(Color32 color)
    {
        Color.RGBToHSV(color, out _hue, out _saturation, out _value);

        if (hueSlider != null) hueSlider.value = _hue;
        if (saturationSlider != null) saturationSlider.value = _saturation;
        if (brightSlider != null) brightSlider.value = _value;

        UpdateColor();
        UpdateSaturationBackground();
    }
}