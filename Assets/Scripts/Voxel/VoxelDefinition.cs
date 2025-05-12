using UnityEngine;

namespace Voxels
{
    /// <summary>
    /// 体素定义类，用于描述体素的外观和属性。
    /// 
    /// 核心职责：
    /// 1. 存储体素的基本属性（名称、描述、颜色等）
    /// 2. 管理体素在texture sheet上的位置(textureIndex)
    /// 3. 提供纹理初始化方法
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
        
        [Tooltip("True if the voxel should be considered transparent when rendering / ray‑casting.")]
        public bool isTransparent = false;

        /// <summary>Automatically filled in by <see cref="VoxelRegistry"/> when registered.</summary>
        [HideInInspector] public ushort typeId;
        
        [Tooltip("Texture index in the texture sheet")]
        public int textureIndex = 0;

    }
}
