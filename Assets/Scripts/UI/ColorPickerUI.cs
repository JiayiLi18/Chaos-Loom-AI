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
    private Image colorPreview;      // name: colorPreview 颜色预览图片
    private Image saturationBackground; // name: background 饱和度滑块的背景图片
    private Button confirmButton;    // name: ConfirmBtn 确认按钮
    private Button resetButton;      // name: ResetBtn 重置按钮

    [Header("Events")]
    public UnityEvent<Color32> onColorChanged; // 当颜色改变时触发的事件
    public UnityEvent onColorConfirmed; // 当颜色确认时触发的事件
    public UnityEvent<Color32> onColorReset; // 当颜色重置时触发的事件（携带当前颜色）

    private float _hue = 0f;        // 0-1
    private float _saturation = 1f;  // 0-1
    private float _value = 1f;       // 0-1
    private bool _isInitialized = false;
    private bool _suppressEvents = false; // 抑制事件触发（程序化更新时使用）

    private void OnEnable()
    {
        if (!_isInitialized)
        {
            InitializeComponents();
        }
        // 不自动激活面板，由VoxelEditingUI控制显示状态
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
            if (confirmButton == null)
            {
                confirmButton = colorPickerPanel.transform.Find("confirmBtn").GetComponent<Button>();
            }
            if (resetButton == null)
            {
                resetButton = colorPickerPanel.transform.Find("resetBtn").GetComponent<Button>();
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

            // 设置按钮事件监听器
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnConfirmButtonClicked);
            }
            if (resetButton != null)
            {
                resetButton.onClick.AddListener(OnResetButtonClicked);
            }

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
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmButtonClicked);
        if (resetButton != null)
            resetButton.onClick.RemoveListener(OnResetButtonClicked);
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
        if (!_suppressEvents)
        {
            onColorChanged?.Invoke(newColor);
        }
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

    // 静默设置颜色（不触发 onColorChanged），用于程序化重置/初始化
    public void SetColorSilently(Color32 color)
    {
        Color.RGBToHSV(color, out _hue, out _saturation, out _value);

        _suppressEvents = true;
        if (hueSlider != null) hueSlider.SetValueWithoutNotify(_hue);
        if (saturationSlider != null) saturationSlider.SetValueWithoutNotify(_saturation);
        if (brightSlider != null) brightSlider.SetValueWithoutNotify(_value);

        // 直接更新预览和背景
        if (colorPreview != null) colorPreview.color = Color.HSVToRGB(_hue, _saturation, _value);
        UpdateSaturationBackground();
        _suppressEvents = false;
    }

    /// <summary>
    /// 设置颜色选择器面板的激活状态
    /// </summary>
    public void SetPanelActive(bool active)
    {
        if (colorPickerPanel != null)
        {
            colorPickerPanel.SetActive(active);
        }
    }

    // 按钮事件处理方法
    private void OnConfirmButtonClicked()
    {
        Debug.Log("[ColorPickerUI] Confirm button clicked");
        onColorConfirmed?.Invoke();
    }

    private void OnResetButtonClicked()
    {
        Debug.Log("[ColorPickerUI] Reset button clicked");
        onColorReset?.Invoke(GetCurrentColor());
    }
}