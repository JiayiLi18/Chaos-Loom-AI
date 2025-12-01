using UnityEngine;
using UnityEngine.Events;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class AgentCam : MonoBehaviour
{
    [Header("Agent Cam Settings")]
    [SerializeField] private Camera _agentCam;
    [SerializeField] private Transform _agentTransform;
    
    [Header("Four Direction Photo Settings")]
    [SerializeField] private RenderTexture photoRenderTexture;
    [SerializeField] private string saveDirectory = "Photos";
    [SerializeField] private float photoDelay = 0.05f; // 每次拍照之间的延迟
    
    private PhotoInventoryUI _photoInventory;
    private AgentMove _agentMove;
    private string _savePath;
    private bool _isInitialized = false;
    
    // 跟踪最近拍摄的四张照片文件名
    private List<string> _lastFourPhotos = new List<string>();

    void OnEnable()
    {
        InitializeComponents();
    }
    
    private void InitializeComponents()
    {
        if (_agentCam == null)
        {
            GameObject agentCamGO = GameObject.Find("agentCam");
            if (agentCamGO != null)
            {
                _agentCam = agentCamGO.GetComponent<Camera>();
            }
        }
        if (_agentTransform == null)
        {
            GameObject agentGO = GameObject.Find("agent");
            if (agentGO != null)
            {
                _agentTransform = agentGO.transform;
            }
        }
        
        // 查找PhotoInventoryUI组件
        _photoInventory = FindAnyObjectByType<PhotoInventoryUI>();
        if (_photoInventory == null)
        {
            Debug.LogWarning("Can't find PhotoInventoryUI component, photo inventory feature will be unavailable");
        }
        
        // 查找AgentMove组件
        _agentMove = FindAnyObjectByType<AgentMove>();
        if (_agentMove == null)
        {
            Debug.LogWarning("Can't find AgentMove component, direction detection feature will be unavailable");
        }
        
        // 设置保存路径
        _savePath = Path.Combine(Application.persistentDataPath, saveDirectory);
        if (!Directory.Exists(_savePath))
        {
            Directory.CreateDirectory(_savePath);
            Debug.Log($"Create photo save directory: {_savePath}");
        }

        if(photoRenderTexture == null)
        {
            Debug.LogError("photoRenderTexture is not set, can't take photos");
            return;
        }
        
        _isInitialized = true;
    }
    
    /// <summary>
    /// 拍摄四方向照片（前后左右各90度）
    /// </summary>
    public void TakeFourDirectionPhotos()
    {
        if (!_isInitialized || _agentCam == null || _agentTransform == null || _agentMove == null)
        {
            Debug.LogError("AgentCam is not correctly initialized, can't take photos");
            return;
        }
        
        StartCoroutine(TakeFourDirectionPhotosCoroutine());
    }
    
    /// <summary>
    /// 拍摄四方向照片（前后左右各90度），完成后执行回调
    /// </summary>
    /// <param name="onComplete">拍照完成后的回调函数，参数为照片文件名列表</param>
    public void TakeFourDirectionPhotos(System.Action<List<string>> onComplete = null)
    {
        if (!_isInitialized || _agentCam == null || _agentTransform == null || _agentMove == null)
        {
            Debug.LogError("AgentCam is not correctly initialized, can't take photos");
            onComplete?.Invoke(null);
            return;
        }
        
        StartCoroutine(TakeFourDirectionPhotosCoroutine(onComplete));
    }
    
    private IEnumerator TakeFourDirectionPhotosCoroutine()
    {
        yield return StartCoroutine(TakeFourDirectionPhotosCoroutine(null));
    }
    
    private IEnumerator TakeFourDirectionPhotosCoroutine(System.Action<List<string>> onComplete)
    {
        // 清空上次的照片记录
        _lastFourPhotos.Clear();
        
        // 保存原始旋转
        Vector3 originalRotation = _agentTransform.eulerAngles;
        
        // 从AgentMove获取当前方向区间和标准角度
        AgentMove.DirectionInterval currentInterval = _agentMove.GetCurrentDirectionInterval();
        AgentMove.DirectionAngle[] standardAngles = _agentMove.GetCurrentDirectionAngles();
        
        //Debug.Log($"Current direction interval: {currentInterval}");
        
        for (int i = 0; i < 4; i++)
        {
            // 设置相机到标准角度
            _agentTransform.eulerAngles = new Vector3(originalRotation.x, standardAngles[i].angle, originalRotation.z);
            
            // 等待一帧确保旋转完成
            yield return null;
            
            // 拍照并收集文件名
            string fileName = TakeSinglePhoto(standardAngles[i].direction);
            if (!string.IsNullOrEmpty(fileName))
            {
                _lastFourPhotos.Add(fileName);
            }
            
            // 等待指定延迟
            yield return new WaitForSeconds(photoDelay);
        }
        
        // 恢复原始旋转
        _agentTransform.eulerAngles = originalRotation;
        
        // 刷新照片库
        if (_photoInventory != null)
        {
            _photoInventory.RefreshPhotoGrid();
        }
        
        Debug.Log($"Four direction photos taken, total {_lastFourPhotos.Count} photos");
        
        // 执行回调函数
        onComplete?.Invoke(new List<string>(_lastFourPhotos));
    }
    
    
    /// <summary>
    /// 拍摄单张照片
    /// </summary>
    /// <param name="direction">方向标识</param>
    /// <returns>保存的文件名</returns>
    private string TakeSinglePhoto(string direction)
    {
        if (photoRenderTexture == null)
        {
            Debug.LogError("PhotoRenderTexture is not set, can't take photos");
            return null;
        }
        
        // 设置相机渲染目标
        _agentCam.targetTexture = photoRenderTexture;
        
        // 渲染到纹理
        _agentCam.Render();
        
        // 创建Texture2D并读取像素
        Texture2D photo = new Texture2D(photoRenderTexture.width, photoRenderTexture.height, TextureFormat.RGB24, false);
        RenderTexture.active = photoRenderTexture;
        photo.ReadPixels(new Rect(0, 0, photoRenderTexture.width, photoRenderTexture.height), 0, 0);
        photo.Apply();
        
        // 恢复相机设置
        _agentCam.targetTexture = null;
        RenderTexture.active = null;
        
        // 保存照片
        string fileName = SavePhoto(photo, direction);
        
        // 销毁临时纹理
        Destroy(photo);
        
        return fileName;
    }
    
    /// <summary>
    /// 保存照片到指定位置
    /// </summary>
    /// <param name="photo">照片纹理</param>
    /// <param name="direction">方向标识</param>
    /// <returns>保存的文件名</returns>
    private string SavePhoto(Texture2D photo, string direction)
    {
        string fileName = $"AgentCam_{direction}_{System.DateTime.Now: MM-dd_HH-mm-ss}.png";
        string fullPath = Path.Combine(_savePath, fileName);
        
        byte[] bytes = photo.EncodeToPNG();
        File.WriteAllBytes(fullPath, bytes);
        
        Debug.Log($"AgentCam {direction} direction photo saved to: {fullPath}");
        return fileName;
    }

    /// <summary>
    /// 根据文件名获取照片的完整路径
    /// </summary>
    /// <param name="fileName">保存时返回的文件名</param>
    /// <returns>完整路径</returns>
    public string GetPhotoFullPath(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return null;
        }
        return Path.Combine(_savePath, fileName);
    }
    
    /// <summary>
    /// 获取最近拍摄的四张照片的文件名列表
    /// </summary>
    /// <returns>四张照片的文件名列表</returns>
    public List<string> GetFourDirectionPhotos()
    {
        if (_lastFourPhotos.Count != 4)
        {
            Debug.LogError("The number of recently taken four photos is incorrect, can't get file name list");
            return null;
        }
        else
        {
            return new List<string>(_lastFourPhotos);
        }
    }
}