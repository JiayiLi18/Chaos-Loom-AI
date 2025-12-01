using UnityEngine;
using Voxels;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;
/// <summary>
/// 负责场景中体素的实际建造和删除功能
/// </summary>
public class RuntimeVoxelBuilding : MonoBehaviour
{
    [Header("References")]
    private VoxelPreviewManager _previewManager;
    [SerializeField] private VoxelInventoryUI _voxelInventoryUI;

    [Header("Building Settings")]
    [SerializeField] private float maxDistance = 5f;
    [SerializeField] private LayerMask voxelLayer;

    [Header("Preview Settings")]
    [SerializeField] private Color hoverColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color placeColor = new Color(0f, 1f, 0f, 0.3f);


    private Camera _cam;
    private WorldGrid _world;
    private Vector3Int _hoverVoxel = new(-999, -999, -999);
    private Vector3Int _hoverNormal = Vector3Int.zero;
    private bool _isEnabled = true;
    private byte _selectedType = 1;

    private bool _isInitialized = false;

    // 添加一个静态实例，方便其他组件访问
    public static RuntimeVoxelBuilding Instance { get; private set; }

    // 批量收集配置
    [Header("Batch Collection Settings")]
    [SerializeField] private float batchCollectInterval = 5f; // 批量收集时间间隔（秒）
    
    private List<VoxelInstanceData> _pendingInstances = new List<VoxelInstanceData>();
    private float _lastBuildTime = 0f;

    [Header("Inspector Debug")]
    [SerializeField] private List<VoxelInstanceData> inspectorPendingInstances = new List<VoxelInstanceData>();
    [SerializeField] private List<VoxelInstanceData> lastSentInstancesSnapshot = new List<VoxelInstanceData>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        _cam = Camera.main;
        if (_cam == null)
        {
            Debug.LogError("找不到主摄像机，请确保场景中有标记为MainCamera的相机");
            enabled = false;
            return;
        }

        _world = FindAnyObjectByType<WorldGrid>();
        if (_world == null)
        {
            Debug.LogError("找不到WorldGrid组件，请确保场景中存在WorldGrid");
            enabled = false;
            return;
        }
        
        // 获取预览管理器
        _previewManager = FindAnyObjectByType<VoxelPreviewManager>();
        if (_previewManager == null)
        {
            Debug.LogWarning("找不到VoxelPreviewManager组件，预览功能将不可用");
        }

        if (_voxelInventoryUI == null)
        {
            _voxelInventoryUI = FindAnyObjectByType<VoxelInventoryUI>();
            if (_voxelInventoryUI == null)
            {
                Debug.LogError("找不到VoxelInventoryUI组件");
            }
        }
        

