using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voxels
{
    /// <summary>
    /// 体素定义的全局注册表，负责管理所有 VoxelDefinition 并分配唯一标识符。
    /// 
    /// 核心职责：
    /// 1. 为每个 VoxelDefinition 分配唯一的 typeId
    /// 2. 维护所有已注册 VoxelDefinition 的全局列表和查找字典
    /// 3. 提供通过 typeId 查找对应 VoxelDefinition 的方法 (GetDefinition)
    /// 4. 支持运行时动态注册新的体素类型
    /// 5. 确保所有体素定义的资源（如纹理）被正确加载
    /// 
    /// 与其他组件的关系：
    /// - 依赖 TextureLibrary：订阅 TextureLibrary.OnLibraryInitialized 事件以确保在纹理库初始化后再加载体素定义
    /// - 被 Voxel 结构体依赖：Voxel 通过 typeId 从此注册表查找完整的 VoxelDefinition
    /// 
    /// 执行顺序：设置为 DefaultExecutionOrder(1000)，确保在 TextureLibrary (-1000) 之后初始化
    /// </summary>
    [DefaultExecutionOrder(1000)] // Ensure this runs after TextureLibrary (which has -1000)
    public static class VoxelRegistry
    {
        private static readonly List<VoxelDefinition> s_Definitions = new List<VoxelDefinition>();
        private static readonly Dictionary<string, ushort> s_IdByName = new Dictionary<string, ushort>(StringComparer.Ordinal);
        private static bool s_Initialized = false;
        private static List<VoxelDefinition> s_PendingRegistrations = new List<VoxelDefinition>();

        /// <summary>Number of registered voxel types ("Air" is normally 0).</summary>
        public static int Count => s_Definitions.Count;

        public static event Action OnRegistryChanged;

        /// <summary>Returns the <see cref="VoxelDefinition"/> for the given ID, or null if unknown.</summary>
        public static VoxelDefinition GetDefinition(ushort id) =>
            id < s_Definitions.Count ? s_Definitions[id] : null;

        /// <summary>获取所有已注册的VoxelDefinition</summary>
        public static IReadOnlyList<VoxelDefinition> GetAllDefinitions() => s_Definitions;

        /// <summary>Register a new voxel definition and return its runtime ID.</summary>
        public static ushort Register(VoxelDefinition def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (s_IdByName.TryGetValue(def.name, out var existing))
                return existing; // already registered

            // If TextureLibrary isn't ready yet, queue for later registration
            if (TextureLibrary.I == null)
            {
                if (!s_PendingRegistrations.Contains(def))
                {
                    s_PendingRegistrations.Add(def);
                    Debug.Log($"Queued voxel '{def.name}' for registration once TextureLibrary is ready");
                }
                return 0; // Temporary ID, will be updated when actually registered
            }

            ushort id = (ushort)s_Definitions.Count; // sequential; 0 reserved for Air
            def.typeId = id;

            // ✅ 注册贴图
            if (def.texture != null)
            {
                def.UpdateTextureIfNeeded();
                Debug.Log($"Registered voxel '{def.name}' with texture '{def.texture.name}' at index {def.sliceIndex}");
            }
            else
            {
                Debug.LogWarning($"Voxel '{def.name}' has no texture assigned!");
            }

            s_Definitions.Add(def);
            s_IdByName[def.name] = id;
            OnRegistryChanged?.Invoke();
            return id;
        }

        /// <summary>Call once at boot to load all ScriptableObject definitions in Resources/Voxels.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeRegistry()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                TryInitVoxelRegistry();
            };
#else
    TryInitVoxelRegistry();
#endif
        }

        private static void TryInitVoxelRegistry()
        {
            // Subscribe to TextureLibrary initialization event
            TextureLibrary.OnLibraryInitialized += OnTextureLibraryInitialized;

            // In case TextureLibrary is already initialized
            if (TextureLibrary.IsInitialized)
            {
                OnTextureLibraryInitialized();
            }
            else
            {
                Debug.Log("VoxelRegistry: Waiting for TextureLibrary to be initialized...");
            }
        }

        // This will be called when TextureLibrary is initialized
        private static void OnTextureLibraryInitialized()
        {
            // Unsubscribe to avoid multiple calls
            TextureLibrary.OnLibraryInitialized -= OnTextureLibraryInitialized;

            Debug.Log("VoxelRegistry: TextureLibrary is now initialized, proceeding with voxel loading");
            LoadVoxelDefinitions();
        }

        private static void LoadVoxelDefinitions()
        {
            if (s_Initialized) return;
            s_Initialized = true;

            // Load voxel definitions from Resources
            var defs = Resources.LoadAll<VoxelDefinition>("Voxels");
            foreach (var d in defs) Register(d);

            // Process any pending registrations
            foreach (var d in s_PendingRegistrations)
            {
                if (!s_IdByName.ContainsKey(d.name)) // Check again in case it was registered in the previous step
                    Register(d);
            }
            s_PendingRegistrations.Clear();

            // Ensure index 0 exists and represents Air.
            if (s_Definitions.Count == 0 || s_Definitions[0].isTransparent == false)
            {
                var air = ScriptableObject.CreateInstance<VoxelDefinition>();
                air.name = "Air";
                air.displayName = "Air";
                air.description = "Air";
                air.isTransparent = true;
                air.baseColor = new Color32(0, 0, 0, 0);
                Register(air); // becomes ID 0
            }
        }
    }
}

