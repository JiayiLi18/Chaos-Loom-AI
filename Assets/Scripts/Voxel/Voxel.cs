using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Voxels
{
    /// <summary>
    /// 体素轻量级结构体，代表游戏世界中的实际体素实例。
    /// 
    /// 核心职责：
    /// 1. 以最小的内存占用（仅存储一个 typeId）表示一个体素实例
    /// 2. 提供访问对应 VoxelDefinition 的属性（Definition）
    /// 3. 提供快速判断是否为空气的属性（IsAir）
    /// 4. 实现相等比较等基本操作
    /// 
    /// 与其他组件的关系：
    /// - 依赖 VoxelRegistry：通过 typeId 查找完整的 VoxelDefinition
    /// - 通过 VoxelDefinition 间接依赖纹理和其他资源
    /// 
    /// 设计理念：通过只存储 typeId 而非整个定义对象，使结构体保持轻量，
    /// 适合在大量体素构成的世界中高效存储和传递
    /// </summary>
    [System.Serializable]
    public struct Voxel : System.IEquatable<Voxel>
    {
        [SerializeField] private ushort _typeId;
        [SerializeField] private Color32 _color;  // 颜色信息，仅用于可变色体素

        public ushort TypeId => _typeId;
        public bool   IsAir  => _typeId == 0;
        public VoxelDefinition Definition => VoxelRegistry.GetDefinition(_typeId);
        
        // 是否是可变色体素（ID = 1）
        public bool IsColorable => _typeId == 1;
        
        // 获取体素颜色
        public Color32 Color
        {
            get => IsColorable ? _color : new Color32(255, 255, 255, 255);
            set => _color = value;
        }

        // 创建普通体素
        public Voxel(ushort id)
        {
            _typeId = id;
            _color = new Color32(255, 255, 255, 255);
        }

        // 创建可变色体素
        public Voxel(Color32 color)
        {
            _typeId = 1; // 可变色体素的ID固定为1
            _color = color;
        }

        public static readonly Voxel Air = new Voxel(0);

        public bool Equals(Voxel other) => 
            _typeId == other._typeId && 
            (!IsColorable || _color.Equals(other._color));
            
        public override bool Equals(object obj) => 
            obj is Voxel v && Equals(v);
            
        public override int GetHashCode() => 
            IsColorable ? (_typeId, _color).GetHashCode() : _typeId.GetHashCode();
            
        public static bool operator ==(Voxel a, Voxel b) => a.Equals(b);
        public static bool operator !=(Voxel a, Voxel b) => !a.Equals(b);
    }
}

