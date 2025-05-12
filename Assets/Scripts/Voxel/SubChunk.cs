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
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SubChunk : MonoBehaviour
    {
        #region Constants / Data
        public const int Size = 100;
        private const int VoxelCount = Size * Size * Size;

        // CPU copy for editable voxels
        [SerializeField, HideInInspector] private Voxel[] _voxelsManaged = new Voxel[VoxelCount];//仅主线程写
        // Native copy fed to jobs
        private NativeArray<Voxel> _voxelsNative;//job只读

        private bool _dirtyData = true; // voxel array changed, need to copy to native
        private JobHandle _meshHandle;
        private bool jobRunning = false;

        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private MeshCollider _collider;

        // Job output containers (reused)
        private NativeList<float3> _verts;
        private NativeList<int> _tris;
        private NativeList<Color32> _cols;
        private NativeList<Vector2> _uvs;
        private NativeList<Vector2> _uvs1;

        private NativeArray<Color32> _palette;//baseColor
        private NativeArray<int> _texIndex;//texture index
        #endregion

        private void Awake()
        {
            _voxelsNative = new NativeArray<Voxel>(VoxelCount, Allocator.Persistent);
            _verts = new NativeList<float3>(Allocator.Persistent);
            _tris = new NativeList<int>(Allocator.Persistent);
            _cols = new NativeList<Color32>(Allocator.Persistent);
            _uvs = new NativeList<Vector2>(Allocator.Persistent);
            _uvs1 = new NativeList<Vector2>(Allocator.Persistent);
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _collider = gameObject.AddComponent<MeshCollider>();
            _collider.sharedMesh = null;   // 初始为空

            EnsureMaterial();
            _dirtyData = true; // first build
        }

        private void OnDestroy()
        {
            _meshHandle.Complete();
            _voxelsNative.Dispose();
            if (_verts.IsCreated) _verts.Dispose();
            if (_tris.IsCreated) _tris.Dispose();
            if (_cols.IsCreated) _cols.Dispose();
            if (_palette.IsCreated) _palette.Dispose();
            if (_uvs.IsCreated) _uvs.Dispose();
            if (_uvs1.IsCreated) _uvs1.Dispose();
            if (_texIndex.IsCreated) _texIndex.Dispose();
        }

        void OnEnable() => VoxelRegistry.OnRegistryChanged += MarkDirtyAll;
        void OnDisable() => VoxelRegistry.OnRegistryChanged -= MarkDirtyAll;

        private void Update()
        {
            // 若有 Job 正在跑，轮询是否结束
            if (jobRunning && _meshHandle.IsCompleted)
            {
                _meshHandle.Complete();     // ← 保证安全读取
                ApplyMesh(); // 把 verts/tris/colors 写进 mesh
                jobRunning = false;
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

            // Build palette (TempJob lifetime → auto‑dispose after job)
            int count = VoxelRegistry.Count;
            _palette = new NativeArray<Color32>(count, Allocator.Persistent);
            _texIndex = new NativeArray<int>(count, Allocator.Persistent);
            for (ushort i = 0; i < count; i++)
            {
                var def = VoxelRegistry.GetDefinition(i);
                _palette[i] = def ? def.baseColor : new Color32(255, 255, 255, 255);
                _texIndex[i] = def ? def.textureIndex : 0;
            }

            var job = new GreedyMeshJob
            {
                voxels = _voxelsNative,
                palette = _palette,
                texIndex = _texIndex,
                vertices = _verts,
                triangles = _tris,
                colors = _cols,
                uvs = _uvs,
                uvs1 = _uvs1
            };

            _meshHandle = job.Schedule();
        }

        private void ApplyMesh()
        {
            var mesh = _filter.sharedMesh ?? new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.Clear();
            mesh.SetVertices(_verts.AsArray());
            mesh.SetTriangles(new List<int>(_tris.AsArray()), 0);
            mesh.SetColors(_cols.AsArray());
            mesh.SetUVs(0, _uvs.AsArray());     // UV0
            mesh.SetUVs(1, _uvs1.AsArray());   // UV1
            mesh.RecalculateNormals();
            _filter.sharedMesh = mesh;
            _collider.sharedMesh = mesh;   // Mesh 更新同时刷新 Collider

            // Job 用完才能安全 Dispose
            _palette.Dispose();
            _texIndex.Dispose();
        }
        #endregion

        #region Helpers
        private static int Index(int x, int y, int z) => x + Size * (y + Size * z);
        private static int Index(Vector3Int p) => p.x + Size * (p.y + Size * p.z);
        //private static bool IsInside(Vector3Int p) => (uint)p.x < Size && (uint)p.y < Size && (uint)p.z < Size;

        private void EnsureMaterial()
        {
            if (_renderer.sharedMaterial == null)
                _renderer.sharedMaterial = VoxelResources.DefaultMaterial;
        }

        void MarkDirtyAll()  // 仅在新增方块时调用
        {
            _dirtyData = true;               // 本地标脏
            //WorldGrid.MarkNeighborsDirty(this); // 如果有跨块边缘需要重建
        }

        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.one * (Size * .5f), Vector3.one * Size);
        }
#endif
    }
}