using UnityEngine;
using System.Collections.Generic;

namespace Voxels
{
    /// <summary>
    /// 颜色纹理缓存接口，用于管理颜色到纹理的转换
    /// </summary>
    public interface IColorTextureCache
    {
        /// <summary>
        /// 根据颜色获取或创建对应的纹理
        /// </summary>
        Texture2D GetOrCreateColorTexture(Color32 color);
        
        /// <summary>
        /// 根据颜色名称获取或创建对应的纹理
        /// </summary>
        Texture2D GetOrCreateColorTexture(string colorName);
        
        /// <summary>
        /// 清理缓存
        /// </summary>
        void ClearCache();
    }
    
    /// <summary>
    /// VoxelDefinition与SurfaceSpec之间的转换器
    /// </summary>
    public static class VoxelSurfaceTranslator
    {
        /// <summary>
        /// 面数量
        /// </summary>
        public const int FaceCount = 6;
        
        /// <summary>
        /// 从VoxelDefinition创建6个面的SurfaceSpec数组
        /// </summary>
        public static SurfaceSpec[] FromDefinition(VoxelDefinition def)
        {
            var specs = New6(Color.white);
            
            if (def == null)
            {
                Debug.LogWarning("[VoxelSurfaceTranslator] VoxelDefinition is null, using default white colors");
                return specs;
            }
            
            // 检查是否有面纹理（face_textures不为空且至少有一个面有纹理）
            bool hasFaceTextures = HasValidFaceTextures(def);
            
            if (hasFaceTextures)
            {
                // 类型2：面纹理模式 - 每个面有独立的纹理
                //Debug.Log("[VoxelSurfaceTranslator] Loading face textures mode");
                for (int i = 0; i < FaceCount; i++)
                {
                    var faceTexture = def.faceTextures[i];
                    if (faceTexture != null && faceTexture.texture != null)
                    {
                        specs[i].mode = SurfaceMode.Texture;
                        specs[i].albedo = faceTexture.texture;
                        specs[i].origin = SurfaceSpec.Origin.FromDef;
                        specs[i].isTemporary = false;
                        
                        // 如果贴图名称看起来像RGB颜色，转换为纯色模式
                        if (LooksLikeRgbPng(faceTexture.texture.name))
                        {
                            Color32 rgbColor = ParseRgbNameToColor(faceTexture.texture.name);
                            specs[i].mode = SurfaceMode.Color;
                            specs[i].baseColor = rgbColor;
                            specs[i].albedo = null;
                            specs[i].origin = SurfaceSpec.Origin.Generated;
                            //Debug.Log($"[VoxelSurfaceTranslator] Converted RGB texture {faceTexture.texture.name} to color {rgbColor}");
                        }
                    }
                    else
                    {
                        // 如果某个面没有纹理，使用baseColor
                        specs[i].mode = SurfaceMode.Color;
                        specs[i].baseColor = def.baseColor;
                        specs[i].origin = SurfaceSpec.Origin.FromDef;
                    }
                }
            }
            else if (HasUnifiedTexture(def))
            {
                // 类型1：统一纹理模式 - 所有面使用同一个纹理
                //Debug.Log("[VoxelSurfaceTranslator] Loading unified texture mode");
                var unifiedTexture = def.faceTextures[0].texture;
                
                for (int i = 0; i < FaceCount; i++)
                {
                    specs[i].mode = SurfaceMode.Texture;
                    specs[i].albedo = unifiedTexture;
                    specs[i].origin = SurfaceSpec.Origin.FromDef;
                    specs[i].isTemporary = false;
                    
                    // 如果贴图名称看起来像RGB颜色，转换为纯色模式
                    if (LooksLikeRgbPng(unifiedTexture.name))
                    {
                        Color32 rgbColor = ParseRgbNameToColor(unifiedTexture.name);
                        specs[i].mode = SurfaceMode.Color;
                        specs[i].baseColor = rgbColor;
                        specs[i].albedo = null;
                        specs[i].origin = SurfaceSpec.Origin.Generated;
                        //Debug.Log($"[VoxelSurfaceTranslator] Converted unified RGB texture {unifiedTexture.name} to color {rgbColor}");
                    }
                }
            }
            else
            {
                // 没有纹理，使用baseColor
                //Debug.Log("[VoxelSurfaceTranslator] Loading base color mode");
                for (int i = 0; i < FaceCount; i++)
                {
                    specs[i].mode = SurfaceMode.Color;
                    specs[i].baseColor = def.baseColor;
                    specs[i].origin = SurfaceSpec.Origin.FromDef;
                    specs[i].isTemporary = false;
                }
            }
            
            return specs;
        }
        
