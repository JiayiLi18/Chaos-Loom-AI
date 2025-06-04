using UnityEngine;
using Voxels;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
/// <summary>
/// 负责场景中体素的实际建造和删除功能
/// </summary>
public class RuntimeVoxelBuilding : MonoBehaviour
{
    [Header("Building Settings")]
    [SerializeField] private float maxDistance = 5f;
    [SerializeField] private LayerMask voxelLayer;

    [Header("Preview Settings")]
    [SerializeField] private Color hoverColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color placeColor = new Color(0f, 1f, 0f, 0.3f);

    [Header("Color Editing")]
    [SerializeField] private InputActionReference colorModifyAction;
    private RuntimeVoxelColorEditor _colorEditor;

    [Header("UI")]
    private VoxelPreviewManager _previewManager;
    private PaintingSystem _paintingSystem;

    private Camera _cam;
    private WorldGrid _world;
    private Vector3Int _hoverVoxel = new(-999, -999, -999);
    private Vector3Int _hoverNormal = Vector3Int.zero;
    private bool _isEnabled = true;
    private byte _selectedType = 1;

    private bool _isInitialized = false;

    // 添加一个静态实例，方便其他组件访问
    public static RuntimeVoxelBuilding Instance { get; private set; }

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
        
        // 获取颜色编辑器组件
        _colorEditor = FindAnyObjectByType<RuntimeVoxelColorEditor>();
        if (_colorEditor == null)
        {
            Debug.LogWarning("找不到RuntimeVoxelColorEditor组件，颜色编辑功能将不可用");
        }

        // 获取预览管理器
        _previewManager = FindAnyObjectByType<VoxelPreviewManager>();
        if (_previewManager == null)
        {
            Debug.LogWarning("找不到VoxelPreviewManager组件，预览功能将不可用");
        }

        // 获取绘画系统
        _paintingSystem = FindAnyObjectByType<PaintingSystem>();

        _isInitialized = true;
    }

    private void OnEnable()
    {
        if (!_isInitialized) 
        {
            InitializeComponents();
        }

        // 启用所有组件
        if (colorModifyAction != null && colorModifyAction.action != null)
        {
            colorModifyAction.action.Enable();
        }
        if (_colorEditor != null)
        {
            _colorEditor.enabled = true;
        }
        if (_previewManager != null)
        {
            _previewManager.enabled = true;
        }
    }

    private void OnDisable()
    {
        // 禁用所有组件
        if (colorModifyAction != null && colorModifyAction.action != null)
        {
            colorModifyAction.action.Disable();
        }
        if (_colorEditor != null)
        {
            _colorEditor.enabled = false;
        }
        if (_previewManager != null)
        {
            _previewManager.HidePreview();
            _previewManager.enabled = false;
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

        //如果正在绘画，则不更新
        if (_paintingSystem != null && _paintingSystem.isEnabled)
        {
            if (_previewManager != null)
            {
                _previewManager.HidePreview();
            }
            return;
        }

        UpdateHoverVoxel();
        UpdatePreview();
        HandleInput();
    }

    private void HandleInput()
    {
        bool isColorEditMode = colorModifyAction != null && colorModifyAction.action.IsPressed();

        if (isColorEditMode && _colorEditor != null)
        {
            // 在颜色编辑模式下，使用已知的hover位置进行颜色编辑
            if (_hoverVoxel.x != -999)
            {
                _colorEditor.HandleColorEditingAtPosition(_hoverVoxel);
            }
            return;
        }

        // 正常的方块放置/删除逻辑
        if (Input.GetMouseButtonDown(1))
        {
            // 右键删除方块
            if (_hoverVoxel.x != -999)
            {
                _world.SetVoxelWorld(_hoverVoxel, Voxel.Air);
                // 同时清除颜色覆盖
                VoxelColorOverride.Instance.ClearVoxelColor(_hoverVoxel);
            }
        }
        else if (Input.GetMouseButtonDown(0))
        {
            // 左键放置方块
            if (_hoverVoxel.x != -999)
            {
                Vector3Int placePos = _hoverVoxel + _hoverNormal;
                _world.SetVoxelWorld(placePos, new Voxel(_selectedType));
            }
        }
    }

    private void UpdatePreview()
    {
        if (_previewManager == null) return;

        bool isColorEditMode = colorModifyAction != null && colorModifyAction.action.IsPressed();
        Color previewColor = isColorEditMode && _colorEditor != null ? 
            _colorEditor.GetCurrentColor() : hoverColor;

        _previewManager.UpdatePreview(
            _hoverVoxel,
            _hoverNormal,
            previewColor,
            !isColorEditMode, // 在颜色编辑模式下不显示放置预览
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