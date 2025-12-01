using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Voxels;
using System;

/// <summary>
/// 游戏状态管理器 - 专门负责记录和管理当前游戏状态
/// 包括 agent 位置、player 位置、six direction、nearby voxels、
/// 最新的 pending plans（最多3个）和 last commands（最多3个）
/// </summary>
public class GameStateManager : MonoBehaviour
{
    [Header("Current Game State")]
    [SerializeField] private Vector3 agentPosition = Vector3.zero;
    [SerializeField] private Vector3 playerPositionRel = Vector3.zero;
    [SerializeField] private SixDirectionData sixDirection;
    [SerializeField] private List<NearbyVoxelData> nearbyVoxels = new List<NearbyVoxelData>();

    [Header("Plans & Commands")]
    [SerializeField] private List<PendingPlanData> pendingPlans = new List<PendingPlanData>();
    [SerializeField] private List<LastCommandData> lastCommands = new List<LastCommandData>();

    [Header("Settings")]
    [SerializeField] private int maxPendingPlans = 3;
    [SerializeField] private int maxLastCommands = 3;

    [Header("Auto Monitoring Settings")]
    [SerializeField] private int voxelScanRadius = 2;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform agentTransform;
    [SerializeField] private WorldGrid worldGrid;

    private float lastAutoMonitoringTime;

    private void Awake()
    {
        // 初始化游戏时间系统
        GameTime.Initialize();

        // 确保列表初始化
        if (pendingPlans == null) pendingPlans = new List<PendingPlanData>();
        if (lastCommands == null) lastCommands = new List<LastCommandData>();
        if (nearbyVoxels == null) nearbyVoxels = new List<NearbyVoxelData>();

        // 初始化 six direction 默认值
        if (sixDirection == null)
        {
            sixDirection = new SixDirectionData
            {
                up = new DirectionData("up", "", 0),
                down = new DirectionData("down", "", 0),
                front = new DirectionData("front", "", 0),
                back = new DirectionData("back", "", 0),
                left = new DirectionData("left", "", 0),
                right = new DirectionData("right", "", 0)
            };
        }

        // 自动查找必要的组件
        InitializeAutoMonitoring();
    }


    private void InitializeAutoMonitoring()
    {
        // 自动查找 Player Transform
        if (playerTransform == null)
        {
            var playerMove = FindFirstObjectByType<PlayerMove>();
            if (playerMove != null)
            {
                playerTransform = playerMove.transform;
            }
            else
            {
                Debug.LogWarning("GameStateManager: Could not find PlayerMove component for auto monitoring");
            }
        }

        // 自动查找 Agent Transform
        if (agentTransform == null)
        {
            var agentMove = FindFirstObjectByType<AgentMove>();
            //TODO: agentMove后续改为自动查找AgentMove
            if (agentMove != null)
            {
                agentTransform = agentMove.transform;
            }
            else
            {
                Debug.LogWarning("GameStateManager: Could not find AgentMove component for auto monitoring");
            }
        }

        // 自动查找 WorldGrid
        if (worldGrid == null)
        {
            worldGrid = FindFirstObjectByType<WorldGrid>();
            if (worldGrid == null)
            {
                Debug.LogWarning("GameStateManager: Could not find WorldGrid for voxel monitoring");
            }
        }
    }

