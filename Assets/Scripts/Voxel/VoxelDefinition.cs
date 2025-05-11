using UnityEngine;

namespace Voxels
{
    /// <summary>
    /// 体素定义类，用于描述体素的外观和属性。
    /// 
    /// 核心职责：
    /// 1. 存储体素的基本属性（名称、描述、颜色等）
    /// 2. 管理体素的纹理资源，并与 TextureLibrary 交互
    /// 3. 维护体素在 TextureLibrary 中的纹理索引（sliceIndex）
    /// 4. 提供纹理更新和初始化方法
    /// 
    /// 与其他组件的关系：
    /// - 依赖 TextureLibrary：通过 UpdateTextureIfNeeded 方法注册纹理并获取 sliceIndex
    /// - 被 VoxelRegistry 管理：在 VoxelRegistry 中注册并获得唯一的 typeId
    /// - 被 Voxel 结构体引用：Voxel 结构体通过 typeId 引用到此定义类
    /// 
    /// 用法：可以在编辑器中创建实例，也可以在运行时动态创建
    /// </summary>
    [CreateAssetMenu(menuName = "Voxels/Voxel Definition", fileName = "Voxel_Definition")]
    public sealed class VoxelDefinition : ScriptableObject
    {
        [Tooltip("Human‑readable name (for debug / UI). Must be unique at runtime.")]
        public string displayName = "Voxel";

        [Tooltip("Description of the voxel.")]
        public string description;

        [Tooltip("Base albedo tint; meshes will multiply vertex colour with this.")]
        public Color32 baseColor = Color.white;
        public Texture2D texture;

        [Tooltip("True if the voxel should be considered transparent when rendering / ray‑casting.")]
        public bool isTransparent = false;

        /// <summary>Automatically filled in by <see cref="VoxelRegistry"/> when registered.</summary>
        [HideInInspector] public ushort typeId;
        //[HideInInspector]
        public int sliceIndex = -1;   // 从 TextureLibrary 来的贴图编号，之后可以更新为每个面都有单独编号
        //public bool textureNeedsUpdate = false; // 标记贴图是否需要更新

        public void InitRuntime(Texture2D tex)
        {
            if (tex != null)
            {
                texture = tex;
                UpdateTextureIfNeeded();
            }
        }

        // 合并后的更新贴图方法，处理注册和更新
        public void UpdateTextureIfNeeded()
        {
            if (texture != null)
            {
                if (TextureLibrary.IsInitialized)
                {
                    // 尝试使用安全的注册方法
                    int newIndex = TextureLibrary.SafeRegister(texture);
                    if (newIndex >= 0)
                    {
                        sliceIndex = newIndex;
                        Debug.Log($"VoxelDefinition '{name}': updated texture index to {sliceIndex}");
                    }
                    else
                    {
                        // textureNeedsUpdate = true;
                    }
                }
                else
                {
                    // TextureLibrary尚未准备好，标记为需要更新
                    // textureNeedsUpdate = true;
                }
            }
            else
            {
                sliceIndex = 0;
                // textureNeedsUpdate = false;
            }
        }
    }
}
