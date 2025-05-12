using UnityEngine;

namespace Voxels
{
    /// <summary>
    /// 体素资源管理器，负责提供和管理体素渲染所需的材质、纹理和着色器资源。
    /// 
    /// 核心职责：
    /// 1. 提供默认的体素渲染材质 (DefaultMaterial)
    /// 2. 提供和管理默认的纹理数组 (DefaultTextureArray)
    /// 3. 当 TextureLibrary 更新纹理数组时，更新所有相关材质
    /// 4. 确保材质正确引用纹理数组
    /// 
    /// 与其他组件的关系：
    /// - 被 TextureLibrary 依赖：TextureLibrary 初始化时使用 DefaultTextureArray 作为基础，
    ///   并在更新纹理库时调用 UpdateMaterialsWithTextureArray 更新所有材质
    /// - 间接被 VoxelRegistry 依赖：VoxelRegistry 通过 TextureLibrary 间接使用此组件提供的资源
    /// - 支持渲染系统：提供体素渲染所需的材质和纹理资源
    /// 
    /// 设计理念：作为资源提供者，集中管理所有体素渲染相关的资源，减少资源查找和创建的冗余代码，
    /// 并提供合理的默认值，确保即使在资源缺失的情况下系统也能正常运行
    /// </summary>
    public static class VoxelResources
    {
        // 在Resources文件夹中的路径，不包含扩展名
        private const string DefaultMatPath = "Voxels/Voxel_Default_Mat";
        private static Material _defaultMat;
        private static Texture2DArray _defaultTexArray;

        /// <summary>Material that multiplies a white texture with Vertex Color (URP‑Unlit).</summary>
        public static Material DefaultMaterial
        {
            get
            {
                // 1) Try load from Resources (user may override / change properties)
                _defaultMat = Resources.Load<Material>(DefaultMatPath);
                if (_defaultMat != null) 
                {
                    // 确保材质引用了贴图数组
                    EnsureTextureArrayAssigned(_defaultMat);
                    _defaultMat.SetColor("_BaseColor", Color.white);
                    return _defaultMat;
                }
                else
                {
                    Debug.LogError("[VoxelResources] Default material not found in Resources");
                    return null;
                }
            }
        }
        
        /// <summary>获取默认的贴图数组</summary>
        public static Texture2DArray DefaultTextureArray
        {
            get
            {
                // 2)创建一个基本的空数组
                if (_defaultTexArray == null)
                {
                    Debug.Log("[VoxelResources] Default texture array not found in Resources");
                    _defaultTexArray = CreateEmptyTextureArray();
                }
                
                return _defaultTexArray;
            }
        }
        
        /// <summary>创建一个基本的空贴图数组</summary>
        private static Texture2DArray CreateEmptyTextureArray()
        {
            const int size = 16; // 小贴图，节省内存
            
            // 创建一个只有一层的贴图数组
            Texture2DArray arr = new Texture2DArray(size, size, 1, TextureFormat.RGBA32, false);
            
            // 创建一个简单的白色贴图作为默认值
            Color32[] pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }
            
            // 复制到贴图数组
            arr.SetPixels32(pixels, 0);
            // 应用更改，不生成 mipmap
            arr.Apply(false, false);
            arr.wrapMode = TextureWrapMode.Repeat;
            arr.filterMode = FilterMode.Point;
            
            Debug.Log("[VoxelResources] Created empty texture array with 1 white texture");
            return arr;
        }
        
        /// <summary>确保材质引用了贴图数组</summary>
        public static void EnsureTextureArrayAssigned(Material material)
        {
            if (material == null) return;
            
            // 检查材质是否有_Blocks属性
            if (material.HasProperty("_Blocks"))
            {
                // 如果当前没有设置或已设置但为null
                Texture currentTex = material.GetTexture("_Blocks");
                if (currentTex == null)
                {
                    material.SetTexture("_Blocks", DefaultTextureArray);
                    Debug.Log($"[VoxelResources] Assigned default texture array to material '{material.name}'");
                }
            }
        }
        
        /// <summary>当TextureLibrary更新时更新所有材质</summary>
        public static void UpdateMaterialsWithTextureArray(Texture2DArray newArray)
        {
            if (newArray == null) return;
            
            // 设置全局贴图参数
            Shader.SetGlobalTexture("_Blocks", newArray);
            
            // 更新默认材质
            if (_defaultMat != null && _defaultMat.HasProperty("_Blocks"))
            {
                _defaultMat.SetTexture("_Blocks", newArray);
            }
            
            // 缓存新的贴图数组作为默认值
            _defaultTexArray = newArray;
            
            Debug.Log("[VoxelResources] Updated all materials with new texture array");
        }
    }
}
