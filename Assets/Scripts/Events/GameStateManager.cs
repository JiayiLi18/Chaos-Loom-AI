using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Voxels;

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
    [SerializeField, TextArea(10, 20)] private string nearbyVoxels = "";

    [Header("Plans & Commands")]
    [SerializeField] private List<PendingPlanData> pendingPlans = new List<PendingPlanData>();
    [SerializeField] private List<LastCommandData> lastCommands = new List<LastCommandData>();

    [Header("Settings")]
    [SerializeField] private int maxPendingPlans = 3;
    [SerializeField] private int maxLastCommands = 3;

    [Header("Auto Monitoring Settings")]
    [SerializeField] private bool enableAutoMonitoring = true;
    [SerializeField] private int voxelScanRadius = 2;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform agentTransform;
    [SerializeField] private WorldGrid worldGrid;

    private void Awake()
    {
        // 初始化游戏时间系统
        GameTime.Initialize();

        // 确保列表初始化
        if (pendingPlans == null) pendingPlans = new List<PendingPlanData>();
        if (lastCommands == null) lastCommands = new List<LastCommandData>();

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
        if (enableAutoMonitoring)
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
                var follower = FindFirstObjectByType<Follower>();
                //TODO: follower后续改为自动查找Agent
                if (follower != null)
                {
                    agentTransform = follower.transform;
                }
                else
                {
                    Debug.LogWarning("GameStateManager: Could not find Follower component for auto monitoring");
                }
            }

            // 自动查找 WorldGrid
            if (worldGrid == null)
            {
                worldGrid = FindFirstObjectByType<WorldGrid>();
            }
            else
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
        if (!enableAutoMonitoring)
        {
            Debug.LogWarning("GameStateManager: Auto monitoring is disabled");
            return;
        }

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

        Debug.Log($"GameStateManager: Auto monitoring completed - Agent: {agentVoxelPos}, Player Rel: {playerPosRel}");
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
    /// </summary>
    private void UpdateNearbyVoxelsData()
    {
        if (agentTransform == null || worldGrid == null) return;

        // 获取Agent的世界坐标并转换为WorldGrid局部坐标
        Vector3Int agentVoxelPos = Vector3Int.FloorToInt(agentTransform.position);
        Vector3Int agentLocalPos = worldGrid.WorldToLocalVoxelCoord(agentVoxelPos);

        var voxelInfo = new List<string>();

        // 扫描指定半径内的体素
        for (int x = -voxelScanRadius; x <= voxelScanRadius; x++)
        {
            for (int y = -voxelScanRadius; y <= voxelScanRadius; y++)
            {
                for (int z = -voxelScanRadius; z <= voxelScanRadius; z++)
                {
                    Vector3Int checkLocalPos = agentLocalPos + new Vector3Int(x, y, z);
                    Voxel voxel = worldGrid.GetVoxel(checkLocalPos);

                    if (!voxel.IsAir)
                    {
                        Vector3Int relativePos = new Vector3Int(x, y, z);

                        // 获取体素名称
                        var voxelDef = VoxelRegistry.GetDefinition(voxel.TypeId);
                        string voxelName = voxelDef?.name ?? "unknown";

                        voxelInfo.Add($"({relativePos.x},{relativePos.y},{relativePos.z}):{voxelName}({voxel.TypeId})");
                    }
                }
            }
        }

        string voxelString = string.Join(";\n", voxelInfo);
        UpdateNearbyVoxels(voxelString);
    }

    #region Public API - Game State Updates

    /// <summary>
    /// 更新 agent 位置（体素世界坐标）
    /// </summary>
    public void UpdateAgentPosition(Vector3Int position)
    {
        agentPosition = new Vector3(position.x, position.y, position.z);
        Debug.Log($"GameStateManager: Agent voxel position updated to {position}");
    }

    /// <summary>
    /// 更新 player 相对位置（体素世界坐标）
    /// </summary>
    public void UpdatePlayerPositionRel(Vector3Int positionRel)
    {
        playerPositionRel = new Vector3(positionRel.x, positionRel.y, positionRel.z);
        Debug.Log($"GameStateManager: Player relative voxel position updated to {positionRel}");
    }



    /// <summary>
    /// 更新六方向数据
    /// </summary>
    public void UpdateSixDirection(SixDirectionData sixDir)
    {
        sixDirection = sixDir;
        Debug.Log($"GameStateManager: Six direction updated");
    }

    /// <summary>
    /// 更新附近的体素信息
    /// </summary>
    public void UpdateNearbyVoxels(string voxels)
    {
        nearbyVoxels = voxels ?? "";
        Debug.Log($"GameStateManager: Nearby voxels updated");
    }


    /// <summary>
    /// 设置完整的游戏状态（批量更新）
    /// </summary>
    public void SetGameState(Vector3 agentPos, Vector3 playerPosRel, SixDirectionData sixDir,
                           string nearbyVox)
    {
        agentPosition = agentPos;
        playerPositionRel = playerPosRel;
        sixDirection = sixDir;
        nearbyVoxels = nearbyVox ?? "";

        Debug.Log($"GameStateManager: Complete game state updated");
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

        Debug.Log($"GameStateManager: Added last command '{command.id}' - {command.type}, total: {lastCommands.Count}");
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
            Debug.Log($"GameStateManager: Removed last command '{commandId}', remaining: {lastCommands.Count}");
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

    #endregion

    #region Public API - Get Current Game State

    /// <summary>
    /// 获取当前完整的游戏状态快照
    /// </summary>
    public GameState GetCurrentGameState()
    {
        var gameState = new GameState
        {
            timestamp = TimestampUtils.GenerateTimestamp(),
            agent_position = TimestampUtils.ToVector3Data(agentPosition),
            player_position_rel = TimestampUtils.ToVector3Data(playerPositionRel),
            six_direction = sixDirection,
            nearby_voxels = nearbyVoxels,
            pending_plans = new List<PendingPlanData>(pendingPlans), // 创建副本
            last_commands = new List<LastCommandData>(lastCommands) // 创建副本
        };

        Debug.Log($"GameStateManager: Generated game state snapshot - " +
                  $"Plans: {gameState.pending_plans.Count}, Commands: {gameState.last_commands.Count}");

        return gameState;
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

    #endregion

    #region Public API - Auto Monitoring Control

    /// <summary>
    /// 启用或禁用自动监控
    /// </summary>
    public void SetAutoMonitoring(bool enabled)
    {
        enableAutoMonitoring = enabled;
        Debug.Log($"GameStateManager: Auto monitoring {(enabled ? "enabled" : "disabled")}");
    }


    /// <summary>
    /// 设置体素扫描半径
    /// </summary>
    public void SetVoxelScanRadius(int radius)
    {
        voxelScanRadius = Mathf.Max(1, Mathf.Min(10, radius));
        Debug.Log($"GameStateManager: Voxel scan radius set to {voxelScanRadius}");
    }

    /// <summary>
    /// 手动触发一次监控更新
    /// </summary>
    public void TriggerManualUpdate()
    {
        if (enableAutoMonitoring)
        {
            PerformAutoMonitoring();
            Debug.Log("GameStateManager: Manual monitoring update triggered");
        }
    }

    /// <summary>
    /// 重新初始化自动监控组件
    /// </summary>
    public void ReinitializeAutoMonitoring()
    {
        InitializeAutoMonitoring();
        Debug.Log("GameStateManager: Auto monitoring reinitialized");
    }

    #endregion

    #region Debug & Inspector

    /// <summary>
    /// 获取调试信息
    /// </summary>
    public string GetDebugInfo()
    {
        return $"Agent Pos: {agentPosition}, Player Rel Pos: {playerPositionRel}\n" +
               $"Pending Plans: {pendingPlans.Count}, Last Commands: {lastCommands.Count}\n" +
               $"Nearby Voxels: {(string.IsNullOrEmpty(nearbyVoxels) ? "None" : "Set")}\n" +
               $"Auto Monitoring: {(enableAutoMonitoring ? "Enabled" : "Disabled")}, " +
               $"Radius: {voxelScanRadius}\n" +
               $"Player Transform: {(playerTransform != null ? "Found" : "Missing")}, " +
               $"Agent Transform: {(agentTransform != null ? "Found" : "Missing")}, " +
               $"WorldGrid: {(worldGrid != null ? "Found" : "Missing")}";
    }

    [ContextMenu("Log Current State")]
    private void LogCurrentState()
    {
        Debug.Log($"=== GameStateManager Current State ===\n{GetDebugInfo()}");

        for (int i = 0; i < pendingPlans.Count; i++)
        {
            var plan = pendingPlans[i];
            Debug.Log($"Pending Plan {i}: {plan.id} - {plan.action_type} - {plan.description}");
        }

        for (int i = 0; i < lastCommands.Count; i++)
        {
            var cmd = lastCommands[i];
            Debug.Log($"Last Command {i}: {cmd.id} - {cmd.type} - {cmd.phase}");
        }
    }

    [ContextMenu("Trigger Manual Update")]
    private void ContextMenuTriggerUpdate()
    {
        TriggerManualUpdate();
    }

    [ContextMenu("Toggle Auto Monitoring")]
    private void ContextMenuToggleMonitoring()
    {
        SetAutoMonitoring(!enableAutoMonitoring);
    }

    [ContextMenu("Reinitialize Auto Monitoring")]
    private void ContextMenuReinitialize()
    {
        ReinitializeAutoMonitoring();
    }

    [ContextMenu("Test Game Time")]
    private void TestGameTime()
    {
        Debug.Log($"Current Game Time: {GameTime.GenerateTimestamp()}");
        Debug.Log($"Game Time in Seconds: {GameTime.GetGameTime():F2}");
    }

    #endregion
}
