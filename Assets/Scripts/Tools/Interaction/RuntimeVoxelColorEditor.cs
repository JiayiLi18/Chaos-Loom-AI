using UnityEngine;

namespace Voxels
{
    /// <summary>
    /// 负责体素颜色的编辑功能
    /// </summary>
    public class RuntimeVoxelColorEditor : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private ColorPickerUI colorPicker;
        [SerializeField] public static Color32 currentVoxelEditingColor = Color.white;
        [SerializeField] private float maxDistance = 20f;
        [SerializeField] private LayerMask voxelLayer;

        private Camera _cam;
        private bool _isEnabled = true;

        private void OnEnable()
        {
            InitializeComponents();
            if (colorPicker != null)
            {
                colorPicker.enabled = true;
            }
            InitializeColorPicker();
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
        }

        private void InitializeColorPicker()
        {
            if (colorPicker != null)
            {
                colorPicker.onColorChanged.AddListener(OnColorChanged);
                colorPicker.SetColor(currentVoxelEditingColor);
            }
        }

        private void OnDestroy()
        {
            if (colorPicker != null)
            {
                colorPicker.onColorChanged.RemoveListener(OnColorChanged);
            }
        }

        private void OnColorChanged(Color32 newColor)
        {
            currentVoxelEditingColor = newColor;
        }

        /// <summary>
        /// 处理颜色编辑，由外部调用
        /// TODO: 射线等功能从外部调用，这里只负责颜色编辑
        /// </summary>
        public void HandleColorEditing()
        {
            if (!_isEnabled) return;

            // 获取悬停的体素位置
            if (TryGetHoverVoxel(out Vector3Int hoverVoxel))
            {
                if (Input.GetMouseButtonDown(0))
                {
                    // 修改颜色
                    VoxelColorOverride.Instance.SetVoxelColor(hoverVoxel, currentVoxelEditingColor);
                }
                else if (Input.GetMouseButtonDown(1))
                {
                    // 清除颜色覆盖
                    VoxelColorOverride.Instance.ClearVoxelColor(hoverVoxel);
                }
            }
        }

        /// <summary>
        /// 在已知位置处理颜色编辑
        /// </summary>
        /// <param name="position">要编辑颜色的体素位置</param>
        public void HandleColorEditingAtPosition(Vector3Int position)
        {
            if (!_isEnabled) return;

            if (Input.GetMouseButtonDown(0))
            {
                // 修改颜色
                VoxelColorOverride.Instance.SetVoxelColor(position, currentVoxelEditingColor);
            }
            else if (Input.GetMouseButtonDown(1))
            {
                // 清除颜色覆盖
                VoxelColorOverride.Instance.ClearVoxelColor(position);
            }
        }

        private bool TryGetHoverVoxel(out Vector3Int hoverVoxel)
        {
            hoverVoxel = new Vector3Int(-999, -999, -999);

            Ray ray = UIStateManager.currentGameplayMode ? 
                _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)) : 
                _cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, voxelLayer))
            {
                WorldGrid worldGrid = hit.collider.GetComponentInParent<WorldGrid>();
                if (worldGrid != null && worldGrid.RaycastWorld(ray, maxDistance, out Vector3Int hitBlock, out _, out _))
                {
                    hoverVoxel = hitBlock;
                    return true;
                }
            }

            return false;
        }

        public Color GetCurrentColor()
        {
            return currentVoxelEditingColor;
        }

        public bool TryGetHoverPosition(out Vector3Int position)
        {
            return TryGetHoverVoxel(out position);
        }

        private void OnDisable()
        {
            if (colorPicker != null)
            {
                colorPicker.enabled = false;
            }
        }
    }
} 