using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 存储goal_id到goal_label的映射
/// 在PlanPermissionRequest阶段存储，在CommandBatch解析时使用
/// </summary>
public static class GoalLabelStorage
{
    private static Dictionary<string, string> _goalLabels = new Dictionary<string, string>();
    
    /// <summary>
    /// 存储goal label
    /// </summary>
    public static void StoreGoalLabel(string goalId, string goalLabel)
    {
        if (string.IsNullOrEmpty(goalId))
        {
            Debug.LogWarning("GoalLabelStorage: Cannot store goal label with empty goal_id");
            return;
        }
        
        _goalLabels[goalId] = goalLabel;
        Debug.Log($"GoalLabelStorage: Stored goal_label '{goalLabel}' for goal_id '{goalId}'");
    }
    
    /// <summary>
    /// 获取goal label
    /// </summary>
    public static string GetGoalLabel(string goalId)
    {
        if (string.IsNullOrEmpty(goalId))
        {
            return null;
        }
        
        return _goalLabels.TryGetValue(goalId, out string label) ? label : null;
    }
    
    /// <summary>
    /// 清空所有存储的goal labels
    /// </summary>
    public static void Clear()
    {
        _goalLabels.Clear();
        Debug.Log("GoalLabelStorage: Cleared all goal labels");
    }
}

