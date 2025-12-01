using System.Text.RegularExpressions;
using UnityEngine;
using Voxels;
using System.Collections.Generic;
using System;
using System.Linq;

/// <summary>
/// 命令管理器 - 负责解析CommandBatch，创建并管理命令执行器，跟踪执行状态
/// </summary>
public class CommandManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AgentCam agentCam;
    [SerializeField] private ChatUI chatUI;
    private GameStateManager gameStateManager;
    
    [Header("Command Executors")]
    [SerializeField] private CreateVoxelTypeExecutor createVoxelTypeExecutor;
    [SerializeField] private UpdateVoxelTypeExecutor updateVoxelTypeExecutor;
    [SerializeField] private PlaceBlockExecutor placeBlockExecutor;
    [SerializeField] private DestroyBlockExecutor destroyBlockExecutor;
    [SerializeField] private MoveToExecutor moveToExecutor;
    [SerializeField] private ContinuePlanExecutor continuePlanExecutor;
    
    // 命令状态跟踪字典
    private Dictionary<string, CommandStatus> _commandStatuses = new Dictionary<string, CommandStatus>();
    private Dictionary<string, ICommandExecutor> _activeExecutions = new Dictionary<string, ICommandExecutor>();
    
    // 当前正在处理的CommandBatch
    private CommandBatch _currentCommandBatch;
    
    // 状态更新事件
    public delegate void CommandStatusChangedCallback(string commandId, CommandStatus status, string errorMessage = null);
    public static event CommandStatusChangedCallback OnCommandStatusChanged;

    void OnEnable()
    {
        InitializeComponents();
    }
    
    private void InitializeComponents()
    {
        if (agentCam == null)
        {
            agentCam = FindAnyObjectByType<AgentCam>();
            if (agentCam == null)
            {
                Debug.LogError("CommandManager: AgentCam is not assigned");
            }
        }
        
        if (chatUI == null)
        {
            chatUI = FindAnyObjectByType<ChatUI>();
        }
        
        // 获取 GameStateManager 引用
        if (gameStateManager == null)
        {
            gameStateManager = FindAnyObjectByType<GameStateManager>();
            if (gameStateManager == null)
            {
                Debug.LogWarning("CommandManager: 找不到GameStateManager组件，命令状态更新功能将不可用");
            }
        }
        
        // 初始化命令执行器
        InitializeExecutors();
    }
    
    private void InitializeExecutors()
    {
        if (createVoxelTypeExecutor == null)
            createVoxelTypeExecutor = GetComponent<CreateVoxelTypeExecutor>() ?? FindAnyObjectByType<CreateVoxelTypeExecutor>();
        if (updateVoxelTypeExecutor == null)
            updateVoxelTypeExecutor = GetComponent<UpdateVoxelTypeExecutor>() ?? FindAnyObjectByType<UpdateVoxelTypeExecutor>();
        if (placeBlockExecutor == null)
            placeBlockExecutor = GetComponent<PlaceBlockExecutor>() ?? FindAnyObjectByType<PlaceBlockExecutor>();
        if (destroyBlockExecutor == null)
            destroyBlockExecutor = GetComponent<DestroyBlockExecutor>() ?? FindAnyObjectByType<DestroyBlockExecutor>();
        if (moveToExecutor == null)
            moveToExecutor = GetComponent<MoveToExecutor>() ?? FindAnyObjectByType<MoveToExecutor>();
        if (continuePlanExecutor == null)
            continuePlanExecutor = GetComponent<ContinuePlanExecutor>() ?? FindAnyObjectByType<ContinuePlanExecutor>();
    }

    /// <summary>
    /// 处理CommandBatch - 解析并执行所有命令
    /// </summary>
    public void ProcessCommandBatch(CommandBatch commandBatch)
    {
        if (commandBatch == null || commandBatch.commands == null || commandBatch.commands.Count == 0)
        {
            Debug.LogWarning("CommandManager: CommandBatch is null or empty");
            return;
        }
        
        _currentCommandBatch = commandBatch;
        Debug.Log($"CommandManager: Processing CommandBatch with {commandBatch.commands.Count} commands");
        
        // 在ChatUI中显示CommandUI
        if (chatUI != null)
        {
            chatUI.DisplayCommandBatch(commandBatch);
        }
        
        // 初始化所有命令状态为Pending
        foreach (var command in commandBatch.commands)
        {
            UpdateCommandStatus(command.id, CommandStatus.Pending);
        }
        
        // 执行命令（按顺序，可以考虑依赖关系）
        ExecuteCommandsSequentially(commandBatch.commands, 0);
    }
    
    /// <summary>
    /// 顺序执行命令列表
    /// </summary>
    private void ExecuteCommandsSequentially(List<CommandData> commands, int index)
    {
        if (index >= commands.Count)
        {
            Debug.Log("CommandManager: All commands executed");
            return;
        }
        
        var command = commands[index];
        ExecuteCommand(command, (success, error) =>
        {
            if (success)
            {
                // 继续执行下一个命令
                ExecuteCommandsSequentially(commands, index + 1);
            }
            else
            {
                // 失败时也可以继续执行（或停止）
                Debug.LogWarning($"CommandManager: Command {command.id} failed: {error}");
                ExecuteCommandsSequentially(commands, index + 1);
            }
        });
    }
    
    /// <summary>
    /// 执行单个命令
    /// </summary>
    public void ExecuteCommand(CommandData command, Action<bool, string> onComplete = null)
    {
        if (command == null || string.IsNullOrEmpty(command.id) || string.IsNullOrEmpty(command.type))
        {
            Debug.LogError($"CommandManager: Invalid command data");
            onComplete?.Invoke(false, "Invalid command data");
            return;
        }
        
        // 获取命令执行器
        ICommandExecutor executor = GetExecutorForType(command.type);
        if (executor == null)
        {
            string error = $"CommandManager: No executor found for command type: {command.type}";
            Debug.LogError(error);
            UpdateCommandStatus(command.id, CommandStatus.Failed, error);
            onComplete?.Invoke(false, error);
            return;
        }
        
        // 执行命令（不再设置 Ongoing 状态，执行中保持 Pending）
        _activeExecutions[command.id] = executor;
        
        // 执行命令
        executor.Execute(command.id, command.params_data, (success, errorMessage) =>
        {
            // 移除活跃执行记录
            _activeExecutions.Remove(command.id);
            
            // 更新状态：成功 -> Done, 失败 -> Failed
            CommandStatus finalStatus = success ? CommandStatus.Done : CommandStatus.Failed;
            UpdateCommandStatus(command.id, finalStatus, errorMessage);
            
            // 调用完成回调
            onComplete?.Invoke(success, errorMessage);
        });
    }
    
    /// <summary>
    /// 根据命令类型获取执行器
    /// </summary>
    private ICommandExecutor GetExecutorForType(string commandType)
    {
        ICommandExecutor executor = null;
        
        switch (commandType)
        {
            case "create_voxel_type":
                executor = createVoxelTypeExecutor;
                if (executor == null)
                {
                    executor = GetComponent<CreateVoxelTypeExecutor>() ?? FindAnyObjectByType<CreateVoxelTypeExecutor>();
                    if (executor != null) createVoxelTypeExecutor = executor as CreateVoxelTypeExecutor;
                }
                break;
            case "update_voxel_type":
                executor = updateVoxelTypeExecutor;
                if (executor == null)
                {
                    executor = GetComponent<UpdateVoxelTypeExecutor>() ?? FindAnyObjectByType<UpdateVoxelTypeExecutor>();
                    if (executor != null) updateVoxelTypeExecutor = executor as UpdateVoxelTypeExecutor;
                }
                break;
            case "place_block":
                executor = placeBlockExecutor;
                if (executor == null)
                {
                    executor = GetComponent<PlaceBlockExecutor>() ?? FindAnyObjectByType<PlaceBlockExecutor>();
                    if (executor != null) placeBlockExecutor = executor as PlaceBlockExecutor;
                }
                break;
            case "destroy_block":
                executor = destroyBlockExecutor;
                if (executor == null)
                {
                    executor = GetComponent<DestroyBlockExecutor>() ?? FindAnyObjectByType<DestroyBlockExecutor>();
                    if (executor != null) destroyBlockExecutor = executor as DestroyBlockExecutor;
                }
                break;
            case "move_to":
                executor = moveToExecutor;
                if (executor == null)
                {
                    executor = GetComponent<MoveToExecutor>() ?? FindAnyObjectByType<MoveToExecutor>();
                    if (executor != null) moveToExecutor = executor as MoveToExecutor;
                }
                break;
            case "continue_plan":
                executor = continuePlanExecutor;
                if (executor == null)
                {
                    executor = GetComponent<ContinuePlanExecutor>() ?? FindAnyObjectByType<ContinuePlanExecutor>();
                    if (executor != null) continuePlanExecutor = executor as ContinuePlanExecutor;
                }
                break;
            default:
                Debug.LogError($"CommandManager: Unknown command type: {commandType}");
                return null;
        }
        
        // 检查 executor 是否为 null，并输出调试信息
        if (executor == null)
        {
            Debug.LogError($"CommandManager: Executor for type '{commandType}' is null. " +
                          $"Make sure the executor component is assigned in the CommandManager or exists as a component in the scene.");
        }
        
        return executor;
    }
    
    /// <summary>
    /// 更新命令状态，帮助UI更新命令状态，等
    /// </summary>
    public void UpdateCommandStatus(string commandId, CommandStatus status, string errorMessage = null)
    {
        _commandStatuses[commandId] = status;
        
        // 直接调用 GameStateManager 更新 last command 的 phase
        if (gameStateManager != null)
        {
            gameStateManager.UpdateLastCommandPhaseFromStatus(commandId, status);
        }
        
        // 触发状态更新事件
        OnCommandStatusChanged?.Invoke(commandId, status, errorMessage);
        
        Debug.Log($"CommandManager: Command {commandId} status updated to {status}" + 
                  (errorMessage != null ? $": {errorMessage}" : ""));
    }
    
    /// <summary>
    /// 获取命令状态
    /// </summary>
    public CommandStatus GetCommandStatus(string commandId)
    {
        return _commandStatuses.TryGetValue(commandId, out var status) ? status : CommandStatus.Pending;
    }
    
    /// <summary>
    /// 中断指定命令
    /// </summary>
    public void InterruptCommand(string commandId)
    {
        if (_activeExecutions.TryGetValue(commandId, out var executor))
        {
            executor.Interrupt();
            UpdateCommandStatus(commandId, CommandStatus.Cancelled, "Cancelled by user");
            _activeExecutions.Remove(commandId);
        }
    }
    
    /// <summary>
    /// 清空所有命令状态
    /// </summary>
    public void ClearCommandStatuses()
    {
        _commandStatuses.Clear();
        _activeExecutions.Clear();
        _currentCommandBatch = null;
    }

    // --------------------------------- Legacy AgentPerception ---------------------------------
    public void AgentPerception()
    {
        if (agentCam == null)
        {
            Debug.LogError("[CommandManager - AgentPerception] AgentCam is not assigned");
            return;
        }
        
        // 使用回调函数处理异步拍照
        agentCam.TakeFourDirectionPhotos((photoFileNames) =>
        {
            if (photoFileNames == null || photoFileNames.Count == 0)
            {
                Debug.LogError("[CommandManager - AgentPerception] failed to take photos or no photo file names");
                return;
            }
            
            // 转换为ImageData列表
            List<ImageData> imageDataList = new List<ImageData>();
            
            foreach (string fileName in photoFileNames)
            {
                if (!string.IsNullOrEmpty(fileName))
                {
                    imageDataList.Add(new ImageData(fileName: fileName));
                }
            }
            
            // 发布事件
            EventBus.Publish(new AgentPerceptionPayload(imageDataList));
            Debug.Log($"CommandManager - AgentPerception completed, published {imageDataList.Count} photos");
        });
        
        Debug.Log("CommandManager - AgentPerception started, waiting for photos...");
    }
}