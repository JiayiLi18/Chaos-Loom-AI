using UnityEngine;

public static class VoxelRayUtil
{
    // 命中点 p 与法线 n（自然数 ±1 轴向），取“内部”体素
    public static Vector3Int HitToVoxel(RemoveOrPlace mode, Vector3 p, Vector3 n)
    {
        const float eps = 0.0001f;
        p += n * (mode == RemoveOrPlace.Place ? eps : -eps);
        return new Vector3Int(
            Mathf.FloorToInt(p.x),
            Mathf.FloorToInt(p.y),
            Mathf.FloorToInt(p.z));
    }

    public enum RemoveOrPlace { Remove, Place }
}
