using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Voxels
{
    /// <summary>
    /// MonoBehaviour representing a fixed‑size SubChunk (10³ voxels).
    /// Multiple SubChunks make up the full 100³ world volume.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class SubChunk : MonoBehaviour
    {
        #region Constants / Data
        public const int Size = 16;
        private const int VoxelCount = Size * Size * Size;

        // CPU copy for editable voxels
        [SerializeField, HideInInspector] private Voxel[] _voxelsManaged = new Voxel[VoxelCount];//仅主线程写
        // Native copy fed to jobs
        private NativeArray<Voxel> _voxelsNative;//job只读

        // 边界缓存数组
        private NativeArray<ushort> _borderPX, _borderNX; // +X / -X
        private NativeArray<ushort> _borderPY, _borderNY; // +Y / -Y
        private NativeArray<ushort> _borderPZ, _borderNZ; // +Z / -Z

        private bool _dirtyData = true; // voxel array changed, need to copy to native
        private JobHandle _meshHandle;
        private bool jobRunning = false;

        private bool _isInitialized = false;
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private MeshCollider _collider;

        // Job output containers (reused)
        private NativeList<float3> _verts;
        private NativeList<int> _tris;
        private NativeList<Color32> _cols;
        private NativeList<Vector2> _uvs;
        private NativeList<Vector2> _uvs1;

        private NativeArray<Color32> _palette;
        private NativeArray<float> _slices;
        // 面贴图索引数组 [typeId * 6 + faceIndex] = sliceIndex
        private NativeArray<float> _faceSlices;

        private const float EPS = 1e-5f;
        #endregion

        private void Awake()
        {
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _collider = GetComponent<MeshCollider>();

            // 确保有 MeshCollider 组件
            if (_collider == null)
            {
                _collider = gameObject.AddComponent<MeshCollider>();
            }

            //gameObject.AddComponent<Paintable>();//添加可绘制组件

            // 设置材质
            _renderer.sharedMaterial = VoxelResources.DefaultMaterial;

            _voxelsNative = new NativeArray<Voxel>(VoxelCount, Allocator.Persistent);

            // 创建网格输出容器
            _verts = new NativeList<float3>(1024, Allocator.Persistent);
            _tris = new NativeList<int>(1024, Allocator.Persistent);
            _cols = new NativeList<Color32>(1024, Allocator.Persistent);
            _uvs = new NativeList<Vector2>(1024, Allocator.Persistent);
            _uvs1 = new NativeList<Vector2>(1024, Allocator.Persistent);

            _isInitialized = true;
        }

        /// <summary>
        /// 初始化全部体素为空气
        /// </summary>
        public void InitVoxels()
        {
            for (int i = 0; i < VoxelCount; i++)
                _voxelsManaged[i] = Voxel.Air;
            _dirtyData = true;
        }

        /// <summary>
        /// 批量设置体素，用于初始化
        /// </summary>
        public void SetVoxels(Voxel[] voxels)
        {
            if (voxels.Length != VoxelCount)
            {
                Debug.LogError($"[SubChunk] SetVoxels: array length mismatch, expected {VoxelCount}, got {voxels.Length}");
                return;
            }
            System.Array.Copy(voxels, _voxelsManaged, VoxelCount);
            _dirtyData = true;
        }

        /// <summary>
        /// 标记所有数据为脏，需要重建网格
        /// </summary>
        public void MarkDirtyAll() => _dirtyData = true;

        /// <summary>
        /// 标记网格需要重建
        /// </summary>
        public void MarkDirty()
        {
            _dirtyData = true;
        }

        private void OnDestroy()
        {
            _meshHandle.Complete();
            _voxelsNative.Dispose();
            if (_verts.IsCreated) _verts.Dispose();
            if (_tris.IsCreated) _tris.Dispose();
            if (_cols.IsCreated) _cols.Dispose();
            if (_uvs.IsCreated) _uvs.Dispose();
            if (_uvs1.IsCreated) _uvs1.Dispose();
            if (_palette.IsCreated) _palette.Dispose();
            if (_slices.IsCreated) _slices.Dispose();
            if (_faceSlices.IsCreated) _faceSlices.Dispose();

            // 释放边界缓存
            if (_borderPX.IsCreated) _borderPX.Dispose();
            if (_borderNX.IsCreated) _borderNX.Dispose();
            if (_borderPY.IsCreated) _borderPY.Dispose();
            if (_borderNY.IsCreated) _borderNY.Dispose();
            if (_borderPZ.IsCreated) _borderPZ.Dispose();
            if (_borderNZ.IsCreated) _borderNZ.Dispose();
        }

        void OnEnable() => VoxelRegistry.OnRegistryChanged += MarkDirtyAll;
        void OnDisable() => VoxelRegistry.OnRegistryChanged -= MarkDirtyAll;

        private void Update()
        {
            // 等待初始化完成
            if (!_isInitialized) return;

            // 若有 Job 正在跑，轮询是否结束
            if (jobRunning)
            {
                if (_meshHandle.IsCompleted)
                {
                    _meshHandle.Complete();     // ← 保证安全读取
                    ApplyMesh(); // 把 verts/tris/colors 写进 mesh

                    // 释放所有Native数组
                    if (_borderPX.IsCreated) _borderPX.Dispose();
                    if (_borderNX.IsCreated) _borderNX.Dispose();
                    if (_borderPY.IsCreated) _borderPY.Dispose();
                    if (_borderNY.IsCreated) _borderNY.Dispose();
                    if (_borderPZ.IsCreated) _borderPZ.Dispose();
                    if (_borderNZ.IsCreated) _borderNZ.Dispose();
                    if (_palette.IsCreated) _palette.Dispose();
                    if (_slices.IsCreated) _slices.Dispose();
                    if (_faceSlices.IsCreated) _faceSlices.Dispose();

                    // Dispose color override arrays
                    if (_colorOverrides.IsCreated) _colorOverrides.Dispose();
                    if (_hasColorOverride.IsCreated) _hasColorOverride.Dispose();

                    jobRunning = false;
                }
            }

            // 若数据脏 & 没有 Job 在跑，启动新 Job
            if (_dirtyData && !jobRunning)
            {
                _voxelsNative.CopyFrom(_voxelsManaged);
                ScheduleMeshJob();
                jobRunning = true;
                _dirtyData = false;
            }
        }

        #region Public API
        public void SetVoxel(Vector3Int local, Voxel voxel)
        {
            int i = Index(local);
            if (_voxelsManaged[i] == voxel) return;
            _voxelsManaged[i] = voxel;
            _dirtyData = true;
            //Debug.Log($"[SubChunk {name}] 设置位置 {local} 的体素为 ID:{voxel.TypeId}, 是否为空气:{voxel.IsAir}");
        }
        public Voxel GetVoxel(Vector3Int local) => _voxelsManaged[Index(local)];
        #endregion

        #region Job Scheduling / Mesh Application
        private void ScheduleMeshJob()
        {
            _verts.Clear();
            _tris.Clear();
            _cols.Clear();
            _uvs.Clear();
            _uvs1.Clear();

            int NN = Size * Size;

            // 释放旧的边界缓存数组（如果存在）
            if (_borderPX.IsCreated) _borderPX.Dispose();
            if (_borderNX.IsCreated) _borderNX.Dispose();
            if (_borderPY.IsCreated) _borderPY.Dispose();
            if (_borderNY.IsCreated) _borderNY.Dispose();
            if (_borderPZ.IsCreated) _borderPZ.Dispose();
            if (_borderNZ.IsCreated) _borderNZ.Dispose();

            // 初始化边界缓存数组
            _borderPX = new NativeArray<ushort>(NN, Allocator.Persistent);
            _borderNX = new NativeArray<ushort>(NN, Allocator.Persistent);
            _borderPY = new NativeArray<ushort>(NN, Allocator.Persistent);
            _borderNY = new NativeArray<ushort>(NN, Allocator.Persistent);
            _borderPZ = new NativeArray<ushort>(NN, Allocator.Persistent);
            _borderNZ = new NativeArray<ushort>(NN, Allocator.Persistent);

            // Build palette
            int count = VoxelRegistry.Count;
            
            // 释放旧的palette和slices数组（如果存在）
            if (_palette.IsCreated) _palette.Dispose();
            if (_slices.IsCreated) _slices.Dispose();
            
            _palette = new NativeArray<Color32>(count, Allocator.Persistent);
            _slices = new NativeArray<float>(count, Allocator.Persistent);

            // 计算最大的typeId，用于确定面贴图数组大小
            int maxTypeId = 0;
            for (ushort i = 0; i < count; i++)
            {
                var def = VoxelRegistry.GetDefinition(i);
                if (def != null && def.typeId > maxTypeId)
                    maxTypeId = def.typeId;
            }

            // 释放旧的面贴图索引数组（如果存在）
            if (_faceSlices.IsCreated) _faceSlices.Dispose();
            
            // 创建面贴图索引数组
            _faceSlices = new NativeArray<float>((maxTypeId + 1) * 6, Allocator.Persistent);

            // 释放旧的颜色覆盖数组（如果存在）
            if (_colorOverrides.IsCreated) _colorOverrides.Dispose();
            if (_hasColorOverride.IsCreated) _hasColorOverride.Dispose();
            
            // 创建颜色覆盖数组
            var colorOverrides = new NativeArray<Color32>(VoxelCount, Allocator.Persistent);
            var hasColorOverride = new NativeArray<bool>(VoxelCount, Allocator.Persistent);

            // 填充颜色覆盖数据
            for (int x = 0; x < Size; x++)
            {
                for (int y = 0; y < Size; y++)
                {
                    for (int z = 0; z < Size; z++)
                    {
                        int index = Index(x, y, z);
                        Vector3Int worldPos = LocalToWorldVoxel(new Vector3Int(x, y, z));

                        // 检查是否有颜色覆盖
                        if (VoxelColorOverride.Instance.HasColorOverride(worldPos))
                        {
                            var defaultColor = VoxelRegistry.GetDefinition(_voxelsManaged[index].TypeId)?.baseColor ?? Color.white;
                            colorOverrides[index] = VoxelColorOverride.Instance.GetVoxelColor(worldPos, defaultColor);
                            hasColorOverride[index] = true;
                        }
                    }
                }
            }

            // 填充面贴图索引和基础数据
            for (ushort i = 0; i < count; i++)
            {
                var def = VoxelRegistry.GetDefinition(i);
                _palette[i] = def ? def.baseColor : new Color32(255, 255, 255, 255);
                _slices[i] = def ? (def.faceTextures[0] != null ? def.faceTextures[0].sliceIndex : 0f) : 0f;

                if (def != null)
                {
                    // 填充每个面的贴图索引
                    for (int face = 0; face < 6; face++)
                    {
                        int sliceIndex = def.GetFaceTextureSliceIndex(face);
                        _faceSlices[def.typeId * 6 + face] = sliceIndex;
                    }
                }
            }

            var job = new GreedyMeshJob
            {
                voxels = _voxelsNative,
                palette = _palette,
                slices = _slices,
                faceSlices = _faceSlices,
                maxTypeId = maxTypeId,
                vertices = _verts,
                triangles = _tris,
                colors = _cols,
                uvs = _uvs,
                uvs1 = _uvs1,

                // 传入边界缓存
                borderPX = _borderPX,
                borderNX = _borderNX,
                borderPY = _borderPY,
                borderNY = _borderNY,
                borderPZ = _borderPZ,
                borderNZ = _borderNZ,

                // 传入颜色覆盖数据
                colorOverrides = colorOverrides,
                hasColorOverride = hasColorOverride
            };

            _meshHandle = job.Schedule();

            // Store the arrays for later disposal
            _colorOverrides = colorOverrides;
            _hasColorOverride = hasColorOverride;
        }

        private NativeArray<Color32> _colorOverrides;
        private NativeArray<bool> _hasColorOverride;

        private void ApplyMesh()
        {
            // Add validation checks
            if (_verts.Length == 0)
            {
                //Debug.LogWarning($"[SubChunk {name}] No vertices generated in mesh job");
                return;
            }

            var mesh = _filter.sharedMesh ?? new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.Clear();
            mesh.SetVertices(_verts.AsArray());
            mesh.SetTriangles(new List<int>(_tris.AsArray()), 0);
            mesh.SetColors(_cols.AsArray());
            mesh.SetUVs(0, _uvs.AsArray());     // UV0 : block atlas
            mesh.SetUVs(1, _uvs1.AsArray());    // UV1 : slice index
            mesh.RecalculateNormals();
            
            // Add validation before setting collider mesh
            if (mesh.vertexCount == 0)
            {
                //Debug.LogWarning($"[SubChunk {name}] Mesh has no vertices after generation");
                return;
            }
            
            _filter.sharedMesh = mesh;
            _collider.sharedMesh = mesh;   // Mesh 更新同时刷新 Collider

        }
        #endregion

        #region Utility
        private static int Index(Vector3Int p) => p.x + Size * (p.y + Size * p.z);
        private static int Index(int x, int y, int z) => x + Size * (y + Size * z);

        // 世界坐标 → 局部坐标
        public Vector3Int WorldToLocal(Vector3 worldPos) => Vector3Int.FloorToInt(transform.InverseTransformPoint(worldPos));
        // 局部坐标 → 世界坐标
        public Vector3Int LocalToWorldVoxel(Vector3Int local) => Vector3Int.RoundToInt(transform.position) + local;

        #endregion
    }
}