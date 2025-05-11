using System.Collections.Generic;
using UnityEngine;

namespace Voxels
{
    /// <summary>
    /// Manages a fixed 100×100×100 voxel world, divided into 10×10×10 SubChunks.
    /// Provides helpers: World‑space → SubChunk lookup; Set/Get voxel by world coord.
    /// </summary>
    public sealed class WorldGrid : MonoBehaviour
    {
        public const int WorldSize = 100;      // voxels along one axis
        public const int SubChunkSize = SubChunk.Size; // 1
        public const int SubChunkCount = WorldSize / SubChunkSize; // 1

        private readonly SubChunk[,,] _subChunks = new SubChunk[SubChunkCount, SubChunkCount, SubChunkCount];

        private void Awake()
        {
            for (int x = 0; x < SubChunkCount; ++x)
                for (int y = 0; y < SubChunkCount; ++y)
                    for (int z = 0; z < SubChunkCount; ++z)
                    {
                        GameObject go = new($"SubChunk_{x}_{y}_{z}");
                        go.transform.SetParent(transform, false);
                        go.transform.localPosition = new Vector3(x * SubChunkSize, y * SubChunkSize, z * SubChunkSize);
                        go.layer = LayerMask.NameToLayer("Voxel");
                        go.tag = "Voxel";
                        var sc = go.AddComponent<SubChunk>();
                        _subChunks[x, y, z] = sc;
                    }
        }

        /// <summary>World voxel coordinate → Set</summary>
        public void SetVoxel(Vector3Int localRaw, Voxel voxel)
        {
            if (!IsInsideWorld(localRaw)) return;
            (SubChunk sc, Vector3Int local) = Map(localRaw);
            sc.SetVoxel(local, voxel);
        }

        public void SetVoxelWorld(Vector3Int worldRaw, Voxel voxel)
        {
            Vector3Int localRaw = WorldToLocalVoxelCoord(worldRaw);
            SetVoxel(localRaw, voxel);
        }

        /// <summary>World voxel coordinate → Get</summary>
        public Voxel GetVoxel(Vector3Int localRaw)
        {
            if (!IsInsideWorld(localRaw)) return Voxel.Air;
            (SubChunk sc, Vector3Int local) = Map(localRaw);
            return sc.GetVoxel(local);
        }

        public Voxel GetVoxelWorld(Vector3Int worldRaw)
        {
            Vector3Int localRaw = WorldToLocalVoxelCoord(worldRaw);
            return GetVoxel(localRaw);
        }

        private static bool IsInsideWorld(Vector3Int p) =>
            (uint)p.x < WorldSize && (uint)p.y < WorldSize && (uint)p.z < WorldSize;

        private static (int cx, int cy, int cz, int lx, int ly, int lz) Split(Vector3Int world)
        {
            int cx = Mathf.FloorToInt((float)world.x / SubChunkSize);
            int cy = Mathf.FloorToInt((float)world.y / SubChunkSize);
            int cz = Mathf.FloorToInt((float)world.z / SubChunkSize);
            int lx = world.x - cx * SubChunkSize;
            int ly = world.y - cy * SubChunkSize;
            int lz = world.z - cz * SubChunkSize;
            return (cx, cy, cz, lx, ly, lz);
        }

        private (SubChunk, Vector3Int) Map(Vector3Int world)
        {
            var t = Split(world);
            return (_subChunks[t.cx, t.cy, t.cz], new Vector3Int(t.lx, t.ly, t.lz));
        }

        /// <summary>将世界坐标转换为相对于WorldGrid的坐标</summary>
        public Vector3Int WorldToLocalVoxelCoord(Vector3Int worldCoord)
        {
            // 获取WorldGrid的世界坐标
            Vector3 gridWorldPos = transform.position;

            // 计算相对于WorldGrid的坐标
            return new Vector3Int(
                worldCoord.x - Mathf.FloorToInt(gridWorldPos.x),
                worldCoord.y - Mathf.FloorToInt(gridWorldPos.y),
                worldCoord.z - Mathf.FloorToInt(gridWorldPos.z)
            );
        }

        /// <summary>将相对于WorldGrid的坐标转换为世界坐标</summary>
        public Vector3Int LocalToWorldVoxelCoord(Vector3Int localCoord)
        {
            // 获取WorldGrid的世界坐标
            Vector3 gridWorldPos = transform.position;

            // 计算世界坐标
            return new Vector3Int(
                localCoord.x + Mathf.FloorToInt(gridWorldPos.x),
                localCoord.y + Mathf.FloorToInt(gridWorldPos.y),
                localCoord.z + Mathf.FloorToInt(gridWorldPos.z)
            );
        }
    }
}