        /// <summary>
        /// 将SurfaceSpec数组应用回VoxelDefinition
        /// </summary>
        public static void ApplyToDefinition(SurfaceSpec[] stableSpecs, VoxelDefinition def, IColorTextureCache colorTexCache)
        {
            if (stableSpecs == null || def == null || colorTexCache == null)
            {
                Debug.LogError("[VoxelSurfaceTranslator] Invalid parameters for ApplyToDefinition");
                return;
            }
            
            // 检查是否所有面都是纯色模式
            bool allColorMode = true;
            bool allSameColor = true;
            Color32 firstColor = stableSpecs[0].baseColor;
            
            for (int i = 0; i < FaceCount; i++)
            {
                if (stableSpecs[i].mode != SurfaceMode.Color)
                {
                    allColorMode = false;
                    break;
                }
                
                if (!stableSpecs[i].baseColor.Equals(firstColor))
                {
                    allSameColor = false;
                }
            }
            
            if (allColorMode && allSameColor)
            {
                // 所有面都是相同颜色，使用统一纹理模式
                def.baseColor = firstColor;
                string colorName = GenerateColorName(firstColor);
                def.faceTextures = new VoxelDefinition.FaceTexture[FaceCount];
                
                // 为第一个面创建颜色纹理
                var colorTexture = colorTexCache.GetOrCreateColorTexture(colorName);
                def.faceTextures[0] = new VoxelDefinition.FaceTexture { texture = colorTexture };
                
                // 其他面设为null，表示使用统一纹理
                for (int i = 1; i < FaceCount; i++)
                {
                    def.faceTextures[i] = null;
                }
                
                //Debug.Log($"[VoxelSurfaceTranslator] Applied unified color mode: {firstColor} -> {colorName}");
            }
            else
            {
                // 混合模式或面纹理模式
                def.faceTextures = new VoxelDefinition.FaceTexture[FaceCount];
                
                for (int i = 0; i < FaceCount; i++)
                {
                    var spec = stableSpecs[i];
                    
                    if (spec.mode == SurfaceMode.Color)
                    {
                        // 纯色模式：创建颜色纹理
                        string colorName = GenerateColorName(spec.baseColor);
                        var colorTexture = colorTexCache.GetOrCreateColorTexture(colorName);
                        def.faceTextures[i] = new VoxelDefinition.FaceTexture { texture = colorTexture };
                    }
                    else if (spec.mode == SurfaceMode.Texture && spec.albedo != null)
                    {
                        // 贴图模式：直接使用贴图
                        def.faceTextures[i] = new VoxelDefinition.FaceTexture { texture = spec.albedo };
                    }
                    else
                    {
                        // 无效状态：使用默认颜色
                        def.faceTextures[i] = new VoxelDefinition.FaceTexture { texture = null };
                    }
                }
                
                // 设置baseColor为第一个面的颜色
                def.baseColor = stableSpecs[0].baseColor;
                
                //Debug.Log("[VoxelSurfaceTranslator] Applied mixed mode with individual face textures");
            }
        }
        
        /// <summary>
        /// 创建6个面的SurfaceSpec数组
        /// </summary>
        private static SurfaceSpec[] New6(Color defaultColor)
        {
            var specs = new SurfaceSpec[FaceCount];
            for (int i = 0; i < FaceCount; i++)
            {
                specs[i] = new SurfaceSpec();
                specs[i].baseColor = defaultColor;
            }
            return specs;
        }
        
        /// <summary>
        /// 检查VoxelDefinition是否有有效的面纹理
        /// </summary>
        private static bool HasValidFaceTextures(VoxelDefinition def)
        {
            if (def.faceTextures == null || def.faceTextures.Length != FaceCount)
                return false;
                
            for (int i = 0; i < FaceCount; i++)
            {
                if (def.faceTextures[i] != null && def.faceTextures[i].texture != null)
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// 检查VoxelDefinition是否有统一纹理
        /// </summary>
        private static bool HasUnifiedTexture(VoxelDefinition def)
        {
            return def.faceTextures != null && 
                   def.faceTextures.Length > 0 && 
                   def.faceTextures[0] != null && 
                   def.faceTextures[0].texture != null;
        }
        
        /// <summary>
        /// 检查纹理名称是否看起来像RGB PNG文件名
        /// </summary>
        private static bool LooksLikeRgbPng(string textureName)
        {
            if (string.IsNullOrEmpty(textureName))
                return false;
                
            // 检查格式：数字+数字+数字.png 或 数字+数字+数字
            string nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(textureName);
            string[] parts = nameWithoutExt.Split('+');
            
            if (parts.Length == 3)
            {
                // 检查每个部分是否都是数字
                foreach (string part in parts)
                {
                    if (!int.TryParse(part, out int value) || value < 0 || value > 255)
                        return false;
                }
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 从RGB文件名解析颜色
        /// </summary>
        private static Color32 ParseRgbNameToColor(string textureName)
        {
            if (string.IsNullOrEmpty(textureName))
                return Color.white;
                
            string nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(textureName);
            string[] parts = nameWithoutExt.Split('+');
            
            if (parts.Length == 3)
            {
                if (int.TryParse(parts[0], out int r) &&
                    int.TryParse(parts[1], out int g) &&
                    int.TryParse(parts[2], out int b))
                {
                    return new Color32((byte)r, (byte)g, (byte)b, 255);
                }
            }
            
            return Color.white;
        }
        
        /// <summary>
        /// 生成颜色名称（RGB格式）
        /// </summary>
        private static string GenerateColorName(Color32 color)
        {
            return $"{color.r}+{color.g}+{color.b}";
        }
        
        /// <summary>
        /// 深拷贝SurfaceSpec数组
        /// </summary>
        public static SurfaceSpec[] DeepCopy(SurfaceSpec[] source)
        {
            if (source == null)
                return null;
                
            var copy = new SurfaceSpec[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                copy[i] = source[i]?.DeepCopy();
            }
            return copy;
        }
    }
}
