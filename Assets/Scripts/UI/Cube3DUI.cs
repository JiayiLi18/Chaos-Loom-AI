using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Voxels;

namespace Voxels
{
    public class Cube3DUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        // Use voxel definition order: (+X Right, -X Left, +Y Up, -Y Down, +Z Front, -Z Back)
        public enum CubeFace { Right = 0, Left = 1, Up = 2, Down = 3, Front = 4, Back = 5 }

        [Header("Scene refs")]
        public Camera uiCamera;
        public Transform rotateRoot;            // 旋转节点（通常就是本物体）
        [Tooltip("按 Right(+X), Left(-X), Up(+Y), Down(-Y), Front(+Z), Back(-Z) 顺序放 6 个 Quad 的 Renderer")]
        public Renderer[] faceRenderers = new Renderer[6];
        [Tooltip("Cube3D 预览的 Layer（例如 'VoxelPreview'）")]
        public LayerMask cubeLayerMask = -1; // 默认所有层

        [Header("Rotation")]
        public float rotateSpeed = 0.4f;
        public Vector2 pitchClamp = new Vector2(-80f, 80f);
        public bool inertia = true;
        public float inertiaDamp = 8f;


        [Header("Events")]
        public System.Action<int> OnFaceSelected; // 面选择事件，传递面索引

        [Header("Face Name Display")]
        [Tooltip("face name order: Right(+X), Left(-X), Up(+Y), Down(-Y), Front(+Z), Back(-Z)")]
        [SerializeField] private GameObject faceNamePanel; // 面名称显示面板（保留以防将来需要悬停面板）
        [SerializeField] private TMPro.TextMeshProUGUI[] allFaceNameTexts = new TMPro.TextMeshProUGUI[6]; // 所有面的名称文本（按Right,Left,Up,Down,Front,Back顺序）

        // 当前选中面
        [SerializeField] private CubeFace current = CubeFace.Front;

        // 每面当前颜色/贴图缓存（便于 UI 同步）
        private readonly Color[] _colors = new Color[6];
        private readonly Texture[] _textures = new Texture[6];

        // 名称->面 的快速映射（如果 6 个 Quad 名字固定，如 "Face_Up" 等）
        public string nameUp = "Face_Up";
        public string nameDown = "Face_Down";
        public string nameLeft = "Face_Left";
        public string nameRight = "Face_Right";
        public string nameFront = "Face_Front";
        public string nameBack = "Face_Back";

        // 旋转内部态
        private Vector3 _euler;
        private Vector2 _dragDelta;
        private Vector2 _lastDelta;
        private bool _dragging;
        private bool _pressOnCube; // 是否在Cube上按下，用于确保拖拽/点击只在Cube上触发
        private bool _hadDragSincePress; // 本次按下后是否发生过拖拽
        private bool _clickHandled; // 本次按下后的点击是否已经处理
        private CubeFace _hoveredFace = CubeFace.Front; // 当前悬停的面，用于智能旋转轴选择

        // 同步状态
        private bool _isInitialized = false;
        private bool[] _faceTextureMode = new bool[6]; // 每个面是否处于贴图模式


        void Awake()
        {
            InitializeComponents();
        }

