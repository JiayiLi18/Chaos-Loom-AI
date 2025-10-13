using UnityEngine;
using System;

namespace Voxels
{
    /// <summary>
    /// 负责管理体素预览框的显示,即HoverBox和PlaceBox
    /// </summary>
    public class VoxelPreviewManager : MonoBehaviour
    {
        [Header("Preview Settings")]
        [SerializeField] private float lineWidth = 0.01f;
        
        private LineRenderer _hoverRenderer;
        private LineRenderer _placeRenderer;
        private bool _isEnabled = true;

        // 预览状态
        private Vector3Int _previewPosition;
        private Vector3Int _previewNormal;
        private Color _hoverColor = Color.white;
        private Color _placeColor = Color.green;
        private bool _showPlacePreview = true;

        private void Start()
        {
            InitializeLineRenderers();
        }

        private void InitializeLineRenderers()
        {
            GameObject hoverObj = new GameObject("HoverBox");
            hoverObj.transform.parent = transform;
            _hoverRenderer = hoverObj.AddComponent<LineRenderer>();
            SetupLineRenderer(_hoverRenderer);

            GameObject placeObj = new GameObject("PlaceBox");
            placeObj.transform.parent = transform;
            _placeRenderer = placeObj.AddComponent<LineRenderer>();
            SetupLineRenderer(_placeRenderer);
        }

        private void SetupLineRenderer(LineRenderer renderer)
        {
            renderer.useWorldSpace = true;
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.startWidth = lineWidth;
            renderer.endWidth = lineWidth;
            renderer.positionCount = 24;
            renderer.loop = false;
        }

        /// <summary>
        /// 更新预览框的显示
        /// </summary>
        /// <param name="position">预览位置（体素坐标）</param>
        /// <param name="normal">预览方向</param>
        /// <param name="hoverColor">悬停预览框颜色</param>
        /// <param name="showPlacePreview">是否显示放置预览框</param>
        /// <param name="placeColor">放置预览框颜色，如果不指定则使用默认的绿色</param>
        public void UpdatePreview(Vector3Int position, Vector3Int normal, Color hoverColor, bool showPlacePreview = true, Color? placeColor = null)
        {
            if (!_isEnabled) return;

            _previewPosition = position;
            _previewNormal = normal;
            _hoverColor = hoverColor;
            _placeColor = placeColor ?? new Color(0f, 1f, 0f, 0.3f);
            _showPlacePreview = showPlacePreview;

            bool showPreview = position.x != -999;
            if (_hoverRenderer != null)
                _hoverRenderer.enabled = showPreview;
            if (_placeRenderer != null)
                _placeRenderer.enabled = showPreview && showPlacePreview;

            if (showPreview)
            {
                // 更新颜色
                if (_hoverRenderer != null)
                {
                    _hoverRenderer.startColor = _hoverColor;
                    _hoverRenderer.endColor = _hoverColor;
                    // 绘制预览框
                    DrawWireBox(_hoverRenderer, position + Vector3.one * 0.5f, Vector3.one);
                }
                
                if (_placeRenderer != null && showPlacePreview)
                {
                    _placeRenderer.startColor = _placeColor;
                    _placeRenderer.endColor = _placeColor;
                    DrawWireFace(_placeRenderer, position + Vector3.one * 0.5f, Vector3.one, normal);
                }
            }
        }

        /// <summary>
        /// 隐藏所有预览框
        /// </summary>
        public void HidePreview()
        {
            if (_hoverRenderer != null)
                _hoverRenderer.enabled = false;
            if (_placeRenderer != null)
                _placeRenderer.enabled = false;
        }

        private void DrawWireBox(LineRenderer renderer, Vector3 center, Vector3 size)
        {
            Vector3 half = size / 2f;
            Vector3[] points = new Vector3[24];
            int index = 0;

            // 前面四条线
            points[index++] = center + new Vector3(-half.x, -half.y, -half.z);
            points[index++] = center + new Vector3(half.x, -half.y, -half.z);

            points[index++] = center + new Vector3(half.x, -half.y, -half.z);
            points[index++] = center + new Vector3(half.x, half.y, -half.z);

            points[index++] = center + new Vector3(half.x, half.y, -half.z);
            points[index++] = center + new Vector3(-half.x, half.y, -half.z);

            points[index++] = center + new Vector3(-half.x, half.y, -half.z);
            points[index++] = center + new Vector3(-half.x, -half.y, -half.z);

            // 后面四条线
            points[index++] = center + new Vector3(-half.x, -half.y, half.z);
            points[index++] = center + new Vector3(half.x, -half.y, half.z);

            points[index++] = center + new Vector3(half.x, -half.y, half.z);
            points[index++] = center + new Vector3(half.x, half.y, half.z);

            points[index++] = center + new Vector3(half.x, half.y, half.z);
            points[index++] = center + new Vector3(-half.x, half.y, half.z);

            points[index++] = center + new Vector3(-half.x, half.y, half.z);
            points[index++] = center + new Vector3(-half.x, -half.y, half.z);

            // 连接前后面的四条线
            points[index++] = center + new Vector3(-half.x, -half.y, -half.z);
            points[index++] = center + new Vector3(-half.x, -half.y, half.z);

            points[index++] = center + new Vector3(half.x, -half.y, -half.z);
            points[index++] = center + new Vector3(half.x, -half.y, half.z);

            points[index++] = center + new Vector3(half.x, half.y, -half.z);
            points[index++] = center + new Vector3(half.x, half.y, half.z);

            points[index++] = center + new Vector3(-half.x, half.y, -half.z);
            points[index++] = center + new Vector3(-half.x, half.y, half.z);

            renderer.SetPositions(points);
        }

        private void DrawWireFace(LineRenderer renderer, Vector3 center, Vector3 size, Vector3 normal)
        {
            Vector3 half = size / 2f;
            Vector3[] points = new Vector3[5];
            
            // 根据法线方向确定要绘制的面
            if (normal.x != 0)
            {
                // X轴面
                float x = normal.x > 0 ? half.x : -half.x;
                points[0] = center + new Vector3(x, -half.y, -half.z);
                points[1] = center + new Vector3(x, half.y, -half.z);
                points[2] = center + new Vector3(x, half.y, half.z);
                points[3] = center + new Vector3(x, -half.y, half.z);
                points[4] = points[0]; // 闭合线框
            }
            else if (normal.y != 0)
            {
                // Y轴面
                float y = normal.y > 0 ? half.y : -half.y;
                points[0] = center + new Vector3(-half.x, y, -half.z);
                points[1] = center + new Vector3(half.x, y, -half.z);
                points[2] = center + new Vector3(half.x, y, half.z);
                points[3] = center + new Vector3(-half.x, y, half.z);
                points[4] = points[0]; // 闭合线框
            }
            else
            {
                // Z轴面
                float z = normal.z > 0 ? half.z : -half.z;
                points[0] = center + new Vector3(-half.x, -half.y, z);
                points[1] = center + new Vector3(half.x, -half.y, z);
                points[2] = center + new Vector3(half.x, half.y, z);
                points[3] = center + new Vector3(-half.x, half.y, z);
                points[4] = points[0]; // 闭合线框
            }

            renderer.positionCount = 5;
            renderer.SetPositions(points);
        }

        private void OnDestroy()
        {
            if (_hoverRenderer != null) Destroy(_hoverRenderer.gameObject);
            if (_placeRenderer != null) Destroy(_placeRenderer.gameObject);
        }
    }
} 