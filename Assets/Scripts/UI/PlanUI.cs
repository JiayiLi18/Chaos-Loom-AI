using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 管理 Plan Batch 的 UI 显示
/// </summary>
public class PlanUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text talkToPlayerText;
    [SerializeField] private TMP_Text goalLabelText;
    [SerializeField] private Transform planListParent; // Plan 列表的父物体（直接拖拽场景中的实例）
    [SerializeField] private GameObject planItemPrefab; // 单个 Plan Item 的预制体

    [Header("Feedback Settings")]
    [SerializeField] private Button sendFeedbackButton;
    [SerializeField] private TMP_InputField feedbackInputField;
    
    [Header("Plan Item UI Paths")]
    [SerializeField] private string toggleButtonPath = "ToggleButton"; // Toggle 按钮路径
    [Tooltip("The name of the UI element in the plan item prefab - must match exactly")]
    [SerializeField] private string backgroundImagePath = "BackgroundImage"; // 背景图片路径
    [SerializeField] private string idTextPath = "IDText"; // ID 文本路径
    [SerializeField] private string actionTypeTextPath = "ActionTypeText"; // Action Type 文本路径
    [SerializeField] private string descriptionTextPath = "DescriptionText"; // Description 文本路径
    [SerializeField] private string dependsOnTextPath = "DependsOnText"; // Depends On 文本路径

    private PlanBatch _currentPlanBatch;
    private Dictionary<string, PlanItemData> _planItems; // 存储每个 plan item 的数据和实例
    private const float DISABLED_ALPHA = 0.4f; // 取消状态的透明度
    
    // 添加引用
    private RuntimeAIChat runtimeAIChat;
    private GameStateManager gameStateManager;
    private ChatUI chatUI;
    
    /// <summary>
    /// Plan Item 的数据结构
    /// </summary>
    private class PlanItemData
    {
        public GameObject instance;
        public PlanItem planItem;
        public bool isConfirmed; // true = 确认, false = 取消
        public Toggle toggleButton;
        public Image backgroundImage;
        
        public PlanItemData(GameObject instance, PlanItem planItem)
        {
            this.instance = instance;
            this.planItem = planItem;
            this.isConfirmed = true; // 默认为确认状态
        }
    }

    private void Awake()
    {
        InitializeReferences();
        if (sendFeedbackButton != null)
        {
            sendFeedbackButton.onClick.AddListener(OnSendFeedback);
        }
    }

    private void InitializeReferences()
    {
        // 获取 RuntimeAIChat 引用
        if (runtimeAIChat == null)
        {
            runtimeAIChat = FindAnyObjectByType<RuntimeAIChat>();
            if (runtimeAIChat == null)
            {
                Debug.LogWarning("PlanUI: cannot find RuntimeAIChat component");
            }
        }

        // 获取 GameStateManager 引用
        if (gameStateManager == null)
        {
            gameStateManager = FindAnyObjectByType<GameStateManager>();
            if (gameStateManager == null)
            {
                Debug.LogWarning("PlanUI: cannot find GameStateManager component");
            }
        }

        // 获取 ChatUI 引用（用于显示等待消息）
        if (chatUI == null)
        {
            chatUI = FindAnyObjectByType<ChatUI>();
            if (chatUI == null)
            {
                Debug.LogWarning("PlanUI: cannot find ChatUI component, cannot display waiting message");
            }
        }
    }

    /// <summary>
    /// 显示 Plan Batch
    /// </summary>
    public void DisplayPlanBatch(PlanBatch planBatch)
    {
        _currentPlanBatch = planBatch;

        // 显示对玩家说的话
        if (talkToPlayerText != null)
        {
            talkToPlayerText.text = planBatch.talk_to_player;
        }
        
        // 显示目标标签
        if (goalLabelText != null)
        {
            goalLabelText.text = $"Goal: {planBatch.goal_label}";
        }

        // 清空现有的 plan items
        ClearPlanItems();

        // 创建每个 plan item
        if (planBatch.plan != null && planBatch.plan.Length > 0)
        {
            if (planListParent == null)
            {
                Debug.LogError("PlanUI: planListParent not assigned. Cannot create plan items.");
                return;
            }

            _planItems = new Dictionary<string, PlanItemData>();
            
            for (int i = 0; i < planBatch.plan.Length; i++)
            {
                CreatePlanItem(planBatch.plan[i], i);
            }
        }
    }

    /// <summary>
    /// 创建单个 Plan Item
    /// </summary>
    private void CreatePlanItem(PlanItem planItem, int index)
    {
        if (planItemPrefab == null || planListParent == null)
        {
            Debug.LogError("PlanUI: planItemPrefab or planListParent is not assigned");
            return;
        }

        GameObject instance = Instantiate(planItemPrefab, planListParent);
        PlanItemData itemData = new PlanItemData(instance, planItem);
        
        // 查找并获取组件
        Toggle toggleButton = FindChildInPrefab(instance, toggleButtonPath)?.GetComponent<Toggle>();
        Image backgroundImage = FindChildInPrefab(instance, backgroundImagePath)?.GetComponent<Image>();
        
        itemData.toggleButton = toggleButton;
        itemData.backgroundImage = backgroundImage;
        
        // 设置文本
        TMP_Text idText = FindChildInPrefab(instance, idTextPath)?.GetComponent<TMP_Text>();
        TMP_Text actionTypeText = FindChildInPrefab(instance, actionTypeTextPath)?.GetComponent<TMP_Text>();
        TMP_Text descriptionText = FindChildInPrefab(instance, descriptionTextPath)?.GetComponent<TMP_Text>();
        TMP_Text dependsOnText = FindChildInPrefab(instance, dependsOnTextPath)?.GetComponent<TMP_Text>();

        if (idText != null)
        {
            string planSequenceNumber = ExtractPlanSequenceNumber(planItem.id);
            // 转换为整数去掉前导 0
            if (int.TryParse(planSequenceNumber, out int number))
            {
                idText.text = number.ToString();
            }
            else
            {
                idText.text = planSequenceNumber;
            }
        }

        if (actionTypeText != null)
        {
            // 去掉下划线并转为小写空格分隔
            string formattedType = planItem.action_type.Replace("_", " ");
            actionTypeText.text = formattedType;
        }

        if (descriptionText != null)
        {
            descriptionText.text = planItem.description;
        }

        if (dependsOnText != null)
        {
            if (planItem.depends_on != null && planItem.depends_on.Length > 0)
            {
                // 提取每个依赖的序号（去掉前导 0）
                string[] dependSequences = new string[planItem.depends_on.Length];
                for (int i = 0; i < planItem.depends_on.Length; i++)
                {
                    string seq = ExtractPlanSequenceNumber(planItem.depends_on[i]);
                    // 转换为整数去掉前导 0
                    if (int.TryParse(seq, out int number))
                    {
                        dependSequences[i] = number.ToString();
                    }
                    else
                    {
                        dependSequences[i] = seq;
                    }
                }
                dependsOnText.text = $"Depends on: {string.Join(", ", dependSequences)}";
                // 有依赖时显示 GameObject
                dependsOnText.gameObject.SetActive(true);
            }
            else
            {
                // 没有依赖时隐藏 GameObject
                dependsOnText.gameObject.SetActive(false);
            }
        }
        else
        {
            Debug.LogError("PlanUI: dependsOnText is not assigned");
        }

        // 设置 Toggle 按钮
        if (toggleButton != null)
        {
            toggleButton.onValueChanged.AddListener((isOn) => OnTogglePlanItem(planItem.id, isOn));
        }
        else
        {
            Debug.LogError("PlanUI: toggleButton is not assigned");
        }

        if (backgroundImage == null)
        {
            Debug.LogError("PlanUI: backgroundImage is not assigned");
        }

        // 设置初始状态（确认状态）
        UpdatePlanItemVisuals(itemData);

        // 添加到字典
        _planItems[planItem.id] = itemData;
    }

    /// <summary>
    /// 切换 Plan Item 的确认/取消状态
    /// </summary>
    private void OnTogglePlanItem(string planId, bool isOn)
    {
        if (!_planItems.ContainsKey(planId))
        {
            Debug.LogWarning($"PlanUI: Plan item {planId} not found");
            return;
        }

        PlanItemData itemData = _planItems[planId];
        
        // 根据 Toggle 状态设置
        itemData.isConfirmed = isOn;
        
        // 更新视觉效果
        UpdatePlanItemVisuals(itemData);
        
        // 如果取消，需要取消所有依赖它的 plan
        if (!itemData.isConfirmed)
        {
            CancelDependentPlans(planId);
        }
        
    }

    /// <summary>
    /// 取消所有依赖指定 plan 的其他 plan
    /// </summary>
    private void CancelDependentPlans(string cancelledPlanId)
    {
        foreach (var kvp in _planItems)
        {
            PlanItemData itemData = kvp.Value;
            
            // 检查这个 plan 是否依赖被取消的 plan
            if (itemData.planItem.depends_on != null)
            {
                foreach (string dependsOnId in itemData.planItem.depends_on)
                {
                    if (dependsOnId == cancelledPlanId && itemData.isConfirmed)
                    {
                        // 取消这个 plan
                        itemData.isConfirmed = false;
                        UpdatePlanItemVisuals(itemData);
                        
                        // 递归取消所有依赖这个 plan 的其他 plan
                        CancelDependentPlans(kvp.Key);
                        
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 更新 Plan Item 的视觉效果
    /// </summary>
    private void UpdatePlanItemVisuals(PlanItemData itemData)
    {
        // 更新背景透明度
        if (itemData.backgroundImage != null)
        {
            Color color = itemData.backgroundImage.color;
            color.a = itemData.isConfirmed ? 1.0f : DISABLED_ALPHA;
            itemData.backgroundImage.color = color;
        }
        
        // 同步 Toggle 显示和交互状态
        if (itemData.toggleButton != null)
        {
            itemData.toggleButton.isOn = itemData.isConfirmed;
            itemData.toggleButton.interactable = true;
        }
    }

    /// <summary>
    /// 从 Plan ID 中提取序号（最后一个下划线后的数字）
    /// 例如: "plan_goal_123456_01_01" -> "01"
    /// </summary>
    private string ExtractPlanSequenceNumber(string planId)
    {
        if (string.IsNullOrEmpty(planId))
            return "";
        
        int lastUnderscoreIndex = planId.LastIndexOf('_');
        if (lastUnderscoreIndex >= 0 && lastUnderscoreIndex < planId.Length - 1)
        {
            return planId.Substring(lastUnderscoreIndex + 1);
        }
        
        return planId; // 如果没有下划线，返回原字符串
    }

    /// <summary>
    /// 辅助方法：在预制体中查找子对象
    /// </summary>
    private Transform FindChildInPrefab(GameObject parent, string childName)
    {
        foreach (Transform child in parent.transform)
        {
            if (child.name == childName)
            {
                return child;
            }
            // 递归查找
            Transform found = FindChildInPrefab(child.gameObject, childName);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// 清空所有 Plan Items
    /// </summary>
    private void ClearPlanItems()
    {
        if (planListParent != null)
        {
            // 先收集所有要销毁的子对象，避免 Destroy() 不会立即移除导致的无限循环
            List<GameObject> childrenToDestroy = new List<GameObject>();
            foreach (Transform child in planListParent)
            {
                // 只收集场景实例，不收集预制体资源
                if (child != null)
                {
                    // 检查对象是否在场景中（不在场景中的不会被销毁）
                    bool isInScene = child.gameObject.scene.name != null && child.gameObject.scene.name != "";
                    if (isInScene)
                    {
                        childrenToDestroy.Add(child.gameObject);
                    }
                }
            }
            
            // 然后逐个销毁
            foreach (GameObject child in childrenToDestroy)
            {
                if (child != null)
                {
                    try
                    {
                        if (Application.isPlaying)
                        {
                            Destroy(child);
                        }
                        else
                        {
                            DestroyImmediate(child);
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Error destroying plan item: {e.Message}");
                    }
                }
            }
        }
        _planItems = null;
    }

    /// <summary>
    /// 清除当前显示的 Plan Batch
    /// </summary>
    public void Clear()
    {
        ClearPlanItems();
        _currentPlanBatch = new PlanBatch();
        
        if (goalLabelText != null)
            goalLabelText.text = "";
        if (talkToPlayerText != null)
            talkToPlayerText.text = "";
    }

    /// <summary>
    /// 获取所有确认的 Plan Items
    /// </summary>
    public PlanItem[] GetConfirmedPlans()
    {
        List<PlanItem> confirmedPlans = new List<PlanItem>();
        
        if (_planItems == null)
            return new PlanItem[0];
        
        foreach (var kvp in _planItems)
        {
            if (kvp.Value.isConfirmed)
            {
                confirmedPlans.Add(kvp.Value.planItem);
            }
        }
        
        return confirmedPlans.ToArray();
    }

    /// <summary>
    /// 获取指定 Plan Item 的状态
    /// </summary>
    public bool IsPlanConfirmed(string planId)
    {
        if (_planItems != null && _planItems.ContainsKey(planId))
        {
            return _planItems[planId].isConfirmed;
        }
        return false;
    }

    /// <summary>
    /// 处理发送反馈按钮点击
    /// </summary>
    private void OnSendFeedback()
    {
        if (runtimeAIChat == null)
        {
            Debug.LogError("PlanUI: RuntimeAIChat is not available, cannot send plan permission");
            return;
        }

        if (_currentPlanBatch.plan == null || _currentPlanBatch.plan.Length == 0)
        {
            Debug.LogWarning("PlanUI: no plans to send");
            return;
        }

        // 获取所有确认的计划
        PlanItem[] confirmedPlans = GetConfirmedPlans();
        
        // 获取 additional info（先确保提交当前输入内容）
        string additionalInfo = "";
        if (feedbackInputField != null)
        {
            // 提交并同步输入内容，避免仍在编辑态导致读取为空
            feedbackInputField.DeactivateInputField();
            feedbackInputField.ForceLabelUpdate();
            additionalInfo = feedbackInputField.text != null ? feedbackInputField.text.Trim() : "";
        }
        else
        {
            Debug.LogWarning("PlanUI: feedbackInputField is not assigned, additional info will be empty");
        }
        
        // 验证：只要有 confirmed plans 或者 additional info 有内容就可以发送
        bool hasConfirmedPlans = confirmedPlans != null && confirmedPlans.Length > 0;
        bool hasAdditionalInfo = !string.IsNullOrWhiteSpace(additionalInfo);
        
        if (!hasConfirmedPlans && !hasAdditionalInfo)
        {
            Debug.LogWarning("PlanUI: no confirmed plans and no additional info to send");
            return;
        }
        
        // 如果没有确认的计划，使用空数组
        if (confirmedPlans == null || confirmedPlans.Length == 0)
        {
            confirmedPlans = new PlanItem[0];
        }

        // 获取当前游戏状态
        GameState gameState = null;
        if (gameStateManager != null)
        {
            gameState = gameStateManager.GetFreshGameStateSnapshot();
        }
        else
        {
            Debug.LogWarning("PlanUI: cannot find GameStateManager, the game state will not be included");
        }

        // 创建 Plan Permission Request
        PlanPermissionRequest request = new PlanPermissionRequest(
            _currentPlanBatch.session_id,
            _currentPlanBatch.goal_id,
            _currentPlanBatch.goal_label,
            additionalInfo,
            confirmedPlans,
            gameState
        );

        Debug.Log($"PlanUI: Sending {confirmedPlans.Length} approved plans with additional info: {(string.IsNullOrWhiteSpace(additionalInfo) ? "(empty)" : additionalInfo)}");

        // 直接调用 GameStateManager 移除已发送的 pending plans
        if (gameStateManager != null)
        {
            gameStateManager.RemovePendingPlans(confirmedPlans);
        }

        // 发送前立即禁用输入框和按钮，并显示等待消息
        SetFeedbackUIInteractable(false);
        if (chatUI != null)
        {
            chatUI.CreateWaitingMessage();
        }

        // 通过 RuntimeAIChat 发送请求
        RuntimeAIChat.ApiResponseCallback callback = (response, error) =>
        {
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"PlanUI: failed to send plan permission, error: {error}");
                // 发送失败时恢复交互状态
                SetFeedbackUIInteractable(true);
            }
        };

        runtimeAIChat.SendPlanPermission(request, callback);
    }

    /// <summary>
    /// 设置反馈 UI 的交互状态（发送后禁用）
    /// </summary>
    private void SetFeedbackUIInteractable(bool interactable)
    {
        // 禁用输入框的交互（但保留文字显示）
        if (feedbackInputField != null)
        {
            feedbackInputField.interactable = interactable;
            if (!interactable)
            {
                // 发送后保持文字显示但不可编辑
                // feedbackInputField.text 已经包含了发送的文字
            }
        }

        // 禁用发送按钮
        if (sendFeedbackButton != null)
        {
            sendFeedbackButton.interactable = interactable;
            
            // 设置按钮背景透明度
            Image buttonImage = sendFeedbackButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                Color color = buttonImage.color;
                color.a = interactable ? 1.0f : DISABLED_ALPHA;
                buttonImage.color = color;
            }
        }
    }
}
