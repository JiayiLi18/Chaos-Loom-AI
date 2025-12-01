using UnityEngine;
using System.IO;

/// <summary>
/// 负责实际拍照功能和照片保存的核心类
/// </summary>
public class RuntimePhotoTaker : MonoBehaviour
{
    [Header("Photo Settings")]
    [SerializeField] private string saveDirectory = "Photos";
    [SerializeField] private RenderTexture previewRenderTexture;

    [SerializeField] private Camera photoCamera;
    private PhotoTakingUI _photoUI;
    private PhotoInventoryUI _photoInventory;
    private string _savePath;
    private bool _isInitialized = false;
    
    private void Start()
    {
        InitializeComponents();
    }
    
    private void OnDestroy()
    {
        // 取消订阅PhotoInventoryUI的事件
        if (_photoInventory != null)
        {
            _photoInventory.TogglePhotoTakingUIRequested -= OnTogglePhotoTakingUIRequested;
        }
        
        // 取消订阅PhotoTakingUI的事件
        if (_photoUI != null)
        {
            _photoUI.OnCloseRequested -= OnPhotoTakingUICloseRequested;
        }
    }
    
    private void OnPhotoTakingUICloseRequested()
    {
        // 关闭拍照UI
        if (_photoUI != null)
        {
            _photoUI.photoTakingUIPanel.SetActive(false);
        }
    }
    
    private void OnTogglePhotoTakingUIRequested()
    {
        TogglePhotoTakingUI();
    }

    private void InitializeComponents()
    {
        if (photoCamera == null)
        {
            Debug.LogError("找不到主摄像机，请确保场景中有标记为MainCamera的相机");
            enabled = false;
            return;
        }

        _photoUI = FindAnyObjectByType<PhotoTakingUI>();
        if (_photoUI == null)
        {
            Debug.LogWarning("找不到PhotoTakingUI组件，UI功能将不可用");
            return;
        }
        
        // 默认关闭拍照UI
        if (_photoUI.photoTakingUIPanel != null)
        {
            _photoUI.photoTakingUIPanel.SetActive(false);
        }
        
        // 绑定拍照按钮事件
        if (_photoUI.takePhotoButton != null)
        {
            _photoUI.takePhotoButton.onClick.AddListener(TakePhoto);
        }
        if (_photoUI.previewImage != null)
        {
            _photoUI.previewImage.texture = previewRenderTexture;
        }
        
        // 订阅关闭事件
        _photoUI.OnCloseRequested += OnPhotoTakingUICloseRequested;

        _photoInventory = FindAnyObjectByType<PhotoInventoryUI>();
        if (_photoInventory == null)
        {
            Debug.LogWarning("找不到PhotoInventoryUI组件，照片库功能将不可用");
        }
        else
        {
            // 订阅PhotoInventoryUI的按钮点击事件
            _photoInventory.TogglePhotoTakingUIRequested += OnTogglePhotoTakingUIRequested;
        }

        // 使用persistentDataPath来保存照片
        _savePath = Path.Combine(Application.persistentDataPath, saveDirectory);
        if (!Directory.Exists(_savePath))
        {
            Directory.CreateDirectory(_savePath);
            Debug.Log($"创建照片保存目录: {_savePath}");
        }

        _isInitialized = true;
    }

    private void OnEnable()
    {
        if (!_isInitialized)
        {
            InitializeComponents();
        }
        // 不再自动启用PhotoTakingUI，由按钮控制
        if (_photoInventory != null)
        {
            _photoInventory.enabled = true;
        }
    }

    private void OnDisable()
    {
        // 不再自动禁用PhotoTakingUI，由按钮控制
        if (_photoInventory != null)
        {
            _photoInventory.enabled = false;
        }
    }

    public void TakePhoto()
    {
        if (!_isInitialized) return;
        
        // 设置相机渲染目标
        photoCamera.targetTexture = previewRenderTexture;
        
        // 渲染到纹理
        photoCamera.Render();
        
        // 创建Texture2D并读取像素
        Texture2D photo = new Texture2D(previewRenderTexture.width, previewRenderTexture.height, TextureFormat.RGB24, false);
        RenderTexture.active = previewRenderTexture;
        photo.ReadPixels(new Rect(0, 0, previewRenderTexture.width, previewRenderTexture.height), 0, 0);
        photo.Apply();
        
        // 恢复相机设置
        photoCamera.targetTexture = null;
        RenderTexture.active = null;
        
        // 保存照片
        SavePhoto(photo);
        
        // 销毁临时纹理
        Destroy(photo);
    }

    public void TogglePhotoTakingUI()
    {
        if (_photoUI != null)
        {
            // 切换显示状态
            bool currentState = _photoUI.gameObject.activeSelf;
            _photoUI.gameObject.SetActive(!currentState);
            //Debug.Log($"拍照UI切换为: {(!currentState ? "开启" : "关闭")}");
        }
    }

    private void SavePhoto(Texture2D photo)
    {
        string fileName = $"Photo_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
        string fullPath = Path.Combine(_savePath, fileName);
        
        byte[] bytes = photo.EncodeToPNG();
        File.WriteAllBytes(fullPath, bytes);
        
        Debug.Log($"照片已保存至: {fullPath}");

        // 刷新照片库
        if (_photoInventory != null)
        {
            _photoInventory.RefreshPhotoGrid();
        }
    }
} 