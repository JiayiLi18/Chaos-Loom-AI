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
        public const int WorldSize = 64;      // voxels along one axis
        public const int WorldHeight = 64;
        public const int SubChunkSize = SubChunk.Size; // 1
        public const int SubChunkWidth = WorldSize / SubChunkSize; // 1
        public const int SubChunkHeight = WorldHeight / SubChunkSize; // 1

        private readonly SubChunk[,,] _subChunks = new SubChunk[SubChunkWidth, SubChunkWidth, SubChunkWidth];

        /// <summary>
        /// 获取指定SubChunk在某个方向上的相邻区块
        /// </summary>
        public SubChunk GetNeighbour(SubChunk chunk, Vector3Int direction)
        {
            if (!chunk) return null;

            // 找到当前chunk在数组中的索引
            Vector3Int chunkPos = Vector3Int.zero;
            bool found = false;

            for (int x = 0; x < SubChunkWidth && !found; x++)
                for (int y = 0; y < SubChunkHeight && !found; y++)
                    for (int z = 0; z < SubChunkWidth && !found; z++)
                        if (_subChunks[x, y, z] == chunk)
                        {
                            chunkPos = new Vector3Int(x, y, z);
                            found = true;
                            break;
                        }

            if (!found) return null;

            // 计算目标位置
            Vector3Int targetPos = chunkPos + direction;

            // 检查是否在边界内
            if (targetPos.x < 0 || targetPos.x >= SubChunkWidth ||
                targetPos.y < 0 || targetPos.y >= SubChunkHeight ||
                targetPos.z < 0 || targetPos.z >= SubChunkWidth)
                return null;

            return _subChunks[targetPos.x, targetPos.y, targetPos.z];
        }

        private void Awake()
        {
            for (int x = 0; x < SubChunkWidth; ++x)
                for (int y = 0; y < SubChunkHeight; ++y)
                    for (int z = 0; z < SubChunkWidth; ++z)
                    {
                        GameObject go = new($"SubChunk_{x}_{y}_{z}");
                        go.transform.SetParent(transform, false);
                        go.transform.localPosition = new Vector3(x * SubChunkSize, y * SubChunkSize, z * SubChunkSize);
                        go.layer = LayerMask.NameToLayer("Voxel");
                        go.tag = "Voxel";
                        var sc = go.AddComponent<SubChunk>();
                        // 确保初始化体素数组为空气
                        sc.InitVoxels();
                        _subChunks[x, y, z] = sc;
                    }
                    
        }

        /// <summary>World voxel coordinate → Set</summary>
        public void SetVoxel(Vector3Int localRaw, Voxel voxel)
        {
            if (!IsInsideWorld(localRaw)) return;

            // 如果是空气方块且该位置不可破坏，则不执行设置
            if (voxel.IsAir && !CanBreakVoxelAt(localRaw))
            {
                return;
            }

            (SubChunk sc, Vector3Int local) = Map(localRaw);
            sc.SetVoxel(local, voxel);

            // 如果修改的是边界体素，通知相邻区块更新
            UpdateNeighborsIfBorderVoxel(sc, local);
        }

        /// <summary>
        /// 如果修改的是边界体素，更新相邻区块
        /// </summary>
        private void UpdateNeighborsIfBorderVoxel(SubChunk chunk, Vector3Int localPos)
        {
            // 检查X轴边界
            if (localPos.x == 0)
                UpdateNeighborInDirection(chunk, -Vector3Int.right);
            else if (localPos.x == SubChunkSize - 1)
                UpdateNeighborInDirection(chunk, Vector3Int.right);

            // 检查Y轴边界
            if (localPos.y == 0)
                UpdateNeighborInDirection(chunk, -Vector3Int.up);
            else if (localPos.y == SubChunkSize - 1)
                UpdateNeighborInDirection(chunk, Vector3Int.up);

            // 检查Z轴边界
            if (localPos.z == 0)
                UpdateNeighborInDirection(chunk, -Vector3Int.forward);
            else if (localPos.z == SubChunkSize - 1)
                UpdateNeighborInDirection(chunk, Vector3Int.forward);
        }

        /// <summary>
        /// 更新指定方向的相邻区块
        /// </summary>
        private void UpdateNeighborInDirection(SubChunk chunk, Vector3Int direction)
        {
            SubChunk neighbor = GetNeighbour(chunk, direction);
            if (neighbor != null)
            {
                neighbor.MarkDirtyAll();
            }
        }

        public void SetVoxelWorld(Vector3Int worldRaw, Voxel voxel)
        {
            // 如果是空气方块且该位置不可破坏，则不执行设置
            if (voxel.IsAir && !CanBreakVoxelAtWorld(worldRaw))
            {
                Debug.LogWarning($"Cannot set voxel at world position {worldRaw} because it is air and unbreakable");
                return;
            }

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

        /// <summary>
        /// 获取指定世界坐标所在的SubChunk
        /// </summary>
        public SubChunk GetChunkAt(Vector3Int worldPosition)
        {
            Vector3Int localRaw = WorldToLocalVoxelCoord(worldPosition);
            if (!IsInsideWorld(localRaw)) return null;
            (SubChunk sc, _) = Map(localRaw);
            return sc;
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

        /// <summary>
        /// 尝试获取指定方向上的相邻区块
        /// </summary>
        private bool TryGetChunkAt(SubChunk current, Vector3Int direction, out SubChunk neighbour)
        {
            neighbour = GetNeighbour(current, direction);
            return neighbour != null;
        }

        /// <summary>
        /// 在世界空间中进行射线检测，返回命中的体素坐标和法线方向
        /// </summary>
        public bool RaycastWorld(
            Ray ray,
            float maxDist,
            out Vector3Int hitBlock,     // 世界体素坐标
            out Vector3Int hitNormal,    // 世界法线
            out float distance)
        {
            const float EPS = 1e-4f;             // 推入体素内部，避免贴面误差
            hitBlock = hitNormal = Vector3Int.zero;
            distance = 0f;

            // 将射线转换到网格空间
            Vector3 gridOrigin = transform.position;
            Vector3 localOrigin = ray.origin - gridOrigin;
            Vector3 o = localOrigin + ray.direction.normalized * EPS;
            Vector3 d = ray.direction.normalized;
            Vector3Int v = Vector3Int.FloorToInt(o);      // 当前网格体素坐标
            Vector3 step = new Vector3(
                d.x >= 0 ? 1 : -1,
                d.y >= 0 ? 1 : -1,
                d.z >= 0 ? 1 : -1);

            Vector3 inv = new Vector3(
                Mathf.Abs(d.x) < 1e-8f ? float.PositiveInfinity : 1f / Mathf.Abs(d.x),
                Mathf.Abs(d.y) < 1e-8f ? float.PositiveInfinity : 1f / Mathf.Abs(d.y),
                Mathf.Abs(d.z) < 1e-8f ? float.PositiveInfinity : 1f / Mathf.Abs(d.z));

            Vector3 tMax = new Vector3(
                (step.x > 0 ? (v.x + 1) - o.x : o.x - v.x) * inv.x,
                (step.y > 0 ? (v.y + 1) - o.y : o.y - v.y) * inv.y,
                (step.z > 0 ? (v.z + 1) - o.z : o.z - v.z) * inv.z);

            Vector3 tDelta = inv;
            Vector3Int prev = v;

            while (distance <= maxDist)
            {
                // 检查当前体素是否有效
                if (!IsInsideWorld(v))
                {
                    break;
                }

                // 获取当前位置的体素
                Voxel voxel = GetVoxel(v); // 使用网格局部坐标获取体素
                if (!voxel.IsAir)
                {
                    hitBlock = v;
                    hitNormal = prev - v;        // 进入哪一轴就是法线
                    // 转换回世界坐标
                    hitBlock = LocalToWorldVoxelCoord(hitBlock);
                    return true;
                }

                prev = v;

                // DDA 按 ≤ 依序推进 X→Y→Z
                if (tMax.x <= tMax.y && tMax.x <= tMax.z)
                {
                    distance = tMax.x;
                    tMax.x += tDelta.x;
                    v.x += (int)step.x;
                }
                else if (tMax.y <= tMax.z)
                {
                    distance = tMax.y;
                    tMax.y += tDelta.y;
                    v.y += (int)step.y;
                }
                else
                {
                    distance = tMax.z;
                    tMax.z += tDelta.z;
                    v.z += (int)step.z;
                }
            }
            return false;      // 射线在 maxDist 内没碰到方块
        }

        /// <summary>
        /// 检查指定位置的体素是否可以被破坏
        /// </summary>
        public bool CanBreakVoxelAt(Vector3Int localPos)
        {
            // 检查是否在世界范围内
            if (!IsInsideWorld(localPos))
                return false;

            // 检查Y=0的位置（地面层）是否不可破坏
            if (localPos.y == 0)
                return false;

            return true;
        }

        /// <summary>
        /// 检查指定世界坐标的体素是否可以被破坏
        /// </summary>
        public bool CanBreakVoxelAtWorld(Vector3Int worldRaw)
        {
            Vector3Int localRaw = WorldToLocalVoxelCoord(worldRaw);
            return CanBreakVoxelAt(localRaw);
        }
    }
}