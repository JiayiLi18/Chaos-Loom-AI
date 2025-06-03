using UnityEngine;
using System.Collections.Generic;

namespace Voxels
{
    /// <summary>
    /// 管理单个体素的颜色覆盖
    /// </summary>
    public class VoxelColorOverride : MonoBehaviour
    {
        private static VoxelColorOverride _instance;
        public static VoxelColorOverride Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("VoxelColorOverride");
                    _instance = go.AddComponent<VoxelColorOverride>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // 使用字典存储被覆盖颜色的体素
        // Key: 体素的世界坐标的字符串表示 "x,y,z"
        // Value: 覆盖的颜色
        private Dictionary<string, Color32> _colorOverrides = new Dictionary<string, Color32>();

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        // 获取体素的实际颜色
        public Color32 GetVoxelColor(Vector3Int position, Color32 defaultColor)
        {
            string key = GetPositionKey(position);
            return _colorOverrides.TryGetValue(key, out Color32 color) ? color : defaultColor;
        }

        // 设置体素的覆盖颜色
        public void SetVoxelColor(Vector3Int position, Color32 color)
        {
            string key = GetPositionKey(position);
            _colorOverrides[key] = color;
            
            // 通知Chunk更新
            // 获取包含此体素的SubChunk并触发更新
            WorldGrid world = FindAnyObjectByType<WorldGrid>();
            if (world != null)
            {
                SubChunk chunk = world.GetChunkAt(position);
                if (chunk != null)
                {
                    chunk.MarkDirty(); // 需要在SubChunk中添加此方法
                }
            }
        }

        // 清除体素的覆盖颜色
        public void ClearVoxelColor(Vector3Int position)
        {
            string key = GetPositionKey(position);
            if (_colorOverrides.Remove(key))
            {
                // 通知Chunk更新
                WorldGrid world = FindAnyObjectByType<WorldGrid>();
                if (world != null)
                {
                    SubChunk chunk = world.GetChunkAt(position);
                    if (chunk != null)
                    {
                        chunk.MarkDirty();
                    }
                }
            }
        }

        // 检查体素是否有覆盖颜色
        public bool HasColorOverride(Vector3Int position)
        {
            string key = GetPositionKey(position);
            return _colorOverrides.ContainsKey(key);
        }

        // 清除所有颜色覆盖
        public void ClearAllOverrides()
        {
            _colorOverrides.Clear();
            // 可能需要刷新所有Chunk
        }

        private string GetPositionKey(Vector3Int position)
        {
            return $"{position.x},{position.y},{position.z}";
        }
    }
}
