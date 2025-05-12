using UnityEngine;

namespace Voxels
{
    /// <summary>
    /// 体素资源管理器，负责提供和管理体素渲染所需的材质、纹理和着色器资源。
    /// 
    /// 核心职责：
    /// 1. 提供默认的体素渲染材质 (DefaultMaterial)
    /// 2. 提供和管理默认的texture sheet (DefaultTextureSheet)
    /// 3. 当纹理被更新时，更新所有相关材质
    /// 4. 确保材质正确引用纹理
    /// 
    /// 与其他组件的关系：
    /// - 被 TextureLibrary 依赖：TextureLibrary 初始化时使用 DefaultTextureSheet 作为基础，
    ///   并在更新纹理库时调用 UpdateMaterialsWithTextureSheet 更新所有材质
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
        private static Texture2D _defaultTextureSheet;

        /// <summary>Material that multiplies a texture sheet with Vertex Color (URP‑Unlit).</summary>
        public static Material DefaultMaterial
        {
            get
            {
                // 1) Try load from Resources (user may override / change properties)
                _defaultMat = Resources.Load<Material>(DefaultMatPath);
                if (_defaultMat != null) 
                {
                    // 确保材质引用了贴图
                    EnsureTextureSheetAssigned(_defaultMat);
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
        
        /// <summary>获取默认的texture sheet</summary>
        public static Texture2D DefaultTextureSheet
        {
            get
            {
                // 如果没有加载，创建一个基本的白色贴图
                if (_defaultTextureSheet == null)
                {
                    Debug.LogError("[VoxelResources] Default texture sheet not found!");
                }
                
                return _defaultTextureSheet;
            }
        }
        
               
        /// <summary>确保材质引用了texture sheet</summary>
        public static void EnsureTextureSheetAssigned(Material material)
        {
            if (material == null) 
            {
                Debug.LogError("[VoxelResources] Default material is null!");
                return;
            }
            
            // 检查材质是否有_TextureSheet属性
            if (material.HasProperty("_TextureSheet"))
            {
                // 如果当前没有设置或已设置但为null
                Texture currentTex = material.GetTexture("_TextureSheet");
                if (currentTex == null)
                {
                    material.SetTexture("_TextureSheet", DefaultTextureSheet);
                    Debug.Log($"[VoxelResources] Assigned default texture sheet to material '{material.name}'");
                }
            }
            else
            {
                Debug.LogError("[VoxelResources] Material does not have _TextureSheet property!");
            }
        }
        
        /// <summary>当TextureLibrary更新时更新所有材质</summary>
        public static void UpdateMaterialsWithTextureSheet(Texture2D newTextureSheet)
        {
            if (newTextureSheet == null) return;
            
            // 设置全局贴图参数
            Shader.SetGlobalTexture("_TextureSheet", newTextureSheet);
            
            // 更新默认材质
            if (_defaultMat != null && _defaultMat.HasProperty("_TextureSheet"))
            {
                _defaultMat.SetTexture("_TextureSheet", newTextureSheet);
            }
            
            // 缓存新的贴图作为默认值
            _defaultTextureSheet = newTextureSheet;
            
            Debug.Log("[VoxelResources] Updated all materials with new texture sheet");
        }
    }
}
