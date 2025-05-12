using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Voxels
{
    /// <summary>
    /// Correct, robust greedy‑meshing job for a fixed‑size SubChunk.
    /// ‑ Merges coplanar quads per slice & direction.
    /// ‑ Writes sliceIndex in UV1.x for ShaderGraph sampling.
    /// </summary>
    [BurstCompile]
    public struct GreedyMeshJob : IJob
    {
        // ────────── Inputs ──────────
        [ReadOnly] public NativeArray<Voxel> voxels;   // length = SubChunk.Size³
        [ReadOnly] public NativeArray<Color32> palette;  // typeId → base colour
        [ReadOnly] public NativeArray<int> texIndex;   // typeId → texture index

        // ────────── Outputs ─────────
        public NativeList<float3> vertices;
        public NativeList<int> triangles;
        public NativeList<Color32> colors;
        public NativeList<Vector2> uvs;   // UV0 (tile‑rect, kept 0‑1 full‑quad)
        public NativeList<Vector2> uvs1;  // UV1 (x = sliceIndex)

        // Local helpers
        private static readonly float3[] s_Normals =
        {
            new float3( 1, 0, 0), // +X 0
            new float3(-1, 0, 0), // -X 1
            new float3( 0, 1, 0), // +Y 2
            new float3( 0,-1, 0), // -Y 3
            new float3( 0, 0, 1), // +Z 4
            new float3( 0, 0,-1)  // -Z 5
        };

        private static readonly float3[] s_Right =
        {
            new float3(0,1,0), new float3(0,1,0), // ±X : right = +Y
            new float3(0,0,1), new float3(0,0,1), // ±Y : right = +Z
            new float3(1,0,0), new float3(1,0,0)  // ±Z : right = +X
        };

        private static readonly float3[] s_Up =
        {
            new float3(0,0,1), new float3(0,0,1), // ±X :  up   = +Z
            new float3(1,0,0), new float3(1,0,0), // ±Y :  up   = +X
            new float3(0,1,0), new float3(0,1,0)  // ±Z :  up   = +Y
        };

        public void Execute()
        {
            int N = SubChunk.Size;
            var mask = new NativeArray<ushort>(N * N, Allocator.Temp);

            // Six face directions
            for (int face = 0; face < 6; ++face)
            {
                bool isPositive = face % 2 == 0;          // even indices are +, odd are -
                int axis = face / 2;               // 0:X 1:Y 2:Z
                ProcessDirection(mask, axis, isPositive, s_Normals[face], s_Right[face], s_Up[face]);
            }

            mask.Dispose();
        }

        // ─────────────────────────────────────────────────────────────
        private void ProcessDirection(NativeArray<ushort> mask, int axis, bool positive,
                                       float3 normal, float3 right, float3 up)
        {
            int N = SubChunk.Size;
            int uAxis = (axis + 1) % 3;
            int vAxis = (axis + 2) % 3;

            // iterate slices along the normal
            for (int d = 0; d < N; ++d)
            {
                // 1. clear mask
                for (int i = 0; i < mask.Length; i++)
                {
                    mask[i] = 0;
                }

                // 2. build exposure mask for this slice
                for (int v = 0; v < N; ++v)
                {
                    for (int u = 0; u < N; ++u)
                    {
                        int3 pos = new int3();
                        pos[axis] = d;
                        pos[uAxis] = u;
                        pos[vAxis] = v;

                        int idx = Flatten(pos.x, pos.y, pos.z);
                        if (idx < 0) continue;
                        ushort typeId = voxels[idx].TypeId;
                        if (typeId == 0) continue;          // air no face

                        // neighbour position one step towards normal
                        int3 nPos = pos; nPos[axis] += positive ? 1 : -1;
                        int nIdx = Flatten(nPos.x, nPos.y, nPos.z);
                        bool neighbourSolid = (nIdx >= 0) && !voxels[nIdx].IsAir;

                        if (!neighbourSolid)
                            mask[v * N + u] = typeId;        // expose face
                    }
                }

                // 3. greedy merge mask & emit quads
                float3 origin = float3.zero;
                if (positive)
                {
                    origin[axis] = d + 1;
                }
                else
                {
                    origin[axis] = d;
                }
                GreedyMaskToMesh(mask, N, N, origin, normal, right, up, positive);
            }
        }

        // ─────────────────────────────────────────────────────────────
        private void GreedyMaskToMesh(NativeArray<ushort> m, int W, int H, float3 origin,
                                       float3 normal, float3 right, float3 up, bool positive)
        {
            for (int i = 0; i < m.Length;)
            {
                ushort id = m[i];
                if (id == 0) { ++i; continue; }

                // determine quad dims
                int w = 1;
                while (w + i % W < W && m[i + w] == id) w++;
                int h = 1;
                while ((i / W) + h < H)
                {
                    bool same = true;
                    for (int k = 0; k < w; ++k)
                        if (m[i + k + h * W] != id) { same = false; break; }
                    if (!same) break;
                    h++;
                }

                // quad origin (lower‑left in mask space)
                int x0 = i % W;
                int y0 = i / W;

                // build 4 verts (counter‑clockwise for +normal, reversed for -normal)
                float3 bl = origin + right * x0 + up * y0;       // bottom‑left
                float3 tl = origin + right * x0 + up * (y0 + h);   // top‑left
                float3 tr = origin + right * (x0 + w) + up * (y0 + h);    // top‑right
                float3 br = origin + right * (x0 + w) + up * y0;        // bottom‑right

                int vStart = vertices.Length;
                if (positive)
                {
                    // ★ 顺时针：BL → BR → TR → TL
                    vertices.Add(bl); vertices.Add(br); vertices.Add(tr); vertices.Add(tl);

                    // 顺时针三角
                    triangles.Add(vStart); triangles.Add(vStart + 1); triangles.Add(vStart + 2);
                    triangles.Add(vStart); triangles.Add(vStart + 2); triangles.Add(vStart + 3);
                }
                else // reverse winding for back face so normal points outward
                {
                    // 仍可用同一顺序，再反三角即可
                    vertices.Add(bl); vertices.Add(br); vertices.Add(tr); vertices.Add(tl);

                    // 反向三角：0‑2‑1  & 0‑3‑2
                    triangles.Add(vStart); triangles.Add(vStart + 2); triangles.Add(vStart + 1);
                    triangles.Add(vStart); triangles.Add(vStart + 3); triangles.Add(vStart + 2);
                }


                // colour & UVs
                Color32 c = Color.white; // 默认颜色
                if (id < palette.Length)
                {
                    c = palette[id];
                }
                for (int k = 0; k < 4; ++k) colors.Add(c);

                // ---- 生成按 1m/像素 平铺的 UV0 -------------------------
                uvs.Add(new Vector2(x0, y0));        // BL
                uvs.Add(new Vector2(x0 + w, y0));        // BR
                uvs.Add(new Vector2(x0 + w, y0 + h));    // TR
                uvs.Add(new Vector2(x0, y0 + h));    // TL


                // UV1.x = slice index (same per‑quad)
                int index = 0; // 默认值
                if (id < texIndex.Length)
                {
                    index = texIndex[id];
                }
                Vector2 s = new Vector2(index, 0);
                for (int k = 0; k < 4; ++k) uvs1.Add(s);

                // zero out consumed mask area
                for (int dy = 0; dy < h; ++dy)
                    for (int dx = 0; dx < w; ++dx)
                        m[i + dx + dy * W] = 0;

                while (i < m.Length && m[i] == 0) ++i;
            }
        }

        // flatten 3D index, returns -1 if outside chunk
        private static int Flatten(int x, int y, int z)
        {
            int N = SubChunk.Size;
            return (x >= 0 && x < N && y >= 0 && y < N && z >= 0 && z < N) ? x + N * (y + N * z) : -1;
        }
    }
}
