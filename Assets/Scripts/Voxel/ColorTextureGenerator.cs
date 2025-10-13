using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace Voxels
{
    /// <summary>
    /// 颜色纹理生成器，负责根据颜色创建体素纹理
    /// 
    /// 核心职责：
    /// 1. 管理颜色到纹理的映射关系
    /// 2. 检测已存在的颜色纹理
    /// 3. 生成16x16的纯色纹理
    /// 4. 处理6个面的颜色分配
    /// 
    /// 与其他组件的关系：
    /// - 使用 TextureLibrary：将生成的纹理注册到纹理库
    /// - 被 VoxelEditingUI 使用：提供颜色纹理生成服务
    /// </summary>
    public static class ColorTextureGenerator
    {
        private const int TEXTURE_SIZE = 16;
        private const string COLOR_TEXTURE_FOLDER = "VoxelTextures";
        
        // 缓存已生成的颜色纹理，避免重复创建
        private static readonly Dictionary<Color32, Texture2D> _colorTextureCache = new Dictionary<Color32, Texture2D>();
        
        /// <summary>
        /// 为体素生成纹理，支持6个面不同颜色或统一颜色
        /// </summary>
        /// <param name="faceColors">6个面的颜色数组，如果为null则使用统一颜色</param>
        /// <param name="unifiedColor">统一颜色，当faceColors为null时使用</param>
        /// <returns>返回的纹理数组，包含6个面的纹理</returns>
        public static Texture2D[] GenerateVoxelTextures(Color32[] faceColors, Color32 unifiedColor)
        {
            Texture2D[] textures = new Texture2D[6];
            
            if (faceColors != null)
            {
                // 6个面不同颜色
                for (int i = 0; i < 6; i++)
                {
                    textures[i] = GetOrCreateColorTexture(faceColors[i]);
                }
            }
            else
            {
                // 统一颜色
                Texture2D unifiedTexture = GetOrCreateColorTexture(unifiedColor);
                for (int i = 0; i < 6; i++)
                {
                    textures[i] = unifiedTexture;
                }
            }
            
            return textures;
        }
        
        /// <summary>
        /// 获取或创建指定颜色的纹理
        /// </summary>
        /// <param name="color">目标颜色</param>
        /// <returns>对应的纹理</returns>
        public static Texture2D GetOrCreateColorTexture(Color32 color)
        {
            // 标准化颜色（去除alpha变化，只保留RGB）
            Color32 normalizedColor = new Color32(color.r, color.g, color.b, 255);
            
            // 检查缓存
            if (_colorTextureCache.TryGetValue(normalizedColor, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }
            
            // 检查是否已存在文件
            string texturePath = GetColorTexturePath(normalizedColor);
            if (File.Exists(texturePath))
            {
                // 加载现有纹理
                Texture2D existingTexture = LoadTextureFromFile(texturePath);
                if (existingTexture != null)
                {
                    _colorTextureCache[normalizedColor] = existingTexture;
                    return existingTexture;
                }
            }
            
            // 创建新纹理
            Texture2D newTexture = CreateColorTexture(normalizedColor);
            if (newTexture != null)
            {
                // 保存到文件
                SaveTextureToFile(newTexture, texturePath);
                
                // 注册到TextureLibrary
                if (TextureLibrary.IsInitialized)
                {
                    TextureLibrary.SafeRegister(newTexture);
                }
                
                // 添加到缓存
                _colorTextureCache[normalizedColor] = newTexture;
                
                Debug.Log($"[ColorTextureGenerator] Created new color texture: {GetColorTextureName(normalizedColor)}");
            }
            
            return newTexture;
        }
        
        /// <summary>
        /// 创建纯色纹理
        /// </summary>
        private static Texture2D CreateColorTexture(Color32 color)
        {
            Texture2D texture = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.RGBA32, false);
            texture.name = GetColorTextureName(color);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            
            // 填充颜色
            Color32[] pixels = new Color32[TEXTURE_SIZE * TEXTURE_SIZE];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            
            texture.SetPixels32(pixels);
            texture.Apply();
            
            return texture;
        }
        
        /// <summary>
        /// 从文件加载纹理
        /// </summary>
        private static Texture2D LoadTextureFromFile(string filePath)
        {
            try
            {
                byte[] fileData = File.ReadAllBytes(filePath);
                Texture2D texture = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.RGBA32, false);
                
                if (texture.LoadImage(fileData))
                {
                    texture.filterMode = FilterMode.Point;
                    texture.wrapMode = TextureWrapMode.Repeat;
                    return texture;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ColorTextureGenerator] Failed to load texture from {filePath}: {ex.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// 保存纹理到文件
        /// </summary>
        private static void SaveTextureToFile(Texture2D texture, string filePath)
        {
            try
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // 保存为PNG
                byte[] pngData = texture.EncodeToPNG();
                File.WriteAllBytes(filePath, pngData);
                
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ColorTextureGenerator] Failed to save texture to {filePath}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 获取颜色纹理的文件路径
        /// </summary>
        private static string GetColorTexturePath(Color32 color)
        {
            string fileName = GetColorTextureName(color) + ".png";
            string fullPath = Path.Combine(Application.dataPath, "Resources", COLOR_TEXTURE_FOLDER, fileName);
            return fullPath;
        }
        
        /// <summary>
        /// 获取颜色纹理的文件名
        /// </summary>
        private static string GetColorTextureName(Color32 color)
        {
            return $"{color.r}+{color.g}+{color.b}";
        }
        
        /// <summary>
        /// 检查指定颜色是否已有对应的纹理文件
        /// </summary>
        public static bool HasColorTexture(Color32 color)
        {
            string texturePath = GetColorTexturePath(color);
            return File.Exists(texturePath);
        }
        
        /// <summary>
        /// 清空缓存（用于调试或重新加载）
        /// </summary>
        public static void ClearCache()
        {
            _colorTextureCache.Clear();
            Debug.Log("[ColorTextureGenerator] Cache cleared");
        }
        
        /// <summary>
        /// 获取缓存中的纹理数量
        /// </summary>
        public static int GetCacheSize()
        {
            return _colorTextureCache.Count;
        }
    }
}