    /// <summary>
    /// 执行自动监控 - 获取当前 Agent 和 Player 的位置信息以及周围体素数据
    /// </summary>
    public void PerformAutoMonitoring()
    {
        if (agentTransform == null || playerTransform == null || worldGrid == null)
        {
            Debug.LogWarning("GameStateManager: Missing required components for auto monitoring");
            return;
        }

        // 获取体素世界坐标 - 使用WorldGrid的转换函数
        Vector3Int agentVoxelPos = Vector3Int.FloorToInt(agentTransform.position);
        Vector3Int playerVoxelPos = Vector3Int.FloorToInt(playerTransform.position);

        // 转换为WorldGrid的局部坐标
        Vector3Int agentLocalPos = worldGrid.WorldToLocalVoxelCoord(agentVoxelPos);
        Vector3Int playerLocalPos = worldGrid.WorldToLocalVoxelCoord(playerVoxelPos);

        // 计算 Player 相对于 Agent 的体素位置（在WorldGrid局部坐标系中）
        Vector3Int playerPosRel = playerLocalPos - agentLocalPos;

        // 更新位置信息（使用WorldGrid局部坐标）
        UpdateAgentPosition(agentLocalPos);
        UpdatePlayerPositionRel(playerPosRel);

        // 更新六方向数据和周围体素信息
        UpdateSixDirectionData();
        UpdateNearbyVoxelsData();

        //Debug.Log($"GameStateManager: Auto monitoring completed - Agent: {agentVoxelPos}, Player Rel: {playerPosRel}");
    }

    /// <summary>
    /// 更新六方向数据 - 使用射线检测找到最近的非空气体素
    /// </summary>
    private void UpdateSixDirectionData()
    {
        if (agentTransform == null || worldGrid == null) return;

        // 定义六个方向和对应的射线方向
        Vector3[] rayDirections = {
            Vector3.up, Vector3.down, Vector3.forward,
            Vector3.back, Vector3.left, Vector3.right
        };

        string[] directionNames = { "up", "down", "front", "back", "left", "right" };

        var newSixDirection = new SixDirectionData();

        // 从Agent位置发射射线，先向下取整到整数坐标，然后各方向+0.5f
        Vector3 agentPos = agentTransform.position;
        Vector3Int agentVoxelPos = Vector3Int.FloorToInt(agentPos); // 去零头到整数
        Vector3 agentVoxelCenter = new Vector3(agentVoxelPos.x + 0.5f, agentVoxelPos.y + 0.5f, agentVoxelPos.z + 0.5f);

        // 将Agent位置转换为WorldGrid局部坐标
        Vector3Int agentLocalPos = worldGrid.WorldToLocalVoxelCoord(agentVoxelPos);

        float maxDistance = 10f; // 最大射线距离

        for (int i = 0; i < rayDirections.Length; i++)
        {
            // 创建射线，从体素中心+0.5f的位置开始
            Vector3 rayStart = agentVoxelCenter;
            Ray ray = new Ray(rayStart, rayDirections[i]);

            int distanceInt = 0;
            string voxelName = "";
            string voxelId = "";
            bool foundValidVoxel = false;

            // 直接跳过Agent当前位置一格，从下一格开始检测
            Vector3 skipRayStart = agentVoxelCenter + rayDirections[i] * 1.0f;
            Ray skipRay = new Ray(skipRayStart, rayDirections[i]);

            // 使用WorldGrid的射线检测，从跳过一格的位置开始
            bool hit = worldGrid.RaycastWorld(skipRay, maxDistance, out Vector3Int hitBlock, out Vector3Int hitNormal, out float distance);

            if (hit)
            {
                // 找到了非空气体素，获取其类型
                Vector3Int hitLocalPos = worldGrid.WorldToLocalVoxelCoord(hitBlock);
                
                // 特殊处理：如果检测到的是 y=0 层的基岩层，标记为 "base"
                if (hitLocalPos.y == 0)
                {
                    voxelName = "base_0";
                    voxelId = "1";
                    foundValidVoxel = true;
                    
                    // 计算体素距离：跳过的一格(1) + 检测到的距离
                    Vector3Int distanceVector = hitLocalPos - agentLocalPos;
                    distanceInt = Mathf.Max(1, Mathf.Abs(distanceVector.x) + Mathf.Abs(distanceVector.y) + Mathf.Abs(distanceVector.z));
                }
                else
                {
                    Voxel voxel = worldGrid.GetVoxel(hitLocalPos);

                    // 获取体素定义以获取名称
                    var voxelDef = VoxelRegistry.GetDefinition(voxel.TypeId);
                    voxelName = voxelDef?.name ?? "unknown";
                    voxelId = voxel.TypeId.ToString();
                    foundValidVoxel = true;

                    // 计算体素距离：跳过的一格(1) + 检测到的距离
                    Vector3Int distanceVector = hitLocalPos - agentLocalPos;
                    distanceInt = Mathf.Max(1, Mathf.Abs(distanceVector.x) + Mathf.Abs(distanceVector.y) + Mathf.Abs(distanceVector.z));
                }
            }

            if (!foundValidVoxel)
            {
                // 没有找到有效的体素，返回empty  
                voxelName = "empty";
                voxelId = "0";
                distanceInt = Mathf.FloorToInt(maxDistance);
            }

            var directionData = new DirectionData(voxelName, voxelId, distanceInt);

            switch (i)
            {
                case 0: newSixDirection.up = directionData; break;
                case 1: newSixDirection.down = directionData; break;
                case 2: newSixDirection.front = directionData; break;
                case 3: newSixDirection.back = directionData; break;
                case 4: newSixDirection.left = directionData; break;
                case 5: newSixDirection.right = directionData; break;
            }
        }

        UpdateSixDirection(newSixDirection);
    }

