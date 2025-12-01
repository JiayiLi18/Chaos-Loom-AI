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
    /// 6. 支持完全清空并重新加载体素定义（用于JSON数据同步）
    /// 
    /// 与其他组件的关系：
    /// - 依赖 TextureLibrary：订阅 TextureLibrary.OnLibraryInitialized 事件以确保在纹理库初始化后再加载体素定义
    /// - 被 Voxel 结构体依赖：Voxel 通过 typeId 从此注册表查找完整的 VoxelDefinition
    /// - 被 VoxelJsonDB 依赖：VoxelJsonDB 在检测到文件变化时调用 Clear 并重新注册所有体素
    /// 
    /// 执行顺序：设置为 DefaultExecutionOrder(1000)，确保在 TextureLibrary (-1000) 之后初始化
    /// </summary>
    [DefaultExecutionOrder(1000)] // Ensure this runs after TextureLibrary (which has -1000)
    public static class VoxelRegistry
    {
        private static readonly List<VoxelDefinition> s_Definitions = new List<VoxelDefinition>();
        private static readonly Dictionary<string, ushort> s_IdByName = new Dictionary<string, ushort>(StringComparer.Ordinal);
        private static List<VoxelDefinition> s_PendingRegistrations = new List<VoxelDefinition>();

        /// <summary>Number of registered voxel types ("Air" is normally 0).</summary>
        public static int Count => s_Definitions.Count;

        public static event Action OnRegistryChanged;

        /// <summary>Returns the <see cref="VoxelDefinition"/> for the given ID, or null if unknown.</summary>
        public static VoxelDefinition GetDefinition(ushort id) =>
            id < s_Definitions.Count ? s_Definitions[id] : null;

        /// <summary>获取所有已注册的VoxelDefinition</summary>
        public static IReadOnlyList<VoxelDefinition> GetAllDefinitions() => s_Definitions;

        /// <summary>Register a new voxel definition with a specific ID.</summary>
        public static ushort RegisterWithId(VoxelDefinition def, ushort requestedId)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            
            // 确保名称不为空
            if (string.IsNullOrEmpty(def.name))
            {
                Debug.LogError("Cannot register voxel with empty name!");
                return 0; // 返回0表示注册失败
            }
            
            if (s_IdByName.TryGetValue(def.name, out var existing))
            {
                Debug.LogWarning($"Voxel '{def.name}' already registered with ID {existing}");
                return existing; // already registered
            }

            // If TextureLibrary isn't ready yet, queue for later registration
            if (TextureLibrary.I == null)
            {
                if (!s_PendingRegistrations.Contains(def))
                {
                    s_PendingRegistrations.Add(def);
                }
                return 0; // Temporary ID, will be updated when actually registered
            }

            // Ensure the definitions list has enough capacity
            while (s_Definitions.Count <= requestedId)
            {
                s_Definitions.Add(null);
            }

            // 检查是否已有相同ID的体素
            if (s_Definitions[requestedId] != null)
            {
                Debug.LogError($"ID conflict: ID {requestedId} is already used by '{s_Definitions[requestedId].name}'");
                return 0; // 返回0表示注册失败
            }

            def.typeId = requestedId;

            // Register textures (face textures)
            def.UpdateTextureIfNeeded();
            //Debug.Log($"Registered voxel '{def.name}' with face textures");

            s_Definitions[requestedId] = def;
            s_IdByName[def.name] = requestedId;
            OnRegistryChanged?.Invoke();
            return requestedId;
        }

        /// <summary>Register a new voxel definition and return its runtime ID.</summary>
        public static ushort Register(VoxelDefinition def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            
            // 确保名称不为空
            if (string.IsNullOrEmpty(def.name))
            {
                Debug.LogError("Cannot register voxel with empty name!");
                return 0; // 返回0表示注册失败
            }
            
            if (s_IdByName.TryGetValue(def.name, out var existing))
            {
                Debug.LogWarning($"Voxel '{def.name}' already registered with ID {existing}");
                return existing; // already registered
            }

            // 找到最大的已使用ID，然后+1（确保ID连续递增，不使用空闲ID）
            ushort maxId = 0; // 从0开始，因为0是Air
            for (int i = 0; i < s_Definitions.Count; i++)
            {
                if (s_Definitions[i] != null && i > maxId)
                {
                    maxId = (ushort)i;
                }
            }
            ushort nextId = (ushort)(maxId + 1);

            return RegisterWithId(def, nextId);
        }

        /// <summary>
        /// 清空所有注册的体素定义，用于外部JSON数据变更时重新加载
        /// 注意：这会导致所有现有的Voxel实例的typeId变得无效，应该仅在初始化阶段或特定的重载点使用
        /// </summary>
        public static void Clear()
        {
            
            // 销毁所有ScriptableObject以防止内存泄漏
            foreach (var def in s_Definitions)
            {
                if (def != null)
                {
                    UnityEngine.Object.Destroy(def);
                }
            }
            
            s_Definitions.Clear();
            s_IdByName.Clear();
            s_PendingRegistrations.Clear();
            
            Debug.Log("VoxelRegistry: Registry cleared, ready for reinitialization");
            OnRegistryChanged?.Invoke();
        }

        /// <summary>
        /// 注销一个体素定义
        /// </summary>
        public static bool Unregister(ushort id)
        {
            if (id >= s_Definitions.Count)
            {
                Debug.LogError($"[VoxelRegistry] ID {id} is out of range");
                return false;
            }

            var def = s_Definitions[id];
            if (def == null)
            {
                Debug.LogError($"[VoxelRegistry] Definition with ID {id} is already null");
                return false;
            }

            try
            {
                // 从字典中移除
                if (!string.IsNullOrEmpty(def.name))
                {
                    s_IdByName.Remove(def.name);
                }

                // 销毁ScriptableObject
                UnityEngine.Object.Destroy(def);
                
                // 从列表中移除
                s_Definitions[id] = null;

                Debug.Log($"[VoxelRegistry] Successfully unregistered voxel with ID {id}");
                OnRegistryChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoxelRegistry] Error while unregistering voxel {id}: {ex.Message}");
                return false;
            }
        }
    }
}

