using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// ContinuePlan命令执行器 - 继续执行计划
/// </summary>
public class ContinuePlanExecutor : MonoBehaviour, ICommandExecutor
{
    [Header("References")]
    [SerializeField] private AgentCam agentCam;
    
    private bool _isExecuting = false;
    
    public string CommandType => "continue_plan";
    public bool CanInterrupt => true;
    
    private void OnEnable()
    {
        if (agentCam == null)
        {
            agentCam = FindAnyObjectByType<AgentCam>();
        }
    }
    
    public void Execute(string commandId, object paramsData, Action<bool, string> onComplete)
    {
        if (_isExecuting)
        {
            onComplete?.Invoke(false, "Another continue_plan is already in progress");
            return;
        }
        
        ContinuePlanParams @params = ParseParams(paramsData);
        if (@params == null)
        {
            onComplete?.Invoke(false, "Invalid ContinuePlanParams");
            return;
        }
        
        _isExecuting = true;
        
        // 如果需要快照，先拍照
        if (@params.request_snapshot && agentCam != null)
        {
            agentCam.TakeFourDirectionPhotos((photoFileNames) =>
            {
                if (photoFileNames == null || photoFileNames.Count == 0)
                {
                    _isExecuting = false;
                    onComplete?.Invoke(false, "Failed to take snapshot");
                    return;
                }
                
                // Show agent images message (four photos), in the order of front/back/left/right.
                try
                {
                    var chatUI = FindAnyObjectByType<ChatUI>();
                    if (chatUI != null && agentCam != null)
                    {
                        // Sort by direction keywords in file names
                        List<string> ordered = new List<string>();
                        string[] keys = new string[] { "Front", "Back", "Left", "Right" };
                        foreach (var key in keys)
                        {
                            string found = photoFileNames.Find(name => name != null && name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);
                            if (!string.IsNullOrEmpty(found))
                            {
                                ordered.Add(found);
                            }
                        }
                        // If not all directions are matched, use the original order to fill the remaining items
                        if (ordered.Count < photoFileNames.Count)
                        {
                            foreach (var n in photoFileNames)
                            {
                                if (!ordered.Contains(n)) ordered.Add(n);
                            }
                        }

                        // Convert to full path list
                        List<string> fullPaths = new List<string>();
                        foreach (var name in ordered)
                        {
                            string p = agentCam.GetPhotoFullPath(name);
                            if (!string.IsNullOrEmpty(p)) fullPaths.Add(p);
                        }
                        chatUI.ShowAgentImagesMessage(fullPaths, "Hey i just looked around, let's see what next to do here...");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"ContinuePlanExecutor: failed to show agent perception image: {e.Message}");
                }

                // Convert to ImageData list
                List<ImageData> imageDataList = new List<ImageData>();
                foreach (string fileName in photoFileNames)
                {
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        imageDataList.Add(new ImageData(fileName: fileName));
                    }
                }
                
                // Convert array to string
                string nextStepsStr = "";
                if (@params.possible_next_steps != null && @params.possible_next_steps.Length > 0)
                {
                    nextStepsStr = string.Join(", ", @params.possible_next_steps);
                }
                
                // Publish event
                var payload = new AgentContinuePlanPayload(
                    @params.current_summary,
                    nextStepsStr,
                    imageDataList
                );
                EventBus.Publish(payload);
                
                _isExecuting = false;
                onComplete?.Invoke(true, null);
            });
        }
        else
        {
            // 将数组转换为字符串
            string nextStepsStr = "";
            if (@params.possible_next_steps != null && @params.possible_next_steps.Length > 0)
            {
                nextStepsStr = string.Join(", ", @params.possible_next_steps);
            }
            
            // 不需要快照，直接发布事件
            var payload = new AgentContinuePlanPayload(
                @params.current_summary,
                nextStepsStr,
                null
            );
            EventBus.Publish(payload);
            
            _isExecuting = false;
            onComplete?.Invoke(true, null);
        }
        
        Debug.Log($"ContinuePlanExecutor: Continuing plan with summary: {@params.current_summary}");
    }
    
    private ContinuePlanParams ParseParams(object paramsData)
    {
        // 如果已经是正确类型，直接返回
        if (paramsData is ContinuePlanParams)
        {
            return (ContinuePlanParams)paramsData;
        }
        
        // 如果是字符串（JSON字符串），直接解析
        if (paramsData is string jsonString)
        {
            try
            {
                return JsonUtility.FromJson<ContinuePlanParams>(jsonString);
            }
            catch (Exception e)
            {
                Debug.LogError($"ContinuePlanExecutor: Failed to parse params from JSON string: {e.Message}");
                return null;
            }
        }
        
        // 尝试将对象转换为JSON再解析
        try
        {
            string json = JsonUtility.ToJson(paramsData);
            return JsonUtility.FromJson<ContinuePlanParams>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"ContinuePlanExecutor: Failed to parse params: {e.Message}");
            return null;
        }
    }
    
    public void Interrupt()
    {
        _isExecuting = false;
    }
}

