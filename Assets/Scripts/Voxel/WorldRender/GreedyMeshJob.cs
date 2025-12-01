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
        [ReadOnly] public NativeArray<float> slices;   // typeId → texture slice index
        
        // 新增：每个typeId的每个面的贴图索引 [typeId][faceIndex]
        [ReadOnly] public NativeArray<float> faceSlices; // length = typeId * 6
        [ReadOnly] public int maxTypeId; // 最大的typeId值，用于计算faceSlices的索引

        // 边界缓存数组
        [ReadOnly] public NativeArray<ushort> borderPX, borderNX;
        [ReadOnly] public NativeArray<ushort> borderPY, borderNY;
        [ReadOnly] public NativeArray<ushort> borderPZ, borderNZ;

        // 颜色覆盖数据
        [ReadOnly] public NativeArray<Color32> colorOverrides;  // 体素的覆盖颜色
        [ReadOnly] public NativeArray<bool> hasColorOverride;   // 是否有颜色覆盖

        // ────────── Outputs ─────────
        public NativeList<float3> vertices;
        public NativeList<int> triangles;
        public NativeList<Color32> colors;
        public NativeList<Vector2> uvs;   // UV0 (tile‑rect)
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
                ProcessDirection(mask, axis, isPositive, s_Normals[face], s_Right[face], s_Up[face], face);
            }

            mask.Dispose();
        }

        // ─────────────────────────────────────────────────────────────
        private void ProcessDirection(NativeArray<ushort> mask, int axis, bool positive,
                                       float3 normal, float3 right, float3 up, int faceIndex)
        {
            int N = SubChunk.Size;
            int uAxis = (axis + 1) % 3;
            int vAxis = (axis + 2) % 3;

            // 创建颜色掩码数组
            var colorMask = new NativeArray<Color32>(N * N, Allocator.Temp);
            var hasOverrideMask = new NativeArray<bool>(N * N, Allocator.Temp);

            // iterate slices along the normal
            for (int d = 0; d < N; ++d)
            {
                // 1. clear mask
                for (int i = 0; i < mask.Length; i++)
                {
                    mask[i] = 0;
                    colorMask[i] = Color.white;
                    hasOverrideMask[i] = false;
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
                        bool neighbourSolid;
                        int nIdx = Flatten(nPos.x, nPos.y, nPos.z);

                        if (nIdx >= 0)                 // 仍在本 subchunk 内
                        {
                            neighbourSolid = !voxels[nIdx].IsAir;
                        }
                        else                           // 落到边界外 → 查邻接缓存
                        {
                            int uCoord = pos[uAxis];   // 重命名避免冲突
                            int vCoord = pos[vAxis];

                            switch (axis)          // X=0 Y=1 Z=2
                            {
                                case 0:
                                    neighbourSolid = positive
                                        ? borderPX[vCoord * SubChunk.Size + uCoord] != 0
                                        : borderNX[vCoord * SubChunk.Size + uCoord] != 0;
                                    break;
                                case 1:
                                    neighbourSolid = positive
                                        ? borderPY[vCoord * SubChunk.Size + uCoord] != 0
                                        : borderNY[vCoord * SubChunk.Size + uCoord] != 0;
                                    break;
                                default:
                                    neighbourSolid = positive
                                        ? borderPZ[vCoord * SubChunk.Size + uCoord] != 0
                                        : borderNZ[vCoord * SubChunk.Size + uCoord] != 0;
                                    break;
                            }
                        }

                        if (!neighbourSolid)
                        {
                            int maskIndex = v * N + u;
                            mask[maskIndex] = typeId;        // expose face
                            
                            // 存储颜色覆盖信息
                            if (hasColorOverride[idx])
                            {
                                colorMask[maskIndex] = colorOverrides[idx];
                                hasOverrideMask[maskIndex] = true;
                            }
                        }
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
                GreedyMaskToMesh(mask, colorMask, hasOverrideMask, N, N, origin, normal, right, up, positive, faceIndex);
            }

            colorMask.Dispose();
            hasOverrideMask.Dispose();
        }

        // ─────────────────────────────────────────────────────────────
        private void GreedyMaskToMesh(NativeArray<ushort> m, NativeArray<Color32> colorMask, NativeArray<bool> hasOverrideMask,
                                    int W, int H, float3 origin, float3 normal, float3 right, float3 up, bool positive, int faceIndex)
        {
            for (int i = 0; i < m.Length;)
            {
                ushort id = m[i];
                if (id == 0) { ++i; continue; }

                // 检查颜色是否相同
                Color32 overrideColor = colorMask[i];
                bool hasOverride = hasOverrideMask[i];

                // determine quad dims
                int w = 1;
                while (w + i % W < W && m[i + w] == id && 
                      // 如果当前方块有颜色覆盖，不进行合并
                      !hasOverride &&
                      // 如果下一个方块有颜色覆盖，也不进行合并
                      !hasOverrideMask[i + w])
                    w++;
                
                int h = 1;
                while ((i / W) + h < H)
                {
                    bool same = true;
                    for (int k = 0; k < w; ++k)
                    {
                        int index = i + k + h * W;
                        if (m[index] != id || 
                            // 如果当前方块有颜色覆盖，不进行合并
                            hasOverride ||
                            // 如果下一行的方块有颜色覆盖，也不进行合并
                            hasOverrideMask[index])
                        { 
                            same = false; 
                            break; 
                        }
                    }
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
                    vertices.Add(bl); vertices.Add(br); vertices.Add(tr); vertices.Add(tl);
                    triangles.Add(vStart); triangles.Add(vStart + 1); triangles.Add(vStart + 2);
                    triangles.Add(vStart); triangles.Add(vStart + 2); triangles.Add(vStart + 3);
                }
                else
                {
                    vertices.Add(bl); vertices.Add(br); vertices.Add(tr); vertices.Add(tl);
                    triangles.Add(vStart); triangles.Add(vStart + 2); triangles.Add(vStart + 1);
                    triangles.Add(vStart); triangles.Add(vStart + 3); triangles.Add(vStart + 2);
                }

                // colour & UVs
                Color32 c = hasOverride ? overrideColor : 
                           (id < palette.Length ? palette[id] : Color.white);
                
                for (int k = 0; k < 4; ++k) colors.Add(c);

                // UV0 generation (block atlas UVs)
                // ==== 统一的 UV 生成 + 按面修正（只改 UV，不动几何） ====
                // 基础矩形（mask 空间，x 向右，y 向上）
                int X0 = x0, Y0 = y0, Ww = w, Hh = h;

                int rotSteps = 0;   // 0,1,2,3 分别表示 0°/90°/180°/270°（逆时针）
                bool mirrorU = false;

                switch (faceIndex)
                {
                    case 0: // +X
                        mirrorU = true; rotSteps = 1;
                        break;
                    case 1: // -X
                        rotSteps = 3;
                        break;
                    case 2: // +Y
                        mirrorU = true; rotSteps = 1;   // 镜像 + 90°
                        break;
                    case 3: // -Y
                        mirrorU = false; rotSteps = 1;  // 90°
                        break;
                    case 4: // +Z
                        mirrorU = true; // 镜像 
                        break;
                    case 5: // -Z
                        break;
                }

                // 2) 从局部四角(0..Ww, 0..Hh)出发做几何变换，最后偏移到 (X0,Y0)
                float2 nbl = new float2(0, 0);
                float2 nbr = new float2(Ww, 0);
                float2 ntr = new float2(Ww, Hh);
                float2 ntl = new float2(0, Hh);

                // 水平镜像（U 反转）：u -> (Ww - u)
                if (mirrorU)
                {
                    nbl.x = Ww - nbl.x;  nbr.x = Ww - nbr.x;
                    ntr.x = Ww - ntr.x;  ntl.x = Ww - ntl.x;
                }

                // 旋转：每转 90°逆时针：(u,v)->(v, Ww - u)，并交换当前宽高
                for (int ns = 0; ns < rotSteps; ns++)
                {
                    float2 rbl = new float2(nbl.y, Ww - nbl.x);
                    float2 rbr = new float2(nbr.y, Ww - nbr.x);
                    float2 rtr = new float2(ntr.y, Ww - ntr.x);
                    float2 rtl = new float2(ntl.y, Ww - ntl.x);
                    nbl = rbl; nbr = rbr; ntr = rtr; ntl = rtl;
                    int t = Ww; Ww = Hh; Hh = t; // 宽高交换
                }

                // 偏移到 atlas 坐标
                Vector2 uvBL = new Vector2(X0 + nbl.x, Y0 + nbl.y);
                Vector2 uvBR = new Vector2(X0 + nbr.x, Y0 + nbr.y);
                Vector2 uvTR = new Vector2(X0 + ntr.x, Y0 + ntr.y);
                Vector2 uvTL = new Vector2(X0 + ntl.x, Y0 + ntl.y);

                // 写入（顺序必须与顶点写入一致：bl, br, tr, tl）
                uvs.Add(uvBL);
                uvs.Add(uvBR);
                uvs.Add(uvTR);
                uvs.Add(uvTL);


                // UV1.x = slice index (same per‑quad)
                float slice = 0f;
                
                if (id > 0 && id <= maxTypeId && faceSlices.Length >= (id * 6 + faceIndex))
                {
                    slice = faceSlices[id * 6 + faceIndex];
                }
                else if (id < slices.Length)
                {
                    slice = slices[id];
                }
                
                Vector2 s = new Vector2(slice, 0);
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
