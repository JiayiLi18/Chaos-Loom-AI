using UnityEngine;

namespace Voxels
{
    /// <summary>
    /// 元胞自动机规则定义，用于描述体素的生命演化行为。
    /// ！！！暂时没有使用，因为目前voxel搭建以视觉为主，不需要复杂的行为！！！
    /// 
    /// 核心职责：
    /// 1. 定义体素的生命状态数量（0=死亡，1=存活，>1可表示多态）
    /// 2. 定义细胞诞生和存活的邻居数量规则
    /// 3. 定义每个面的特殊行为（生长方向、衰减速率等）
    /// 
    /// 与其他组件的关系：
    /// - 被 VoxelDefinition 引用：通过 lifeRules 属性关联
    /// - 被 CellularAutomataSystem 使用：用于计算元胞自动机的演化
    /// </summary>
    [CreateAssetMenu(menuName="Voxels/Life Rules", fileName="LifeRules_B6S5678")]
    public sealed class VoxelLifeRules : ScriptableObject
    {
        [Tooltip("细胞状态数量（0=死亡，1=存活，>1表示多态）")]
        public byte stateCount = 2;
        
        [Tooltip("诞生规则：当死亡细胞周围有多少个存活邻居时会诞生新细胞")]
        public int[] birth = new int[] { 6 };
        
        [Tooltip("存活规则：当存活细胞周围有多少个存活邻居时会继续存活")]
        public int[] survive = new int[] { 5, 6, 7, 8 };

        [System.Serializable]
        public struct FaceBehaviour 
        {
            [Tooltip("该面是否能向外生成新体素")]
            public bool canSpawn;
            
            [Tooltip("该面在无支撑时的消融速度")]
            public float decay;
            
            [Tooltip("生成时使用的体素类型ID")]
            public ushort spawnTypeId;
        }
        
        [Tooltip("六个面的特殊行为设置 [+X, -X, +Y, -Y, +Z, -Z]")]
        public FaceBehaviour[] faceBehaviours = new FaceBehaviour[6];
    }
} 