        _isInitialized = true;
    }

    private void OnEnable()
    {
        if (!_isInitialized) 
        {
            InitializeComponents();
        }

        // 启用预览管理器
        if (_previewManager != null)
        {
            _previewManager.enabled = true;
        }

        // 订阅“立即刷新建造事件”请求
        EventBus.Subscribe<FlushPlayerBuildRequest>(OnFlushPlayerBuildRequest);
    }

    private void OnDisable()
    {
        // 禁用预览管理器
        if (_previewManager != null)
        {
            _previewManager.HidePreview();
            _previewManager.enabled = false;
        }
        
        // 取消订阅
        EventBus.Unsubscribe<FlushPlayerBuildRequest>(OnFlushPlayerBuildRequest);

        // 发送剩余的批量事件
        if (_pendingInstances.Count > 0)
        {
            SendBatchEvent();
        }
    }

    private void OnDestroy()
    {
        
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // 提供一个公共方法来设置当前选中的方块类型
    public void SetSelectedVoxelType(byte typeId)
    {
        _selectedType = typeId;
    }
    
    /// <summary>
    /// 处理UI发出的方块类型选择事件
    /// </summary>
    private void OnVoxelTypeSelected(VoxelDefinition voxelDef)
    {
        if (voxelDef != null)
        {
            SetSelectedVoxelType((byte)voxelDef.typeId);
            Debug.Log($"[RuntimeVoxelBuilding] Selected voxel type: {voxelDef.displayName} (ID: {voxelDef.typeId})");
        }
    }
    
    /// <summary>
    /// 控制Add按钮状态，间接控制VoxelEditingUI
    /// </summary>
    /// <param name="enable">true为打开Add按钮（创建模式），false为关闭</param>
    public void SetAddButtonState(bool enable)
    {
    }
    
    /// <summary>
    /// 切换到创建模式（打开Add按钮）
    /// </summary>
    public void EnterCreationMode()
    {
        SetAddButtonState(true);
    }
    
    /// <summary>
    /// 切换到建造模式（关闭Add按钮）
    /// </summary>
    public void EnterBuildingMode()
    {
        SetAddButtonState(false);
    }
    

    private void Update()
    {
        if (!_isEnabled) return;

        //如果鼠标悬停在UI上，则不更新
        if (EventSystem.current.IsPointerOverGameObject())
        {
            if (_previewManager != null)
            {
                _previewManager.HidePreview();
            }
            return;
        }
        
        if (_voxelInventoryUI != null && _voxelInventoryUI._selectedSlot != null)
        {
            _selectedType = (byte)_voxelInventoryUI._selectedSlot.VoxelDef.typeId;
            SetSelectedVoxelType(_selectedType);
        }

        UpdateHoverVoxel();
        UpdatePreview();
        HandleInput();
    }

    private void HandleInput()
    {
        // 检查是否需要发送待发送的批量事件
        CheckAndSendBatch();
        
        // 正常的方块放置/删除逻辑
        if (Input.GetMouseButtonDown(1))
        {
            // 右键删除方块
            if (_hoverVoxel.x != -999)
            {
                Vector3Int placePos = _hoverVoxel;
                _world.SetVoxelWorld(placePos, Voxel.Air);

                // 记录删除操作（air 体素）
                RecordVoxelOperation("0", "air", placePos);
            }
        }
        else if (Input.GetMouseButtonDown(0))
        {
            // 左键放置方块
            if (_hoverVoxel.x != -999)
            {
                Vector3Int placePos = _hoverVoxel + _hoverNormal;
                _world.SetVoxelWorld(placePos, new Voxel(_selectedType));

                // 记录放置操作
                var def = VoxelRegistry.GetDefinition((ushort)_selectedType);
                string voxelIdStr = ((ushort)_selectedType).ToString();
                string voxelName = def != null ? (string.IsNullOrEmpty(def.displayName) ? def.name : def.displayName) : voxelIdStr;
                
                RecordVoxelOperation(voxelIdStr, voxelName, placePos);
            }
        }
    }

    /// <summary>
    /// 记录体素操作（放置或删除）
    /// </summary>
    private void RecordVoxelOperation(string voxelId, string voxelName, Vector3Int worldPos)
    {
        // 转换为本地坐标
        Vector3Int localPos = _world.WorldToLocalVoxelCoord(worldPos);
        var instance = new VoxelInstanceData(
            voxelId,
            voxelName,
            new Vector3Data(localPos.x, localPos.y, localPos.z)
        );
        
        _pendingInstances.Add(instance);
        inspectorPendingInstances = new List<VoxelInstanceData>(_pendingInstances);
        _lastBuildTime = Time.time;
        
        //Debug.Log($"[RuntimeVoxelBuilding] Recorded voxel operation: {voxelName} at ({localPos.x},{localPos.y},{localPos.z}), pending count: {_pendingInstances.Count}");
    }
    
    /// <summary>
    /// 公共方法：放置方块（供命令执行器使用）不是玩家操作不做记录
    /// </summary>
    public void PlaceBlockAt(Vector3Int worldPos, ushort voxelTypeId)
    {
        if (!_isInitialized || _world == null)
        {
            Debug.LogError("[RuntimeVoxelBuilding] Not initialized or WorldGrid not found");
            return;
        }
        
        // 放置方块
        _world.SetVoxelWorld(worldPos, new Voxel(voxelTypeId));
        
        // 记录操作
        var def = VoxelRegistry.GetDefinition(voxelTypeId);
        string voxelIdStr = voxelTypeId.ToString();
        string voxelName = def != null ? (string.IsNullOrEmpty(def.displayName) ? def.name : def.displayName) : voxelIdStr;
        //RecordVoxelOperation(voxelIdStr, voxelName, worldPos);
    }
    
    /// <summary>
    /// 公共方法：删除方块（供命令执行器使用）不是玩家操作不做记录
    /// </summary>
    public void DestroyBlockAt(Vector3Int worldPos)
    {
        if (!_isInitialized || _world == null)
        {
            Debug.LogError("[RuntimeVoxelBuilding] Not initialized or WorldGrid not found");
            return;
        }
        
        // 删除方块
        _world.SetVoxelWorld(worldPos, Voxel.Air);
        
        // 记录删除操作（air 体素）
        //RecordVoxelOperation("0", "air", worldPos);
    }

    /// <summary>
    /// 检查并发送批量事件（如果超过时间间隔）
    /// </summary>
    private void CheckAndSendBatch()
    {
        // 如果有待发送的事件，且已经超过时间间隔
        if (_pendingInstances.Count > 0 && Time.time - _lastBuildTime >= batchCollectInterval)
        {
            SendBatchEvent();
        }
    }

    /// <summary>
    /// 发送批量建造事件
    /// </summary>
    private void SendBatchEvent()
    {
        if (_pendingInstances.Count == 0) return;

        // 复制一份列表，避免清空原列表后 payload 被连带清空
        var snapshot = new List<VoxelInstanceData>(_pendingInstances);
        var batchPayload = new PlayerBuildPayload(snapshot);
        EventBus.Publish(batchPayload);
        lastSentInstancesSnapshot = snapshot;
        
        Debug.Log($"[RuntimeVoxelBuilding] Sent batch event with {_pendingInstances.Count} voxel instances");
        
        // 清空待发送列表
        _pendingInstances.Clear();
        inspectorPendingInstances.Clear();
    }

    private void OnFlushPlayerBuildRequest(FlushPlayerBuildRequest _)
    {
        // 收到请求后，立即将当前待发送的实例打包并发布
        SendBatchEvent();
    }

    private void UpdatePreview()
    {
        if (_previewManager == null) return;

        _previewManager.UpdatePreview(
            _hoverVoxel,
            _hoverNormal,
            hoverColor,
            true, // 显示放置预览
            placeColor  // 使用placeColor作为放置预览的颜色
        );
    }

    private void UpdateHoverVoxel()
    {
        Ray ray = UIStateManager.currentGameplayMode ?
            _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)) :
            _cam.ScreenPointToRay(Input.mousePosition);

        _hoverVoxel = new Vector3Int(-999, -999, -999);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, voxelLayer))
        {
            // 获取WorldGrid组件
            WorldGrid worldGrid = hit.collider.GetComponentInParent<WorldGrid>();
            if (worldGrid != null)
            {
                if (worldGrid.RaycastWorld(ray, maxDistance, out Vector3Int hitBlock, out Vector3Int hitNormal, out _))
                {
                    _hoverVoxel = hitBlock;
                    _hoverNormal = hitNormal;
                }
            }
        }
    }
}