    /// <summary>
    /// 更新周围体素信息
    /// 注意：跳过 y=0 层的基岩层，因为它是不可改变的固定层
    /// </summary>
    private void UpdateNearbyVoxelsData()
    {
        if (agentTransform == null || worldGrid == null) return;

        // 获取Agent的世界坐标并转换为WorldGrid局部坐标
        Vector3Int agentVoxelPos = Vector3Int.FloorToInt(agentTransform.position);
        Vector3Int agentLocalPos = worldGrid.WorldToLocalVoxelCoord(agentVoxelPos);

        var voxelInfo = new List<NearbyVoxelData>();

        // 扫描指定半径内的体素
        for (int x = -voxelScanRadius; x <= voxelScanRadius; x++)
        {
            for (int y = -voxelScanRadius; y <= voxelScanRadius; y++)
            {
                for (int z = -voxelScanRadius; z <= voxelScanRadius; z++)
                {
                    Vector3Int checkLocalPos = agentLocalPos + new Vector3Int(x, y, z);
                    
                    // 跳过 y=0 层的基岩层（不可改变的固定层）
                    if (checkLocalPos.y == 0)
                        continue;
                    
                    Voxel voxel = worldGrid.GetVoxel(checkLocalPos);

                    if (!voxel.IsAir)
                    {
                        Vector3Int relativePos = new Vector3Int(x, y, z);

                        // 获取体素名称
                        var voxelDef = VoxelRegistry.GetDefinition(voxel.TypeId);
                        string voxelName = voxelDef?.name ?? "unknown";

                        // 创建 NearbyVoxelData 对象
                        var voxelData = new NearbyVoxelData(
                            new Vector3Data(relativePos.x, relativePos.y, relativePos.z),
                            voxelName,
                            voxel.TypeId.ToString()
                        );
                        voxelInfo.Add(voxelData);
                    }
                }
            }
        }

        UpdateNearbyVoxels(voxelInfo);
    }

    #region Public API - Game State Updates

    /// <summary>
    /// 更新 agent 位置（体素世界坐标）
    /// </summary>
    public void UpdateAgentPosition(Vector3Int position)
    {
        agentPosition = new Vector3(position.x, position.y, position.z);
        //Debug.Log($"GameStateManager: Agent voxel position updated to {position}");
    }

    /// <summary>
    /// 更新 player 相对位置（体素世界坐标）
    /// </summary>
    public void UpdatePlayerPositionRel(Vector3Int positionRel)
    {
        playerPositionRel = new Vector3(positionRel.x, positionRel.y, positionRel.z);
        //Debug.Log($"GameStateManager: Player relative voxel position updated to {positionRel}");
    }



