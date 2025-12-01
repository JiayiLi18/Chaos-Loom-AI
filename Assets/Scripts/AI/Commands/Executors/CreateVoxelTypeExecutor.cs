using UnityEngine;
using Voxels;
using System;

/// <summary>
/// CreateVoxelType命令执行器 - 创建新的体素类型
/// </summary>
public class CreateVoxelTypeExecutor : MonoBehaviour, ICommandExecutor
{
    [Header("References")]
    [SerializeField] private RuntimeVoxelCreator runtimeVoxelCreator;
    
    private bool _isExecuting = false;
    
    public string CommandType => "create_voxel_type";
    public bool CanInterrupt => false; // 创建操作通常很快，不需要中断
    
    private void OnEnable()
    {
        if (runtimeVoxelCreator == null)
        {
            runtimeVoxelCreator = RuntimeVoxelCreator.Instance;
            if (runtimeVoxelCreator == null)
            {
                runtimeVoxelCreator = FindAnyObjectByType<RuntimeVoxelCreator>();
                if (runtimeVoxelCreator == null)
                {
                    Debug.LogError("CreateVoxelTypeExecutor: RuntimeVoxelCreator not found");
                }
            }
        }
    }
    
    public void Execute(string commandId, object paramsData, Action<bool, string> onComplete)
    {
        if (_isExecuting)
        {
            onComplete?.Invoke(false, "Another creation is already in progress");
            return;
        }
        
        CreateVoxelTypeParams @params = ParseParams(paramsData);
        if (@params == null || @params.voxel_type == null)
        {
            onComplete?.Invoke(false, "Invalid CreateVoxelTypeParams");
            return;
        }
        
        _isExecuting = true;
        
        try
        {
            VoxelTypeData voxelType = @params.voxel_type;
            
            if (runtimeVoxelCreator == null)
            {
                _isExecuting = false;
                onComplete?.Invoke(false, "RuntimeVoxelCreator not available");
                return;
            }
            
            // 直接传递字符串数组，由RuntimeVoxelCreator统一处理纹理转换
            runtimeVoxelCreator.CreateVoxelTypeForAgent(
                voxelType.name,
                voxelType.description,
                voxelType.face_textures
            );
            
            Debug.Log($"CreateVoxelTypeExecutor: Created voxel type '{voxelType.name}' with ID '{voxelType.id}'");
            
            _isExecuting = false;
            onComplete?.Invoke(true, null);
        }
        catch (Exception e)
        {
            _isExecuting = false;
            onComplete?.Invoke(false, $"Exception during voxel type creation: {e.Message}");
        }
    }
    
    
    private CreateVoxelTypeParams ParseParams(object paramsData)
    {
        // 如果已经是正确类型，直接返回
        if (paramsData is CreateVoxelTypeParams)
        {
            return (CreateVoxelTypeParams)paramsData;
        }
        
        // 如果是字符串（JSON字符串），直接解析
        if (paramsData is string jsonString)
        {
            try
            {
                return JsonUtility.FromJson<CreateVoxelTypeParams>(jsonString);
            }
            catch (Exception e)
            {
                Debug.LogError($"CreateVoxelTypeExecutor: Failed to parse params from JSON string: {e.Message}");
                return null;
            }
        }
        
        // 尝试将对象转换为JSON再解析
        try
        {
            string json = JsonUtility.ToJson(paramsData);
            return JsonUtility.FromJson<CreateVoxelTypeParams>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"CreateVoxelTypeExecutor: Failed to parse params: {e.Message}");
            return null;
        }
    }
    
    public void Interrupt()
    {
        _isExecuting = false;
    }
}

