using UnityEngine;
using Voxels;

public class PlayerVoxelTool : MonoBehaviour
{
    public LayerMask voxelLayer;           // 只勾 SubChunk 所在 layer
    public byte selectedType = 1;     // 右键放置的方块 ID
    Vector3Int _hover = new(-999, -999, -999);

    Camera _cam;
    WorldGrid _world;

    void Awake()
    {
        _cam = Camera.main;
        _world = FindAnyObjectByType<WorldGrid>();
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1)) return;

        if (Physics.Raycast(_cam.ScreenPointToRay(Input.mousePosition),
                            out var hit, 20f, voxelLayer))
        {
            bool isLeft = Input.GetMouseButtonDown(0);
            var mode = isLeft ? VoxelRayUtil.RemoveOrPlace.Remove
                                 : VoxelRayUtil.RemoveOrPlace.Place;

            Vector3Int coord = VoxelRayUtil.HitToVoxel(mode, hit.point, hit.normal);
            if (_world != null)
            {
                if (isLeft)
                {
                    _world.SetVoxelWorld(coord, Voxel.Air);           // 挖掉
                }
                else
                {
                    _world.SetVoxelWorld(coord, new Voxel(selectedType)); // 放置
                }
            }
            else
                Debug.LogError("WorldGrid not found!");

            if (hit.collider)
                _hover = VoxelRayUtil.HitToVoxel(VoxelRayUtil.RemoveOrPlace.Remove,
                                                 hit.point, hit.normal);
        }
    }

    void OnDrawGizmos()
    {
        if (_hover.x == -999) return;
        Gizmos.color = new Color(1, 1, 1, 0.5f);
        Gizmos.DrawWireCube(_hover + Vector3.one * 0.5f, Vector3.one);
    }
}