        void Start()
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Cube3DUI] Components not initialized!");
                return;
            }
            InitializeVoxelEditingConnection();
        }

        private void InitializeComponents()
        {
            if (rotateRoot == null) rotateRoot = transform;
            _euler = rotateRoot.rotation.eulerAngles;

            // 初始化每面颜色/贴图（从材质里读）
            for (int i = 0; i < faceRenderers.Length; i++)
            {
                if (faceRenderers[i] == null) continue;
                var mat = faceRenderers[i].material; // 实例化材质，独立改色
                _colors[i] = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") :
                             (mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white);
                _textures[i] = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") :
                               (mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null);
            }

            ApplyUIFromFace();
            
            // 初始化面名称显示
            InitializeFaceNameDisplay();
            
            // 检查必要组件
            CheckRequiredComponents();
            
            // 确保有一个默认选中的面
            if (current == (CubeFace)(-1))
            {
                current = CubeFace.Front; // 默认选中Front面
               // Debug.Log("[Cube3DUI] No face selected, defaulting to Front face");
            }
            
            _isInitialized = true;
        }

        private void CheckRequiredComponents()
        {
            if (uiCamera == null)
            {
                Debug.LogError("[Cube3DUI] UI Camera is not assigned!");
                return;
            }
            
            if (cubeLayerMask == 0)
            {
                Debug.LogWarning("[Cube3DUI] Cube Layer Mask is set to Nothing! Events will not work!");
            }
            
            // 检查是否有 Collider
            int colliderCount = 0;
            foreach (var renderer in faceRenderers)
            {
                if (renderer != null && renderer.GetComponent<Collider>() != null)
                {
                    colliderCount++;
                }
            }
            
            if (colliderCount == 0)
            {
                Debug.LogError("[Cube3DUI] No Collider found! Add Collider to face objects!");
            }
            else
            {
            }
            
            // 最重要的检查：相机是否有 PhysicsRaycaster
            var raycaster = uiCamera.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
            if (raycaster == null)
            {
                Debug.LogError("═══════════════════════════════════════════════════════════");
                Debug.LogError("[Cube3DUI] CRITICAL: UI Camera MUST have PhysicsRaycaster!");
                Debug.LogError("Add Component -> Event -> Physics Raycaster to UI Camera");
                Debug.LogError("═══════════════════════════════════════════════════════════");
            }
            else
            {
                // 检查 EventMask 是否包含 cubeLayerMask
                if ((raycaster.eventMask.value & cubeLayerMask.value) == 0)
                {
                    Debug.LogError($"[Cube3DUI] PhysicsRaycaster EventMask ({raycaster.eventMask.value}) does not include Cube Layer ({cubeLayerMask.value})!");
                }
            }
            
            // 检查 EventSystem
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null)
            {
                Debug.LogError("[Cube3DUI] No EventSystem found in scene!");
            }
            else
            {
            }
            
            
        }

        private void InitializeVoxelEditingConnection()
        {
            // 不再需要连接VoxelEditingUI，改为通过事件通知
            Debug.Log("[Cube3DUI] Initialized - ready to receive face selection events");
        }


        void Update()
        {
            // 智能惯性旋转
            if (inertia && _isInitialized && !_dragging && _lastDelta.sqrMagnitude > 0.0001f)
            {
                // 左右惯性：绕Y轴
                _euler.y += _lastDelta.x * rotateSpeed * Time.deltaTime * 60f;
                
                // 上下惯性：根据最后悬停的面选择旋转轴
                float verticalInertia = _lastDelta.y * rotateSpeed * Time.deltaTime * 60f;
                
                switch (_hoveredFace)
                {
                    case CubeFace.Front:
                    case CubeFace.Back:
                    case CubeFace.Up:
                    case CubeFace.Down:
                        // Front/Back/Up/Down面：绕X轴旋转
                        _euler.x -= verticalInertia;
                        _euler.x = Mathf.Clamp(_euler.x, pitchClamp.x, pitchClamp.y);
                        break;
                        
                    case CubeFace.Left:
                    case CubeFace.Right:
                        // Left/Right面：绕Z轴旋转
                        _euler.z -= verticalInertia;
                        _euler.z = Mathf.Clamp(_euler.z, pitchClamp.x, pitchClamp.y);
                        break;
                }
                
                rotateRoot.rotation = Quaternion.Euler(_euler);
                _lastDelta = Vector2.Lerp(_lastDelta, Vector2.zero, Time.deltaTime * inertiaDamp);
            }

            // 更新悬停面检测
            UpdateHoveredFace();
            
            // 更新面名称显示（根据可见性）
            UpdateFaceNameDisplay();
        }

        // ========== 旋转 ==========
        public void OnPointerDown(PointerEventData e)
        {
            // 记录是否在Cube上按下（避免被其他UI遮挡时误触发拖拽）
            _pressOnCube = false;
            _hadDragSincePress = false;
            _clickHandled = false;
            _dragDelta = Vector2.zero;
            _lastDelta = Vector2.zero;
            if (uiCamera == null) return;
            Ray ray = uiCamera.ScreenPointToRay(e.position);
            if (Physics.Raycast(ray, out var hit, 1000f, cubeLayerMask))
            {
                _pressOnCube = true;
            }
        }

        public void OnPointerUp(PointerEventData e)
        {
            // PointerUp 时，如果没有发生拖拽，则将其视为一次点击
            if (!_hadDragSincePress)
            {
                ProcessClick(e.position, source:"PointerUp");
            }
            _pressOnCube = false;
            _dragDelta = Vector2.zero;
            _lastDelta = Vector2.zero;
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (uiCamera == null) return;
            if (!_pressOnCube) return; // 只有在Cube上按下时才开始拖拽
            
            // 检查是否点击在 Cube 上
            Ray ray = uiCamera.ScreenPointToRay(e.position);
            if (Physics.Raycast(ray, out var hit, 1000f, cubeLayerMask))
            {
                _dragging = true;
                _dragDelta = Vector2.zero;
                _lastDelta = Vector2.zero;
                _hadDragSincePress = true;
            }
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging) return;
            
            _dragDelta = e.delta;
            _lastDelta = e.delta;

            // 智能旋转轴选择
            // 左右拖拽：总是绕Y轴旋转
            _euler.y += e.delta.x * rotateSpeed;
            
            // 上下拖拽：根据悬停面选择旋转轴
            float verticalRotation = e.delta.y * rotateSpeed;
            
            switch (_hoveredFace)
            {
                case CubeFace.Front:
                case CubeFace.Back:
                    // Front/Back面：绕X轴旋转
                    _euler.x -= verticalRotation;
                    _euler.x = Mathf.Clamp(_euler.x, pitchClamp.x, pitchClamp.y);
                    break;
                    
                case CubeFace.Left:
                case CubeFace.Right:
                    // Left/Right面：绕Z轴旋转
                    _euler.z -= verticalRotation;
                    _euler.z = Mathf.Clamp(_euler.z, pitchClamp.x, pitchClamp.y);
                    break;
                    
                case CubeFace.Up:
                case CubeFace.Down:
                    // Up/Down面：绕X轴旋转（保持原有行为）
                    _euler.x -= verticalRotation;
                    _euler.x = Mathf.Clamp(_euler.x, pitchClamp.x, pitchClamp.y);
                    break;
            }
            
            rotateRoot.rotation = Quaternion.Euler(_euler);
        }

        public void OnEndDrag(PointerEventData e)
        {
            _dragging = false;
            _dragDelta = Vector2.zero;
            _lastDelta = Vector2.zero;
            
        }

        // ========== 选面 ==========
        public void OnPointerClick(PointerEventData e)
        {
            if (_hadDragSincePress)
            {
                return;
            }
            ProcessClick(e.position, source:"PointerClick");
        }

        private void ProcessClick(Vector2 screenPosition, string source)
        {
            if (_clickHandled) return;
            _clickHandled = true;
            if (uiCamera == null) return;
            
            Ray ray = uiCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit, 1000f, cubeLayerMask))
            {
                return;
            }
            var go = hit.collider.gameObject;
            
            
            CubeFace hitFace = GetFaceFromName(go.name);
            if (hitFace == (CubeFace)(-1))
            {
                return;
            }
            
            // 如果点击的是当前选中的面，不做任何操作（保持选中状态）
            if (hitFace == current)
            {
                return;
            }
            
            current = hitFace;
            int faceIndex = (int)hitFace;
            ApplyUIFromFace();
            UpdateFaceNameDisplay(); // 更新面名称显示而不是高亮面
            
            // 触发面选择事件
            OnFaceSelected?.Invoke(faceIndex);
        }

        /// <summary>
        /// 从物体名称获取面
        /// 使用顺序: Right(0), Left(1), Up(2), Down(3), Front(4), Back(5)
        /// </summary>
        private CubeFace GetFaceFromName(string faceName)
        {
            if (faceName == nameRight) return CubeFace.Right;
            if (faceName == nameLeft) return CubeFace.Left;
            if (faceName == nameUp) return CubeFace.Up;
            if (faceName == nameDown) return CubeFace.Down;
            if (faceName == nameFront) return CubeFace.Front;
            if (faceName == nameBack) return CubeFace.Back;
            
            Debug.LogWarning($"[Cube3DUI] Unknown face name: {faceName}");
            return (CubeFace)(-1);
        }

        // ========== 改色 ==========

        public void SetFaceColor(CubeFace face, Color c)
        {
            int i = (int)face;
            _colors[i] = c;

            // 如果当前面是贴图模式，只清除当前面的贴图
            if (_faceTextureMode[i])
            {
                // 只清除当前面的贴图，保留其他面的贴图
                SetFaceTexture(face, null);
                _faceTextureMode[i] = false; // 标记为颜色模式
                Debug.Log($"[Cube3DUI] Cleared texture for face {face}, switched to color mode");
            }

            var mr = faceRenderers[i];
            if (mr == null) return;

            var mat = mr.material; // 实例化材质
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        }

        // ========== 换贴图（可从按钮传入） ==========
        public void SetFaceTexture(CubeFace face, Texture2D tex)
        {
            int i = (int)face;
            _textures[i] = tex;

            // 标记当前面为贴图模式
            _faceTextureMode[i] = (tex != null);

            var mr = faceRenderers[i];
            if (mr == null) return;

            var mat = mr.material;
            
            // 设置纹理
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            
            // 重要：设置纹理时，同时将BaseColor设置为白色，避免颜色残留
            if (tex != null)
            {
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                else if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            }
        }

        // 让外部 UI 调用（例如按钮 OnClick 绑定）
        public void SetCurrentFaceTexture(Texture2D tex) => SetFaceTexture(current, tex);
        public void SetCurrentFaceColor(Color c) => SetFaceColor(current, c);

        // ========== 选面后同步状态 ==========
        private void ApplyUIFromFace()
        {
            // 选中面后可以在这里添加其他同步逻辑
            // 目前主要依赖VoxelEditingUI的颜色选择器
        }


        /// <summary>
        /// 重置为默认颜色
        /// </summary>
        public void ResetToDefaultColors()
        {
            // 清除所有贴图和重置模式状态
            for (int i = 0; i < 6; i++)
            {
                _faceTextureMode[i] = false;
                SetFaceTexture((CubeFace)i, null); // 清除贴图
                SetFaceColor((CubeFace)i, Color.white); // 重置为白色
            }
            ApplyUIFromFace();
            Debug.Log("[Cube3DUI] Reset to default colors - all faces cleared and set to white");
        }

        // ========== 面名称显示功能 ==========

        /// <summary>
        /// 初始化面名称显示
        /// </summary>
        private void InitializeFaceNameDisplay()
        {
            // 初始化所有面名称文本
            for (int i = 0; i < allFaceNameTexts.Length; i++)
            {
                if (allFaceNameTexts[i] != null)
                {
                    // 设置默认低透明度
                    allFaceNameTexts[i].color = new Color(1f, 1f, 1f, 0.3f);
                    allFaceNameTexts[i].text = GetFaceDisplayName((CubeFace)i);
                    
                    // 确保不阻挡射线
                    allFaceNameTexts[i].raycastTarget = false;
                }
            }

            // 初始化悬停面板（如果存在）
            if (faceNamePanel != null)
            {
                faceNamePanel.SetActive(false);

                // 确保提示面板不阻挡射线
                var cg = faceNamePanel.GetComponent<CanvasGroup>();
                if (cg == null)
                {
                    cg = faceNamePanel.AddComponent<CanvasGroup>();
                }
                cg.blocksRaycasts = false;
                
                // 如果 Text 或 Image 有 RaycastTarget，关闭它们
                var graphics = faceNamePanel.GetComponentsInChildren<Graphic>(true);
                foreach (var g in graphics)
                {
                    g.raycastTarget = false;
                }
            }
            
            // 初始显示当前选中面
            UpdateFaceNameDisplay();
        }

        /// <summary>
        /// 更新面名称显示（只显示可见面，选中面高亮，其他面低透明度）
        /// </summary>
        private void UpdateFaceNameDisplay()
        {
            if (!_isInitialized) return;
            
            // 获取当前可见的面
            var visibleFaces = GetVisibleFaces();
            
            for (int i = 0; i < allFaceNameTexts.Length; i++)
            {
                if (allFaceNameTexts[i] != null)
                {
                    bool isVisible = visibleFaces.Contains((CubeFace)i);
                    
                    if (!isVisible)
                    {
                        // 不可见的面：完全隐藏
                        allFaceNameTexts[i].gameObject.SetActive(false);
                    }
                    else
                    {
                        // 可见的面：显示
                        allFaceNameTexts[i].gameObject.SetActive(true);
                        
                        // 更新文字位置到对应面的中心
                        UpdateTextPositionToFace(allFaceNameTexts[i], (CubeFace)i);
                        
                        if (i == (int)current)
                        {
                            // 选中面：高亮显示，加强阴影
                            allFaceNameTexts[i].color = Color.white;
                            allFaceNameTexts[i].fontStyle = TMPro.FontStyles.Bold;
                            // 使用Underlay效果实现阴影
                            allFaceNameTexts[i].fontMaterial.SetFloat("_UnderlaySoftness", 0.5f);
                            allFaceNameTexts[i].fontMaterial.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.8f));
                            allFaceNameTexts[i].fontMaterial.SetFloat("_UnderlayOffsetX", 2f);
                            allFaceNameTexts[i].fontMaterial.SetFloat("_UnderlayOffsetY", -2f);
                            allFaceNameTexts[i].fontMaterial.EnableKeyword("UNDERLAY_ON");
                        }
                        else
                        {
                            // 非选中面：低透明度
                            allFaceNameTexts[i].color = new Color(1f, 1f, 1f, 0.3f);
                            allFaceNameTexts[i].fontStyle = TMPro.FontStyles.Normal;
                            // 减弱阴影效果
                            allFaceNameTexts[i].fontMaterial.SetFloat("_UnderlaySoftness", 0.3f);
                            allFaceNameTexts[i].fontMaterial.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.3f));
                            allFaceNameTexts[i].fontMaterial.SetFloat("_UnderlayOffsetX", 1f);
                            allFaceNameTexts[i].fontMaterial.SetFloat("_UnderlayOffsetY", -1f);
                            allFaceNameTexts[i].fontMaterial.EnableKeyword("UNDERLAY_ON");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 将文字位置更新到对应面的中心位置
        /// </summary>
        private void UpdateTextPositionToFace(TMPro.TextMeshProUGUI textComponent, CubeFace face)
        {
            if (textComponent == null || uiCamera == null) return;
            
            // 获取对应面的Renderer
            int faceIndex = (int)face;
            if (faceIndex >= faceRenderers.Length || faceRenderers[faceIndex] == null) return;
            
            var renderer = faceRenderers[faceIndex];
            
            // 获取面的中心点（世界坐标）
            Vector3 faceCenter = renderer.bounds.center;
            
            // 将世界坐标转换为屏幕坐标
            Vector3 screenPos = uiCamera.WorldToScreenPoint(faceCenter);
            
            // 检查是否在相机前方
            if (screenPos.z < 0) return; // 在相机后方，不显示
            
            // 获取Canvas
            Canvas canvas = textComponent.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            RectTransform textRect = textComponent.GetComponent<RectTransform>();
            
            if (canvasRect == null || textRect == null) return;
            
            // 将屏幕坐标转换为Canvas本地坐标
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, canvas.worldCamera, out localPoint);
            
            // 设置文字位置
            textRect.anchoredPosition = localPoint;
        }

        /// <summary>
        /// 获取当前可见的面列表
        /// </summary>
        private HashSet<CubeFace> GetVisibleFaces()
        {
            var visibleFaces = new HashSet<CubeFace>();
            
            if (uiCamera == null) return visibleFaces;
            
            // 获取相机的方向向量
            Vector3 cameraForward = uiCamera.transform.forward;
            Vector3 cameraRight = uiCamera.transform.right;
            Vector3 cameraUp = uiCamera.transform.up;
            
            // 定义每个面的法向量（从cube中心向外），按VoxelDefinition顺序
            Vector3[] faceNormals = new Vector3[6]
            {
                Vector3.right,   // Right (+X)
                Vector3.left,    // Left  (-X)
                Vector3.up,      // Up    (+Y)
                Vector3.down,    // Down  (-Y)
                Vector3.forward, // Front (+Z)
                Vector3.back     // Back  (-Z)
            };
            
            // 检查每个面是否面向相机
            for (int i = 0; i < 6; i++)
            {
                Vector3 faceNormal = faceNormals[i];
                
                // 将面法向量转换到世界空间
                Vector3 worldNormal = rotateRoot.TransformDirection(faceNormal);
                
                // 计算面法向量与相机方向的点积
                float dot = Vector3.Dot(worldNormal, -cameraForward);
                
                // 如果点积大于0，说明面朝向相机（可见）
                if (dot > 0.1f) // 使用小的阈值避免边缘情况
                {
                    visibleFaces.Add((CubeFace)i);
                }
            }
            
            return visibleFaces;
        }

        /// <summary>
        /// 更新悬停面检测（用于智能旋转轴选择）
        /// </summary>
        private void UpdateHoveredFace()
        {
            if (!_isInitialized || uiCamera == null) return;
            
            Vector3 mousePos = Input.mousePosition;
            Ray ray = uiCamera.ScreenPointToRay(mousePos);
            
            if (Physics.Raycast(ray, out var hit, 1000f, cubeLayerMask))
            {
                var go = hit.collider.gameObject;
                CubeFace hitFace = GetFaceFromName(go.name);
                
                if (hitFace != (CubeFace)(-1))
                {
                    _hoveredFace = hitFace;
                }
            }
        }

        /// <summary>
        /// 获取面显示名称
        /// </summary>
        private string GetFaceDisplayName(CubeFace face)
        {
            switch (face)
            {
                case CubeFace.Right: return "Right";
                case CubeFace.Left: return "Left";
                case CubeFace.Up: return "Up";
                case CubeFace.Down: return "Down";
                case CubeFace.Front: return "Front";
                case CubeFace.Back: return "Back";
                default: return "Unknown";
            }
        }
    }
}