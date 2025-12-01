using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InputFieldExpandUI : MonoBehaviour
{
    [Header("Refs")]
    public TMP_InputField input;              // 你的 TMP_InputField
    public RectTransform inputContainer;      // 输入框外层容器（我们改它的尺寸/Top）
    public RectTransform logPanel;            // 上方的面板（聊天记录区，要被顶上去）

    [Header("Size")]
    public float minHeight = 20f;     // 输入框最小高度
    public float maxHeight = 220f;    // 输入框最大高度
    public float verticalPadding = 0f; // 输入文字上下内边距（根据你的背景、字体调）

    [Header("Offsets (baselines)")]
    public float inputTopBase = 22f;    // 输入框“最低”时的 Top 基线（通常 0）
    public float panelTopBase = 0f; // 面板与输入框之间的间距基线（例如 8）

    TMP_Text textComp;
    bool isSelected = false;
    float lastHeight = 0f;

    void Awake()
    {
        if (input == null) input = GetComponent<TMP_InputField>();
        textComp = input.textComponent;
        // 监听输入变化
        input.onSelect.AddListener(_ => 
        {
            isSelected = true;
            Recalc();
        });
        input.onDeselect.AddListener(_ => isSelected = false);
        input.onValueChanged.AddListener(_ => Recalc());
        input.onEndEdit.AddListener(_ => 
        {
            isSelected = false;
            Recalc();
        });
    }

    void OnEnable() => Recalc();

    void Update()
    {
        // 选中时持续检查高度变化
        if (isSelected && input != null && textComp != null)
        {
            // 计算当前应该的高度
            float currentHeight = Mathf.Clamp(textComp.preferredHeight + verticalPadding, minHeight, maxHeight);
            
            // 如果高度发生变化，重新计算布局
            if (Mathf.Abs(currentHeight - lastHeight) > 0.1f)
            {
                lastHeight = currentHeight;
                Recalc();
            }
        }
    }

    void Recalc()
    {
        // 1) 计算文本需要的高度
        // preferredHeight 是文字在给定宽度下的理想高度
        float h = Mathf.Clamp(textComp.preferredHeight + verticalPadding, minHeight, maxHeight);
        
        // 更新缓存的高度
        lastHeight = h;

        // 2) 按高度设置 Top（Inspector 里看到的 Top 值）
        SetTop(inputContainer, inputTopBase - h); // "Top = 基线 + 当前高度"，会向上长

        // 3) 把上方面板往上顶：设置它的 Top = 输入框高度 + 基础间距
        SetTop(logPanel, panelTopBase - h);
    }

    // —— RectTransform 工具 —— 
    // Inspector 里的 Top/Bottom 就是 offsetMax.y / offsetMin.y 的包装：
    static void SetTop(RectTransform rt, float top)
    {
        var off = rt.offsetMax; // 右上角
        off.y = -top;           // Top 数值在代码里是 offsetMax.y 的相反数
        rt.offsetMax = off;
    }
}
