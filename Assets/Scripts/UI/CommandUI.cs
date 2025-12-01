using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using Voxels;

/// <summary>
/// 管理 Command Batch 的 UI 显示 - 显示命令列表和状态
/// 支持不同类型命令使用不同的Prefab
/// </summary>
public class CommandUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text goalLabelText;
    [SerializeField] private Transform commandListParent; // Command 列表的父物体
    
    [Header("Command Item Prefabs")]
    [SerializeField] private GameObject createVoxelTypePrefab;
    [SerializeField] private GameObject updateVoxelTypePrefab;
    [SerializeField] private GameObject placeBlockPrefab;
    [SerializeField] private GameObject destroyBlockPrefab;
    [SerializeField] private GameObject moveToPrefab;
    [SerializeField] private GameObject continuePlanPrefab;
    
    private CommandBatch _currentCommandBatch;
    private Dictionary<string, CommandItemData> _commandItems; // command_id -> CommandItemData
    private CommandManager _commandManager;
    
    // 固定子对象名称
    private const string STATUS_TEXT_NAME = "command_Status";
    private const string DESCRIPTION_TEXT_NAME = "command_Des";
    private const string CANCEL_BUTTON_NAME = "cancel"; // 也可以根据实际名称调整
    
    /// <summary>
    /// Command Item 的数据结构
    /// </summary>
    private class CommandItemData
    {
        public GameObject instance;
        public CommandData command;
        public TMP_Text statusText;
        public TMP_Text descriptionText;
        public Button cancelButton;
        
        public CommandItemData(GameObject instance, CommandData command)
        {
            this.instance = instance;
            this.command = command;
        }
    }
    
    private void Awake()
    {
        InitializeReferences();
    }
    
    private void OnEnable()
    {
        // 订阅命令状态更新事件
        CommandManager.OnCommandStatusChanged += OnCommandStatusChanged;
    }
    
    private void OnDisable()
    {
        // 取消订阅
        CommandManager.OnCommandStatusChanged -= OnCommandStatusChanged;
    }
    
    private void InitializeReferences()
    {
        if (_commandManager == null)
        {
            _commandManager = FindAnyObjectByType<CommandManager>();
            if (_commandManager == null)
            {
                Debug.LogWarning("CommandUI: 找不到 CommandManager 组件");
            }
        }
    }
    
    /// <summary>
    /// 显示 Command Batch
    /// </summary>
    public void DisplayCommandBatch(CommandBatch commandBatch)
    {
        _currentCommandBatch = commandBatch;
        
        // 显示目标标签
        if (goalLabelText != null)
        {
            // 尝试从GoalLabelStorage获取goal_label
            string goalLabel = GoalLabelStorage.GetGoalLabel(commandBatch.goal_id);
            if (!string.IsNullOrEmpty(goalLabel))
            {
                goalLabelText.text = $"Commands for Goal: {goalLabel}";
            }
            else
            {
                goalLabelText.text = $"Commands for Goal: {commandBatch.goal_id}";
            }
        }
        
        // 清空现有的 command items
        ClearCommandItems();
        
        // 创建每个 command item
        if (commandBatch.commands != null && commandBatch.commands.Count > 0)
        {
            if (commandListParent == null)
            {
                Debug.LogError("CommandUI: commandListParent not assigned. Cannot create command items.");
                return;
            }
            
            _commandItems = new Dictionary<string, CommandItemData>();
            
            for (int i = 0; i < commandBatch.commands.Count; i++)
            {
                CreateCommandItem(commandBatch.commands[i], i);
            }
        }
    }
    
    /// <summary>
    /// 创建单个 Command Item
    /// </summary>
    private void CreateCommandItem(CommandData command, int index)
    {
        // 根据命令类型选择对应的Prefab
        GameObject prefabToUse = GetPrefabForCommandType(command.type);
        if (prefabToUse == null)
        {
            Debug.LogError($"CommandUI: No prefab found for command type: {command.type}");
            return;
        }
        
        if (commandListParent == null)
        {
            Debug.LogError("CommandUI: commandListParent is not assigned");
            return;
        }
        
        GameObject instance = Instantiate(prefabToUse, commandListParent);
        CommandItemData itemData = new CommandItemData(instance, command);
        
        // 查找固定的子对象
        Transform statusTransform = FindChildInPrefab(instance, STATUS_TEXT_NAME);
        Transform descriptionTransform = FindChildInPrefab(instance, DESCRIPTION_TEXT_NAME);
        Transform cancelButtonTransform = FindChildInPrefab(instance, CANCEL_BUTTON_NAME);
        
        if (statusTransform != null)
        {
            itemData.statusText = statusTransform.GetComponent<TMP_Text>();
        }
        else
        {
            Debug.LogWarning($"CommandUI: command_Status not found in prefab for {command.type}");
        }
        
        if (descriptionTransform != null)
        {
            itemData.descriptionText = descriptionTransform.GetComponent<TMP_Text>();
            // 设置详细描述
            itemData.descriptionText.text = GetCommandDetailedDescription(command);
        }
        else
        {
            Debug.LogWarning($"CommandUI: command_Des not found in prefab for {command.type}");
        }
        
        if (cancelButtonTransform != null)
        {
            itemData.cancelButton = cancelButtonTransform.GetComponent<Button>();
            if (itemData.cancelButton != null)
            {
                // 绑定取消按钮事件
                itemData.cancelButton.onClick.AddListener(() => OnCancelCommand(command.id));
                
                // 初始状态：只有Pending状态的命令可以取消
                UpdateCancelButtonState(itemData, CommandStatus.Pending);
            }
        }
        else
        {
            Debug.LogWarning($"CommandUI: CancelButton not found in prefab for {command.type}");
        }
        
        // 初始化状态显示
        UpdateCommandItemStatus(itemData, CommandStatus.Pending);
        
        // 添加到字典
        _commandItems[command.id] = itemData;
    }
    
    /// <summary>
    /// 根据命令类型获取对应的Prefab
    /// </summary>
    private GameObject GetPrefabForCommandType(string commandType)
    {
        switch (commandType)
        {
            case "create_voxel_type":
                return createVoxelTypePrefab;
            case "update_voxel_type":
                return updateVoxelTypePrefab;
            case "place_block":
                return placeBlockPrefab;
            case "destroy_block":
                return destroyBlockPrefab;
            case "move_to":
                return moveToPrefab;
            case "continue_plan":
                return continuePlanPrefab;
            default:
                Debug.LogError($"CommandUI: Unknown command type: {commandType}");
                return null;
        }
    }
    
    /// <summary>
    /// 取消命令按钮点击事件
    /// </summary>
    private void OnCancelCommand(string commandId)
    {
        if (_commandManager == null)
        {
            Debug.LogError("CommandUI: CommandManager not found, cannot cancel command");
            return;
        }
        
        // 检查命令状态
        CommandStatus currentStatus = _commandManager.GetCommandStatus(commandId);
        if (currentStatus != CommandStatus.Pending)
        {
            Debug.LogWarning($"CommandUI: Cannot cancel command {commandId}, status is {currentStatus}");
            return;
        }
        
        // 调用CommandManager中断命令
        _commandManager.InterruptCommand(commandId);
        Debug.Log($"CommandUI: Cancelled command {commandId}");
    }
    
    /// <summary>
    /// 更新取消按钮的状态（显示/隐藏、交互性与灰度）
    /// </summary>
    private void UpdateCancelButtonState(CommandItemData itemData, CommandStatus status)
    {
        if (itemData.cancelButton == null) return;

        GameObject buttonGO = itemData.cancelButton.gameObject;
        Image buttonImage = itemData.cancelButton.GetComponent<Image>();

        switch (status)
        {
            case CommandStatus.Pending:
                // 可见且可交互
                if (!buttonGO.activeSelf) buttonGO.SetActive(true);
                itemData.cancelButton.interactable = true;
                if (buttonImage != null) buttonImage.color = Color.white;
                break;
            case CommandStatus.Cancelled:
                // 可见但不可交互且呈灰色
                if (!buttonGO.activeSelf) buttonGO.SetActive(true);
                itemData.cancelButton.interactable = false;
                if (buttonImage != null) buttonImage.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                break;
            default:
                // 其他状态不可取消，隐藏按钮
                if (buttonGO.activeSelf) buttonGO.SetActive(false);
                break;
        }
    }
    
    /// <summary>
    /// 获取命令的详细描述信息（用于command_Des显示）
    /// </summary>
    private string GetCommandDetailedDescription(CommandData command)
    {
        // 根据命令类型和参数生成详细描述
        switch (command.type)
        {
            case "destroy_block":
                return GetDestroyBlockDescription(command);
            case "place_block":
                return GetPlaceBlockDescription(command);
            case "move_to":
                return GetMoveToDescription(command);
            case "create_voxel_type":
                return GetCreateVoxelTypeDescription(command);
            case "update_voxel_type":
                return GetUpdateVoxelTypeDescription(command);
            case "continue_plan":
                return GetContinuePlanDescription(command);
            default:
                return command.type;
        }
    }
    
    /// <summary>
    /// 获取Destroy命令的详细描述
    /// 格式：start offset: (1,0,1)
    ///       expand direction: up
    ///       amount: 2 blocks in a row
    ///       target voxel: any
    /// </summary>
    private string GetDestroyBlockDescription(CommandData command)
    {
        DestroyBlockParams @params = ParseParamsData<DestroyBlockParams>(command.params_data);
        
        if (@params == null)
            return "Invalid parameters";
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"start offset: {FormatRelativeOffset(@params.start_offset)}");
        sb.AppendLine($"expand direction: {FormatExpandDirection(@params.expand_direction)}");
        
        int blockCount = Mathf.Max(1, @params.count);
        string countText = blockCount == 1 ? "block" : "blocks";
        sb.AppendLine($"amount: {blockCount} {countText} in a row");
        
        // target voxel
        if (@params.voxel_names != null && @params.voxel_names.Length > 0)
        {
            sb.AppendLine($"target voxel: {string.Join(", ", @params.voxel_names)}");
        }
        else if (@params.voxel_ids != null && @params.voxel_ids.Length > 0)
        {
            sb.AppendLine($"target voxel: {string.Join(", ", @params.voxel_ids)}");
        }
        else
        {
            sb.AppendLine("target voxel: any");
        }
        
        return sb.ToString().TrimEnd('\n', '\r');
    }
    
    /// <summary>
    /// 获取Place命令的详细描述
    /// </summary>
    private string GetPlaceBlockDescription(CommandData command)
    {
        PlaceBlockParams @params = ParseParamsData<PlaceBlockParams>(command.params_data);
        
        if (@params == null)
            return "Invalid parameters";
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"start offset: {FormatRelativeOffset(@params.start_offset)}");
        sb.AppendLine($"expand direction: {FormatExpandDirection(@params.expand_direction)}");
        
        int blockCount = Mathf.Max(1, @params.count);
        string countText = blockCount == 1 ? "block" : "blocks";
        sb.AppendLine($"amount: {blockCount} {countText} in a row");
        
        if (!string.IsNullOrEmpty(@params.voxel_name))
        {
            sb.AppendLine($"voxel material: {@params.voxel_name}");
        }
        else if (!string.IsNullOrEmpty(@params.voxel_id))
        {
            sb.AppendLine($"voxel material: {@params.voxel_id}");
        }
        
        return sb.ToString().TrimEnd('\n', '\r');
    }
    
    /// <summary>
    /// 获取MoveTo命令的详细描述
    /// </summary>
    private string GetMoveToDescription(CommandData command)
    {
        MoveToParams @params = ParseParamsData<MoveToParams>(command.params_data);
        
        if (@params == null)
            return "Invalid parameters";
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        // 计算目标位置（从当前agent位置）
        float x = @params.target_pos.x;
        float y = @params.target_pos.y;
        float z = @params.target_pos.z;
        
        sb.AppendLine($"position: ({x}, {y}, {z})");
        
        // 显示移动方向
        sb.Append("move: ");
        List<string> moves = new List<string>();
        if (Mathf.Abs(x) > 0.01f)
        {
            moves.Add($"{(x > 0 ? "right" : "left")} {Mathf.Abs(x)}");
        }
        if (Mathf.Abs(y) > 0.01f)
        {
            moves.Add($"{(y > 0 ? "up" : "down")} {Mathf.Abs(y)}");
        }
        if (Mathf.Abs(z) > 0.01f)
        {
            moves.Add($"{(z > 0 ? "forward" : "backward")} {Mathf.Abs(z)}");
        }
        
        if (moves.Count == 0)
        {
            sb.AppendLine("none");
        }
        else
        {
            sb.AppendLine(string.Join("; ", moves));
        }
        
        return sb.ToString().TrimEnd('\n', '\r');
    }
    
    /// <summary>
    /// 获取CreateVoxelType命令的详细描述
    /// </summary>
    private string GetCreateVoxelTypeDescription(CommandData command)
    {
        CreateVoxelTypeParams @params = ParseParamsData<CreateVoxelTypeParams>(command.params_data);
        
        if (@params == null || @params.voxel_type == null)
            return "Invalid parameters";
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"name: {@params.voxel_type.name}");
        
        if (!string.IsNullOrEmpty(@params.voxel_type.description))
        {
            sb.AppendLine($"description: {@params.voxel_type.description}");
        }
        
        // 显示面纹理
        if (@params.voxel_type.face_textures != null && @params.voxel_type.face_textures.Length >= 6)
        {
            sb.AppendLine("face textures:");
            // VoxelTypeData.face_textures 顺序是 [right, left, top, bottom, front, back]
            // 按用户要求的格式：top(60,50,10), bottom(255,255,255); left(252,150,110), right(255,255,255); front(160,250,210), back(255,255,255);
            
            // 第一行：top, bottom
            sb.Append($"top({FormatTextureAsRGB(@params.voxel_type.face_textures[2])}), bottom({FormatTextureAsRGB(@params.voxel_type.face_textures[3])}); ");
            // 第二行：left, right
            sb.Append($"left({FormatTextureAsRGB(@params.voxel_type.face_textures[1])}), right({FormatTextureAsRGB(@params.voxel_type.face_textures[0])}); ");
            // 第三行：front, back
            sb.AppendLine($"front({FormatTextureAsRGB(@params.voxel_type.face_textures[4])}), back({FormatTextureAsRGB(@params.voxel_type.face_textures[5])});");
        }
        
        return sb.ToString().TrimEnd('\n', '\r');
    }
    
    /// <summary>
    /// 获取UpdateVoxelType命令的详细描述
    /// </summary>
    private string GetUpdateVoxelTypeDescription(CommandData command)
    {
        UpdateVoxelTypeParams @params = ParseParamsData<UpdateVoxelTypeParams>(command.params_data);
        
        if (@params == null || @params.new_voxel_type == null)
            return "Invalid parameters";
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"voxel id: {@params.voxel_id}");
        
        // 尝试获取当前名称
        string currentName = "";
        if (ushort.TryParse(@params.voxel_id, out ushort typeId))
        {
            var def = VoxelRegistry.GetDefinition(typeId);
            if (def != null)
            {
                currentName = def.name;
            }
        }
        
        if (!string.IsNullOrEmpty(currentName))
        {
            sb.AppendLine($"current name: {currentName}");
        }
        sb.AppendLine($"new name: {@params.new_voxel_type.name}");
        
        if (!string.IsNullOrEmpty(@params.new_voxel_type.description))
        {
            sb.AppendLine($"updated description: {@params.new_voxel_type.description}");
        }
        
        // 显示更新的面纹理
        if (@params.new_voxel_type.face_textures != null && @params.new_voxel_type.face_textures.Length >= 6)
        {
            sb.AppendLine("updated textures:");
            // VoxelTypeData.face_textures 顺序是 [right, left, top, bottom, front, back]
            
            // 第一行：top, bottom
            sb.Append($"top({FormatTextureAsRGB(@params.new_voxel_type.face_textures[2])}), bottom({FormatTextureAsRGB(@params.new_voxel_type.face_textures[3])}); ");
            // 第二行：left, right
            sb.Append($"left({FormatTextureAsRGB(@params.new_voxel_type.face_textures[1])}), right({FormatTextureAsRGB(@params.new_voxel_type.face_textures[0])}); ");
            // 第三行：front, back
            sb.AppendLine($"front({FormatTextureAsRGB(@params.new_voxel_type.face_textures[4])}), back({FormatTextureAsRGB(@params.new_voxel_type.face_textures[5])});");
        }
        
        return sb.ToString().TrimEnd('\n', '\r');
    }
    
    /// <summary>
    /// 获取ContinuePlan命令的详细描述
    /// </summary>
    private string GetContinuePlanDescription(CommandData command)
    {
        ContinuePlanParams @params = ParseParamsData<ContinuePlanParams>(command.params_data);
        
        if (@params == null)
            return "Invalid parameters";
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        if (!string.IsNullOrEmpty(@params.current_summary))
        {
            sb.AppendLine($"current goal: {@params.current_summary}");
        }
        
        if (@params.possible_next_steps != null && @params.possible_next_steps.Length > 0)
        {
            sb.AppendLine($"next: {string.Join(", ", @params.possible_next_steps)}");
        }
        
        sb.AppendLine($"Requesting snapshot: {(@params.request_snapshot ? "yes" : "no")}");
        
        return sb.ToString().TrimEnd('\n', '\r');
    }
    
    /// <summary>
    /// 命令状态更新回调
    /// </summary>
    private void OnCommandStatusChanged(string commandId, CommandStatus status, string errorMessage = null)
    {
        if (_commandItems != null && _commandItems.ContainsKey(commandId))
        {
            CommandItemData itemData = _commandItems[commandId];
            UpdateCommandItemStatus(itemData, status, errorMessage);
        }
    }
    
    /// <summary>
    /// 更新 Command Item 的状态显示
    /// </summary>
    private void UpdateCommandItemStatus(CommandItemData itemData, CommandStatus status, string errorMessage = null)
    {
        // 更新状态文本（command_Status）
        if (itemData.statusText != null)
        {
            string statusText = status.ToString().ToUpper();
            Color statusColor = GetStatusColor(status);
            
            itemData.statusText.text = statusText;
            itemData.statusText.color = statusColor;
            
            // 如果有错误信息，添加到状态文本中
            if (status == CommandStatus.Failed && !string.IsNullOrEmpty(errorMessage))
            {
                itemData.statusText.text += $": {errorMessage}";
            }
        }
        
        // 更新取消按钮状态
        UpdateCancelButtonState(itemData, status);
    }
    
    /// <summary>
    /// 获取状态对应的颜色（用于command_Status文本）
    /// </summary>
    private Color GetStatusColor(CommandStatus status)
    {
        switch (status)
        {
            case CommandStatus.Pending:
                return Color.gray;
            case CommandStatus.Done:
                return Color.green;
            case CommandStatus.Failed:
                return Color.red;
            case CommandStatus.Cancelled:
                return new Color(0.7f, 0.7f, 0.7f, 1f); // 灰色（比 Pending 稍深）
            default:
                return Color.white;
        }
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
    /// 清空所有 Command Items
    /// </summary>
    private void ClearCommandItems()
    {
        if (commandListParent != null)
        {
            List<GameObject> childrenToDestroy = new List<GameObject>();
            foreach (Transform child in commandListParent)
            {
                if (child != null)
                {
                    bool isInScene = child.gameObject.scene.name != null && child.gameObject.scene.name != "";
                    if (isInScene)
                    {
                        childrenToDestroy.Add(child.gameObject);
                    }
                }
            }
            
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
                        Debug.LogWarning($"Error destroying command item: {e.Message}");
                    }
                }
            }
        }
        _commandItems = null;
    }
    
    /// <summary>
    /// 清除当前显示的 Command Batch
    /// </summary>
    public void Clear()
    {
        ClearCommandItems();
        _currentCommandBatch = new CommandBatch("", "");
        
        if (goalLabelText != null)
            goalLabelText.text = "";
    }
    
    /// <summary>
    /// 将纹理路径格式化为RGB颜色字符串
    /// 如果纹理文件名格式为 "数字+数字+数字.png" 或 "数字+数字+数字"，解析为RGB
    /// 否则返回原文件名
    /// </summary>
    private string FormatTextureAsRGB(string texturePath)
    {
        if (string.IsNullOrEmpty(texturePath))
        {
            return "255,255,255"; // 默认白色
        }
        
        // 移除文件扩展名
        string nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(texturePath);
        string[] parts = nameWithoutExt.Split('+');
        
        // 检查格式：数字+数字+数字
        if (parts.Length == 3)
        {
            if (int.TryParse(parts[0], out int r) &&
                int.TryParse(parts[1], out int g) &&
                int.TryParse(parts[2], out int b))
            {
                // 验证值范围
                if (r >= 0 && r <= 255 && g >= 0 && g <= 255 && b >= 0 && b <= 255)
                {
                    return $"{r},{g},{b}";
                }
            }
        }
        
        // 如果不是RGB格式，返回原文件名
        return texturePath;
    }
    
    private string FormatRelativeOffset(Vector3Data offset)
    {
        if (offset == null)
        {
            return "(0, 0, 0)";
        }
        
        int x = Mathf.RoundToInt(offset.x);
        int y = Mathf.RoundToInt(offset.y);
        int z = Mathf.RoundToInt(offset.z);
        
        return $"({x}, {y}, {z})";
    }
    
    private string FormatExpandDirection(string direction)
    {
        if (string.IsNullOrEmpty(direction))
        {
            return "up";
        }
        
        return direction.ToLowerInvariant();
    }
    
    /// <summary>
    /// 正确解析params_data - params_data可能是JSON字符串或已解析的对象
    /// </summary>
    private T ParseParamsData<T>(object paramsData) where T : class
    {
        if (paramsData == null)
        {
            return null;
        }
        
        // 如果已经是正确类型，直接返回
        if (paramsData is T)
        {
            return paramsData as T;
        }
        
        // 如果是字符串（JSON字符串），直接解析
        if (paramsData is string jsonString)
        {
            try
            {
                return JsonUtility.FromJson<T>(jsonString);
            }
            catch (Exception e)
            {
                Debug.LogError($"ParseParamsData: Failed to parse from JSON string: {e.Message}");
                return null;
            }
        }
        
        // 尝试将对象转换为JSON再解析
        try
        {
            string json = JsonUtility.ToJson(paramsData);
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"ParseParamsData: Failed to parse params: {e.Message}");
            return null;
        }
    }
}

