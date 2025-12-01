using UnityEngine;

namespace Voxels
{
    /// <summary>
    /// 简单的颜色纹理缓存实现，使用ColorTextureGenerator
    /// </summary>
    public class SimpleColorTextureCache : IColorTextureCache
    {
        public Texture2D GetOrCreateColorTexture(Color32 color)
        {
            // 直接使用ColorTextureGenerator
            return ColorTextureGenerator.GetOrCreateColorTexture(color);
        }
        
        public Texture2D GetOrCreateColorTexture(string colorName)
        {
            // 从颜色名称解析颜色
            Color32 color = ParseColorName(colorName);
            return GetOrCreateColorTexture(color);
        }
        
        public void ClearCache()
        {
            // 使用ColorTextureGenerator的缓存清理
            ColorTextureGenerator.ClearCache();
        }
        
        private Color32 ParseColorName(string colorName)
        {
            if (string.IsNullOrEmpty(colorName))
                return Color.white;
                
            string[] parts = colorName.Split('+');
            
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
    }
}
