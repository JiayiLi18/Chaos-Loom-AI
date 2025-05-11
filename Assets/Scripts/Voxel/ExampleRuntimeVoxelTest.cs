using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Voxels
{
    /// <summary>
    /// Demo: adds a new randomised "Crystal" voxel type every 5 seconds while the game runs.
    /// Shows how you might expand the registry dynamically.
    /// </summary>
    public sealed class ExampleRuntimeVoxelCreator : MonoBehaviour
    {
        [SerializeField] Vector3Int vector3Int;
        [SerializeField] ushort voxelId;

        public void TestSetVoxel()
        {
            var grid = FindAnyObjectByType<WorldGrid>();
            grid.SetVoxel(vector3Int, new Voxel(voxelId));
        }

        public void TestSetVoxelChunck()
        {
            var grid = FindAnyObjectByType<WorldGrid>();
            for (int x = 0; x < 20; ++x)
            {
                for (int y = 0; y < 20; ++y)
                {
                    for (int z = 0; z < 20; ++z)
                    { grid.SetVoxel(new Vector3Int(x, y, z), new Voxel(voxelId)); }
                }
            }
        }

        public void InitializeGround()
        {
            var grid = FindAnyObjectByType<WorldGrid>();
            for (int x = 0; x < 40; ++x)
            {
                for (int z = 0; z < 40; ++z)
                {
                    { grid.SetVoxel(new Vector3Int(x, 0, z), new Voxel(voxelId)); }
                }
            }
        }
    }
}
