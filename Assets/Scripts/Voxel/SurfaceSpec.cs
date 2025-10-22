using UnityEngine;

namespace Voxels
{
    /// <summary>
    /// 表面显示模式
    /// </summary>
    public enum SurfaceMode 
    { 
        Color,      // 纯色模式
        Texture     // 贴图模式
    }

    /// <summary>
    /// 表面规格定义，统一管理每个面的显示方式
    /// </summary>
    [System.Serializable]
    public class SurfaceSpec
    {
        [Header("Display Mode")]
        public SurfaceMode mode = SurfaceMode.Color;
        
        [Header("Color Settings")]
        public Color baseColor = Color.white;
        
        [Header("Texture Settings")]
        public Texture2D albedo;          // 贴图资源
        
        [Header("Metadata")]
        public Origin origin = Origin.FromDef;
        public bool isTemporary = false;   // 是否为临时状态（未确认的修改）
        
        /// <summary>
        /// 表面来源类型
        /// </summary>
        public enum Origin 
        { 
            FromDef,           // 来自VoxelDefinition
            FromTempColor,      // 来自临时颜色调整
            FromUserTexture,    // 来自用户上传的贴图
            Generated          // 由系统生成（如从RGB文件名推断颜色）
        }
        
        /// <summary>
        /// 默认构造函数
        /// </summary>
        public SurfaceSpec()
        {
            mode = SurfaceMode.Color;
            baseColor = Color.white;
            albedo = null;
            origin = Origin.FromDef;
            isTemporary = false;
        }
        
        /// <summary>
        /// 复制构造函数
        /// </summary>
        public SurfaceSpec(SurfaceSpec other)
        {
            if (other != null)
            {
                mode = other.mode;
                baseColor = other.baseColor;
                albedo = other.albedo;
                origin = other.origin;
                isTemporary = other.isTemporary;
            }
        }
        
        /// <summary>
        /// 深拷贝当前SurfaceSpec
        /// </summary>
        public SurfaceSpec DeepCopy()
        {
            return new SurfaceSpec(this);
        }
        
        /// <summary>
        /// 设置为纯色模式
        /// </summary>
        public void SetColorMode(Color color, Origin originType = Origin.FromTempColor)
        {
            mode = SurfaceMode.Color;
            baseColor = color;
            albedo = null;
            origin = originType;
            isTemporary = true;
        }
        
        /// <summary>
        /// 设置为贴图模式
        /// </summary>
        public void SetTextureMode(Texture2D texture, Origin originType = Origin.FromUserTexture)
        {
            mode = SurfaceMode.Texture;
            albedo = texture;
            origin = originType;
            isTemporary = true;
        }
        
        /// <summary>
        /// 重置为稳定状态
        /// </summary>
        public void ResetToStable()
        {
            isTemporary = false;
        }
        
        /// <summary>
        /// 检查是否为纯色模式
        /// </summary>
        public bool IsColorMode => mode == SurfaceMode.Color;
        
        /// <summary>
        /// 检查是否为贴图模式
        /// </summary>
        public bool IsTextureMode => mode == SurfaceMode.Texture;
        
        /// <summary>
        /// 检查是否有有效的贴图
        /// </summary>
        public bool HasValidTexture => IsTextureMode && albedo != null;
        
        /// <summary>
        /// 获取当前有效颜色（贴图模式下返回白色，纯色模式下返回baseColor）
        /// </summary>
        public Color GetEffectiveColor()
        {
            return IsColorMode ? baseColor : Color.white;
        }
        
        /// <summary>
        /// 获取当前有效贴图
        /// </summary>
        public Texture2D GetEffectiveTexture()
        {
            return IsTextureMode ? albedo : null;
        }
        
        public override string ToString()
        {
            return $"SurfaceSpec(mode={mode}, color={baseColor}, texture={albedo?.name ?? "null"}, origin={origin}, temp={isTemporary})";
        }
    }
}