    /// <summary>
    /// 更新六方向数据
    /// </summary>
    public void UpdateSixDirection(SixDirectionData sixDir)
    {
        sixDirection = sixDir;
        //Debug.Log($"GameStateManager: Six direction updated");
    }

    /// <summary>
    /// 更新附近的体素信息
    /// </summary>
    public void UpdateNearbyVoxels(List<NearbyVoxelData> voxels)
    {
        nearbyVoxels = voxels != null ? new List<NearbyVoxelData>(voxels) : new List<NearbyVoxelData>();
        //Debug.Log($"GameStateManager: Nearby voxels updated, count: {nearbyVoxels.Count}");
    }


    /// <summary>
    /// 设置完整的游戏状态（批量更新）
    /// </summary>
    public void SetGameState(Vector3 agentPos, Vector3 playerPosRel, SixDirectionData sixDir,
                           List<NearbyVoxelData> nearbyVox)
    {
        agentPosition = agentPos;
        playerPositionRel = playerPosRel;
        sixDirection = sixDir;
        nearbyVoxels = nearbyVox != null ? new List<NearbyVoxelData>(nearbyVox) : new List<NearbyVoxelData>();

        //Debug.Log($"GameStateManager: Complete game state updated");
    }

    #endregion

    #region Public API - Plans & Commands Management

    /// <summary>
    /// 添加新的待执行计划（自动维护最多 maxPendingPlans 个）
    /// </summary>
    public void AddPendingPlan(PendingPlanData plan)
    {
        if (plan == null) return;

        pendingPlans.Add(plan);

        // 保持最多 maxPendingPlans 个，移除最旧的
        if (pendingPlans.Count > maxPendingPlans)
        {
            pendingPlans.RemoveAt(0);
        }

        Debug.Log($"GameStateManager: Added pending plan '{plan.id}' - {plan.description}, total: {pendingPlans.Count}");
    }

    /// <summary>
    /// 移除指定的待执行计划
    /// </summary>
    public bool RemovePendingPlan(string planId)
    {
        var plan = pendingPlans.FirstOrDefault(p => p.id == planId);
        if (plan != null)
        {
            pendingPlans.Remove(plan);
            Debug.Log($"GameStateManager: Removed pending plan '{planId}', remaining: {pendingPlans.Count}");
            return true;
        }
        return false;
    }

    /// <summary>
    /// 清空所有待执行计划
    /// </summary>
    public void ClearPendingPlans()
    {
        pendingPlans.Clear();
        Debug.Log("GameStateManager: All pending plans cleared");
    }

    /// <summary>
    /// 添加新的最后执行命令（自动维护最多 maxLastCommands 个）
    /// </summary>
    public void AddLastCommand(LastCommandData command)
    {
        if (command == null) return;

        lastCommands.Add(command);

        // 保持最多 maxLastCommands 个，移除最旧的
        if (lastCommands.Count > maxLastCommands)
        {
            lastCommands.RemoveAt(0);
        }
    }

