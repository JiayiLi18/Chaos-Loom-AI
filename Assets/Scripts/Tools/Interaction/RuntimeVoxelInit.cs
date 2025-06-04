using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Voxels
{
    /// <summary>
    /// Demo: adds a new randomised "Crystal" voxel type every 5 seconds while the game runs.
    /// Shows how you might expand the registry dynamically.
    /// </summary>
    public sealed class RuntimeVoxelInit : MonoBehaviour
    {
        [SerializeField] Vector3Int testVector3;
        [SerializeField] ushort testVoxelId;
        private bool isGroundInitialized = false;

        private void Start()
        {
            // 订阅TextureLibrary的初始化事件
            TextureLibrary.OnLibraryInitialized += OnTextureLibraryInitialized;
        }

        private void OnTextureLibraryInitialized()
        {
            // 等待VoxelJsonDB初始化完成
            StartCoroutine(WaitForVoxelJsonDB());
        }

        private IEnumerator WaitForVoxelJsonDB()
        {
            var jsonDB = FindAnyObjectByType<VoxelJsonDB>();
            // 等待VoxelJsonDB初始化完成
            while (jsonDB != null && !jsonDB.IsInitialized)
            {
                yield return null;
            }

            // 两个系统都初始化完成后，初始化地面
            if (!isGroundInitialized)
            {
                InitializeGround();
                isGroundInitialized = true;
            }
        }

        public void TestSetVoxel()
        {
            var grid = FindAnyObjectByType<WorldGrid>();
            grid.SetVoxel(testVector3, new Voxel(testVoxelId));
        }

        public void TestSetVoxelChunck()
        {
            var grid = FindAnyObjectByType<WorldGrid>();
            for (int x = 0; x < 20; ++x)
            {
                for (int y = 0; y < 20; ++y)
                {
                    for (int z = 0; z < 20; ++z)
                    { grid.SetVoxel(new Vector3Int(x, y, z), new Voxel(testVoxelId)); }
                }
            }
        }

        public void InitializeGround()
        {
            var grid = FindAnyObjectByType<WorldGrid>();
            
            // 计算世界中心点
            int centerX = WorldGrid.WorldSize / 2;
            int centerZ = WorldGrid.WorldSize / 2;
            
            // 计算平台的起始和结束位置（8x8的平台）
            int startX = centerX - 32;
            int startZ = centerZ - 32;
            int endX = centerX + 32;
            int endZ = centerZ + 32;

            // 确保不会超出世界边界
            startX = Mathf.Clamp(startX, 0, WorldGrid.WorldSize - 1);
            startZ = Mathf.Clamp(startZ, 0, WorldGrid.WorldSize - 1);
            endX = Mathf.Clamp(endX, 0, WorldGrid.WorldSize - 1);
            endZ = Mathf.Clamp(endZ, 0, WorldGrid.WorldSize - 1);

            // 生成平台
            for (int x = startX; x < endX; ++x)
            {
                for (int z = startZ; z < endZ; ++z)
                {
                    //如果voxelType 2存在，则使用voxelType 2，否则使用voxelType 1
                    var voxel = new Voxel(2);
                    if (voxel.Definition != null)
                    {
                        grid.SetVoxel(new Vector3Int(x, 0, z), voxel);
                    }
                    else
                    {
                        grid.SetVoxel(new Vector3Int(x, 0, z), new Voxel(1));
                    }
                }
            }
        }
    }
}
