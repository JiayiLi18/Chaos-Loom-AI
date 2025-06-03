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
        
        [Tooltip("Default texture used when individual face textures are not set")]
        public Texture2D texture;

        [System.Serializable]
        public class FaceTexture
        {
            public Texture2D texture;
            [HideInInspector] public int sliceIndex = -1;
            
            [Range(0, 100), Tooltip("Chance (0-100%) to use a random variation")]
            public int randomVariationChance = 0;
            
            [Tooltip("Random texture variations")]
            public Texture2D[] variations = new Texture2D[0];
            
            [HideInInspector] public int[] variationSliceIndices;
        }

        [Tooltip("Textures for each face of the voxel (+X, -X, +Y, -Y, +Z, -Z)")]
        public FaceTexture[] faceTextures = new FaceTexture[6];

        [Tooltip("True if the voxel should be considered transparent when rendering / ray‑casting.")]
        public bool isTransparent = false;

        /// <summary>Automatically filled in by <see cref="VoxelRegistry"/> when registered.</summary>
        [HideInInspector] public ushort typeId;
        
        //[HideInInspector]
        public int sliceIndex = -1;   // 默认贴图的索引，用于向后兼容

        private void OnEnable()
        {
            // 确保faceTextures数组始终有6个元素
            if (faceTextures == null || faceTextures.Length != 6)
            {
                faceTextures = new FaceTexture[6];
                for (int i = 0; i < 6; i++)
                {
                    if (faceTextures[i] == null)
                        faceTextures[i] = new FaceTexture();
                }
            }
            
            // 初始化变化贴图的sliceIndex数组
            for (int i = 0; i < 6; i++)
            {
                if (faceTextures[i] != null && faceTextures[i].variations != null && faceTextures[i].variations.Length > 0)
                {
                    if (faceTextures[i].variationSliceIndices == null || 
                        faceTextures[i].variationSliceIndices.Length != faceTextures[i].variations.Length)
                    {
                        faceTextures[i].variationSliceIndices = new int[faceTextures[i].variations.Length];
                        for (int j = 0; j < faceTextures[i].variationSliceIndices.Length; j++)
                        {
                            faceTextures[i].variationSliceIndices[j] = -1;
                        }
                    }
                }
            }
        }

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
            if (!TextureLibrary.IsInitialized)
            {
                Debug.LogError("[VoxelDefinition] TextureLibrary is not initialized, update texture failed!");
                return;
            }

            // 更新默认贴图
            if (texture != null)
            {
                int newIndex = TextureLibrary.SafeRegister(texture);
                if (newIndex >= 0)
                {
                    sliceIndex = newIndex;
                }
            }
            else
            {
                sliceIndex = 0;
            }

            // 更新每个面的贴图
            for (int i = 0; i < 6; i++)
            {
                if (faceTextures[i] == null)
                    faceTextures[i] = new FaceTexture();

                if (faceTextures[i].texture != null)
                {
                    int newIndex = TextureLibrary.SafeRegister(faceTextures[i].texture);
                    if (newIndex >= 0)
                    {
                        faceTextures[i].sliceIndex = newIndex;
                    }
                }
                else
                {
                    // 如果面没有指定贴图，使用默认贴图
                    faceTextures[i].sliceIndex = sliceIndex;
                }

                // 更新变化贴图的sliceIndex
                if (faceTextures[i].variations != null && faceTextures[i].variations.Length > 0)
                {
                    if (faceTextures[i].variationSliceIndices == null || 
                        faceTextures[i].variationSliceIndices.Length != faceTextures[i].variations.Length)
                    {
                        faceTextures[i].variationSliceIndices = new int[faceTextures[i].variations.Length];
                    }

                    for (int j = 0; j < faceTextures[i].variations.Length; j++)
                    {
                        if (faceTextures[i].variations[j] != null)
                        {
                            int varIndex = TextureLibrary.SafeRegister(faceTextures[i].variations[j]);
                            if (varIndex >= 0)
                            {
                                faceTextures[i].variationSliceIndices[j] = varIndex;
                            }
                        }
                        else
                        {
                            faceTextures[i].variationSliceIndices[j] = faceTextures[i].sliceIndex;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 获取指定面的贴图索引，考虑随机变化
        /// </summary>
        /// <param name="faceIndex">面索引 (0-5: +X, -X, +Y, -Y, +Z, -Z)</param>
        /// <returns>贴图在TextureLibrary中的索引</returns>
        public int GetFaceTextureSliceIndex(int faceIndex)
        {
            // 检查索引是否有效
            if (faceIndex < 0 || faceIndex >= 6 || faceTextures[faceIndex] == null)
                return sliceIndex;
            
            FaceTexture face = faceTextures[faceIndex];
            
            // 检查是否有随机变化和变化贴图
            if (face.randomVariationChance > 0 && 
                face.variations != null && 
                face.variations.Length > 0 && 
                face.variationSliceIndices != null)
            {
                // 生成一个随机数 (0-99)
                int rand = UnityEngine.Random.Range(0, 100);
                
                // 如果随机数小于变化概率，使用随机变化贴图
                if (rand < face.randomVariationChance)
                {
                    int varIndex = UnityEngine.Random.Range(0, face.variations.Length);
                    if (varIndex < face.variationSliceIndices.Length && face.variationSliceIndices[varIndex] >= 0)
                    {
                        return face.variationSliceIndices[varIndex];
                    }
                }
            }
            
            // 返回面的主贴图索引，如果未设置则返回默认贴图索引
            return face.sliceIndex >= 0 ? face.sliceIndex : sliceIndex;
        }

        /// <summary>
        /// 更新体素定义的属性
        /// </summary>
        /// <param name="displayName">显示名称</param>
        /// <param name="baseColor">基础颜色</param>
        /// <param name="texture">贴图</param>
        /// <param name="description">描述</param>
        /// <param name="isTransparent">是否透明</param>
        /// <returns>是否更新成功</returns>
        public bool UpdateDefinition(string displayName, Color32 baseColor, Texture2D texture, 
                                    string description = "", bool isTransparent = false)
        {
            try
            {
                // 更新基本属性
                this.displayName = displayName;
                this.baseColor = baseColor;
                this.description = description;
                this.isTransparent = isTransparent;
                
                // 更新贴图
                if (texture != null)
                {
                    this.texture = texture;
                }
                
                // 更新纹理索引
                UpdateTextureIfNeeded();
                
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[VoxelDefinition] Failed to update definition {name}: {ex.Message}");
                return false;
            }
        }
    }
}