    /// <summary>
    /// 移除指定的最后执行命令
    /// </summary>
    public bool RemoveLastCommand(string commandId)
    {
        var command = lastCommands.FirstOrDefault(c => c.id == commandId);
        if (command != null)
        {
            lastCommands.Remove(command);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 清空所有最后执行命令
    /// </summary>
    public void ClearLastCommands()
    {
        lastCommands.Clear();
        Debug.Log("GameStateManager: All last commands cleared");
    }

    /// <summary>
    /// 更新指定命令的 phase
    /// </summary>
    public void UpdateLastCommandPhase(string commandId, string phase)
    {
        var command = lastCommands.FirstOrDefault(c => c.id == commandId);
        if (command != null)
        {
            command.phase = phase;
            //Debug.Log($"GameStateManager: Updated command {commandId} phase to {phase}");
        }
    }

    #endregion

    #region Public API - Get Current Game State

    /// <summary>
    /// 获取当前完整的游戏状态快照
    /// </summary>
    public GameState GetCurrentGameState()
    {
        // 获取所有体素定义
        List<VoxelDefinitionData> voxelDefinitions = GetVoxelDefinitions();

        var gameState = new GameState
        {
            timestamp = TimestampUtils.GenerateTimestamp(),
            agent_position = TimestampUtils.ToVector3Data(agentPosition),
            player_position_rel = TimestampUtils.ToVector3Data(playerPositionRel),
            six_direction = sixDirection,
            nearby_voxels = nearbyVoxels != null ? new List<NearbyVoxelData>(nearbyVoxels) : new List<NearbyVoxelData>(),
            pending_plans = new List<PendingPlanData>(pendingPlans), // 创建副本
            last_commands = new List<LastCommandData>(lastCommands), // 创建副本
            voxel_definitions = voxelDefinitions
        };

        Debug.Log($"GameStateManager: Generated game state snapshot - " +
                  $"Plans: {gameState.pending_plans.Count}, Commands: {gameState.last_commands.Count}, " +
                  $"VoxelDefinitions: {gameState.voxel_definitions?.Count ?? 0}");

        return gameState;
    }

    /// <summary>
    /// 从VoxelJsonDB获取所有体素定义并转换为VoxelDefinitionData列表
    /// </summary>
    private List<VoxelDefinitionData> GetVoxelDefinitions()
    {
        var voxelDefinitions = new List<VoxelDefinitionData>();

        try
        {
            // 获取VoxelJsonDB实例
            var voxelJsonDB = VoxelJsonDB.Instance;
            if (voxelJsonDB == null || !voxelJsonDB.IsInitialized)
            {
                Debug.LogWarning("GameStateManager: VoxelJsonDB not initialized, skipping voxel definitions");
                return voxelDefinitions;
            }

            // 获取所有体素条目
            var entries = voxelJsonDB.GetAllVoxelEntries();
            if (entries == null)
            {
                Debug.LogWarning("GameStateManager: Failed to get voxel entries from VoxelJsonDB");
                return voxelDefinitions;
            }

            // 转换为VoxelDefinitionData
            foreach (var entry in entries)
            {
                // 确保face_textures数组长度为6
                string[] faceTextures = entry.face_textures;
                if (faceTextures == null || faceTextures.Length != 6)
                {
                    faceTextures = new string[6];
                    if (entry.face_textures != null)
                    {
                        Array.Copy(entry.face_textures, faceTextures, Math.Min(entry.face_textures.Length, 6));
                    }
                }

                var voxelDefData = new VoxelDefinitionData(
                    entry.id,
                    entry.name ?? "",
                    faceTextures,
                    entry.description ?? ""
                );

                voxelDefinitions.Add(voxelDefData);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"GameStateManager: Error getting voxel definitions: {ex.Message}\nStackTrace: {ex.StackTrace}");
        }

        return voxelDefinitions;
    }

    /// <summary>
    /// 获取一个最新计算的游戏状态快照（忽略自动监控开关，仅在调用时计算一次）。
    /// 适用于发送事件前的按需采集，避免持续记录。
    /// </summary>
    public GameState GetFreshGameStateSnapshot()
    {
            // 确保依赖被初始化
            InitializeAutoMonitoring();

            // 立即执行一次监控采集（不依赖帧间隔）
            PerformAutoMonitoring();

            // 基于最新内部数据返回快照
        return GetCurrentGameState();
    }

    /// <summary>
    /// 获取当前待执行计划列表（只读）
    /// </summary>
    public IReadOnlyList<PendingPlanData> GetPendingPlans()
    {
        return pendingPlans.AsReadOnly();
    }

    /// <summary>
    /// 获取当前最后执行命令列表（只读）
    /// </summary>
    public IReadOnlyList<LastCommandData> GetLastCommands()
    {
        return lastCommands.AsReadOnly();
    }

    /// <summary>
    /// 批量添加 pending plans（从 PlanBatch）
    /// </summary>
    public void AddPendingPlansFromBatch(PlanBatch planBatch)
    {
        if (planBatch.plan == null || planBatch.plan.Length == 0)
            return;

        // 将所有 plan items 添加到 pending plans
        foreach (var planItem in planBatch.plan)
        {
            var pendingPlan = new PendingPlanData(
                planItem.id,
                planBatch.goal_id,
                planBatch.goal_label,
                planItem.action_type,
                planItem.description,
                planItem.depends_on
            );
            AddPendingPlan(pendingPlan);
        }
    }

    /// <summary>
    /// 批量添加 last commands（从 CommandBatch）
    /// </summary>
    public void AddLastCommandsFromBatch(CommandBatch commandBatch)
    {
        if (commandBatch.commands == null || commandBatch.commands.Count == 0)
            return;

        // 获取 goal_label（可能需要从存储中获取）
        string goalLabel = "";
        if (!string.IsNullOrEmpty(commandBatch.goal_id))
        {
            goalLabel = GoalLabelStorage.GetGoalLabel(commandBatch.goal_id);
        }

        // 将所有 commands 添加到 last commands
        foreach (var command in commandBatch.commands)
        {
            // 使用 command 中的 phase，如果没有则默认为 "pending"
            string phase = !string.IsNullOrEmpty(command.phase) ? command.phase : "pending";
            
            // 规范化 params_data 为 JSON 字符串（保持原始格式，避免类型不匹配导致的数据丢失）
            string paramsJsonString = NormalizeParamsDataToJson(command.params_data, command.type);
            
            var lastCommand = new LastCommandData(
                command.id,
                command.goal_id ?? commandBatch.goal_id,
                command.goal_label ?? goalLabel,
                command.type,
                paramsJsonString, // 存储为 JSON 字符串
                phase
            );
            AddLastCommand(lastCommand);
        }
    }

    /// <summary>
    /// 规范化 params_data 为 JSON 字符串
    /// 如果已经是字符串，直接返回；如果是对象，序列化为 JSON
    /// </summary>
    private string NormalizeParamsDataToJson(object paramsData, string commandType)
    {
        if (paramsData == null)
        {
            Debug.LogWarning($"GameStateManager: Command {commandType} has null params_data, will use empty object");
            return "{}";
        }

        // 如果已经是字符串（JSON 字符串），直接返回
        if (paramsData is string jsonString)
        {
            // 验证是否是有效的 JSON 对象
            if (jsonString.TrimStart().StartsWith("{"))
            {
                return jsonString;
            }
            else
            {
                Debug.LogWarning($"GameStateManager: params_data string is not valid JSON object, wrapping: {jsonString}");
                return "{}";
            }
        }

        // 如果是空字典，返回空对象
        if (paramsData is System.Collections.IDictionary dict && dict.Count == 0)
        {
            return "{}";
        }

        // 如果是对象，尝试序列化为 JSON
        try
        {
            string json = UnityEngine.JsonUtility.ToJson(paramsData, false);
            return string.IsNullOrEmpty(json) ? "{}" : json;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"GameStateManager: Failed to serialize params_data for {commandType}: {ex.Message}, using empty object");
            return "{}";
        }
    }

    /// <summary>
    /// 批量移除已发送的 pending plans
    /// </summary>
    public void RemovePendingPlans(PlanItem[] approvedPlans)
    {
        if (approvedPlans == null || approvedPlans.Length == 0)
            return;

        // 移除所有已发送的 plans
        foreach (var approvedPlan in approvedPlans)
        {
            RemovePendingPlan(approvedPlan.id);
        }
    }

    /// <summary>
    /// 更新 last command 的 phase（从 CommandStatus）
    /// </summary>
    public void UpdateLastCommandPhaseFromStatus(string commandId, CommandStatus status)
    {
        // 将 CommandStatus 枚举转换为字符串 phase
        string phase = status.ToString().ToLower();
        UpdateLastCommandPhase(commandId, phase);
    }

    #endregion
}
