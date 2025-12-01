using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// MoveTo命令执行器 - 移动Agent到指定位置（不使用NavMesh，直接移动Transform）
/// 按照左右(x)、上下(y)、前后(z)的顺序移动
/// </summary>
public class MoveToExecutor : MonoBehaviour, ICommandExecutor
{
    [Header("References")]
    [SerializeField] private Transform agentTransform;
    
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f; // 移动速度（单位/秒）
    [SerializeField] private float movementTolerance = 0.1f; // 到达目标的容差
    
    private bool _isExecuting = false;
    private Coroutine _moveCoroutine;
    
    public string CommandType => "move_to";
    public bool CanInterrupt => true;
    
    private void OnEnable()
    {
        if (agentTransform == null)
        {
            GameObject agent = GameObject.FindGameObjectWithTag("Agent");
            if (agent != null)
            {
                agentTransform = agent.transform;
            }
            else
            {
                Debug.LogError("MoveToExecutor: Agent Transform not found (Tag: 'Agent' not set)");
            }
        }
    }
    
    public void Execute(string commandId, object paramsData, Action<bool, string> onComplete)
    {
        if (_isExecuting)
        {
            onComplete?.Invoke(false, "Agent is already moving");
            return;
        }
        
        MoveToParams @params = ParseParams(paramsData);
        if (@params == null)
        {
            onComplete?.Invoke(false, "Invalid MoveToParams");
            return;
        }
        
        if (agentTransform == null)
        {
            onComplete?.Invoke(false, "Agent Transform not found");
            return;
        }
        
        _isExecuting = true;
        
        // 计算目标相对位置（左右、上下、前后）
        Vector3 relativePos = new Vector3(@params.target_pos.x, @params.target_pos.y, @params.target_pos.z);
        
        // 计算目标世界坐标
        // x = 左右（right/left），y = 上下（up/down），z = 前后（forward/back）
        Vector3 targetWorldPos = agentTransform.position + 
                                 agentTransform.right * relativePos.x +      // 左右
                                 agentTransform.up * relativePos.y +          // 上下
                                 agentTransform.forward * relativePos.z;      // 前后
        
        // 启动协程执行移动
        _moveCoroutine = StartCoroutine(MoveToPosition(targetWorldPos, onComplete));
        
        Debug.Log($"MoveToExecutor: Moving agent from {agentTransform.position} to relative position ({relativePos.x}, {relativePos.y}, {relativePos.z}) -> world pos {targetWorldPos}");
    }
    
    /// <summary>
    /// 协程：移动Agent到目标位置
    /// </summary>
    private IEnumerator MoveToPosition(Vector3 targetPosition, Action<bool, string> onComplete)
    {
        float timeout = 30f; // 30秒超时
        float elapsed = 0f;
        
        while (elapsed < timeout)
        {
            if (agentTransform == null)
            {
                _isExecuting = false;
                _moveCoroutine = null;
                onComplete?.Invoke(false, "Agent Transform became null");
                yield break;
            }
            
            // 计算到目标的距离
            Vector3 currentPos = agentTransform.position;
            Vector3 direction = targetPosition - currentPos;
            float distance = direction.magnitude;
            
            // 如果已经到达目标位置
            if (distance < movementTolerance)
            {
                agentTransform.position = targetPosition; // 确保精确到达
                _isExecuting = false;
                _moveCoroutine = null;
                onComplete?.Invoke(true, null);
                yield break;
            }
            
            // 移动Agent
            Vector3 moveStep = direction.normalized * moveSpeed * Time.deltaTime;
            agentTransform.position += moveStep;
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // 超时
        _isExecuting = false;
        _moveCoroutine = null;
        onComplete?.Invoke(false, "Movement timeout");
    }
    
    private MoveToParams ParseParams(object paramsData)
    {
        // 如果已经是正确类型，直接返回
        if (paramsData is MoveToParams)
        {
            return (MoveToParams)paramsData;
        }
        
        // 如果是字符串（JSON字符串），直接解析
        if (paramsData is string jsonString)
        {
            try
            {
                return JsonUtility.FromJson<MoveToParams>(jsonString);
            }
            catch (Exception e)
            {
                Debug.LogError($"MoveToExecutor: Failed to parse params from JSON string: {e.Message}");
                return null;
            }
        }
        
        // 尝试将对象转换为JSON再解析
        try
        {
            string json = JsonUtility.ToJson(paramsData);
            return JsonUtility.FromJson<MoveToParams>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"MoveToExecutor: Failed to parse params: {e.Message}");
            return null;
        }
    }
    
    public void Interrupt()
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }
        
        _isExecuting = false;
        Debug.Log("MoveToExecutor: Movement interrupted");
    }
}

