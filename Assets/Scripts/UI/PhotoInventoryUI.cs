using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Linq;

/// <summary>
/// 管理照片库界面，显示所有已保存的照片
/// </summary>
public class PhotoInventoryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform photoGrid;
    [SerializeField] private GameObject photoInventoryUI;
    [SerializeField] private GameObject photoPreviewUI;
    [SerializeField] private Button closePreviewButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private RawImage previewDisplayImage; // 大图预览用的RawImage

    [Header("Photo Prefab UI")]
    [SerializeField] private GameObject photoPrefab;
    [SerializeField] private string selectButtonPath = "SelectButton"; // 预制体中按钮的路径
    [SerializeField] private string previewImagePath = "PreviewImage";
    [SerializeField] private string frameImagePath = "SelectButton";//边框颜色
    [SerializeField] private Color selectedColor = new Color(0.055f, 0.9f, 0.21f, 1f);
    [SerializeField] private Color normalColor = new Color(0.23f, 0.23f, 0.23f, 1f);

    public static string _photoDirectory;
    public static string _currentSelectedPhotoPath;
    private Image _currentSelectedFrame;
    private bool _isInitialized = false;

    // 暴露当前选中的纹理
    public Texture CurrentTexture { get; private set; }
    public static string FolderPath = "Photos";

    // 添加照片选择事件
    public System.Action OnPhotoSelected;

    private void Start()
    {
        InitializeComponents();
    }
    private void OnEnable()
    {
        InitializeComponents();
        if(photoInventoryUI != null)
        {
            photoInventoryUI.SetActive(true);
        }
        if(photoPreviewUI != null)
        {
            photoPreviewUI.SetActive(false);
        }
        RefreshPhotoGrid();
    }

    private void InitializeComponents()
    {
         _photoDirectory = Path.Combine(Application.persistentDataPath, FolderPath);
        if (closePreviewButton != null)
        {
            closePreviewButton.onClick.AddListener(() => SetPreviewActive(false));
        }
        if (deleteButton != null)
        {
            deleteButton.onClick.AddListener(() => DeletePhoto(_currentSelectedPhotoPath));
        }

        if(photoInventoryUI != null)
        {
            photoInventoryUI.SetActive(true);
        }

        if (photoPreviewUI != null)
        {
            photoPreviewUI.SetActive(false);
        }

        _isInitialized = true;
    }

    public void RefreshPhotoGrid()
    {
        if (!_isInitialized || photoGrid == null || photoPrefab == null) return;

        // 清除现有的照片
        foreach (Transform child in photoGrid)
        {
            Destroy(child.gameObject);
        }

        // 确保目录存在
        if (!Directory.Exists(_photoDirectory))
        {
            Debug.LogWarning("照片目录不存在");
            return;
        }

        // 获取所有PNG文件
        string[] files = Directory.GetFiles(_photoDirectory, "*.png")
            .OrderByDescending(f => new FileInfo(f).CreationTime) // 按创建时间降序排序
            .ToArray();

        foreach (string file in files)
        {
            LoadPhotoIntoGrid(file);
        }
    }

    private void LoadPhotoIntoGrid(string filePath)
    {
        try
        {
            byte[] data = File.ReadAllBytes(filePath);
            Texture2D tex = new Texture2D(2, 2);
            if (tex.LoadImage(data))
            {
                GameObject photoGO = Instantiate(photoPrefab, photoGrid);
                RawImage rawImage = photoGO.transform.Find(previewImagePath)?.GetComponent<RawImage>();
                Button selectBtn = photoGO.transform.Find(selectButtonPath)?.GetComponent<Button>();
                Image frameImage = photoGO.transform.Find(frameImagePath)?.GetComponent<Image>();

                if (rawImage != null)
                {
                    rawImage.texture = tex;
                }
                if (selectBtn != null)
                {
                    selectBtn.onClick.AddListener(() => SelectPhoto(filePath, tex, frameImage));
                }
                if (frameImage != null)
                {
                    frameImage.color = normalColor;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"加载照片失败: {filePath}\n{e.Message}");
        }
    }

    private void SelectPhoto(string filePath, Texture2D tex, Image frameImage)
    {
        // Reset previous selection if exists
        if (_currentSelectedFrame != null)
        {
            _currentSelectedFrame.color = normalColor;
        }

        _currentSelectedPhotoPath = filePath;
        _currentSelectedFrame = frameImage;
        
        if (previewDisplayImage != null)
        {
            previewDisplayImage.texture = tex;
        }
        if (frameImage != null)
        {
            frameImage.color = selectedColor;
        }

        // 更新当前纹理
        CurrentTexture = tex;

        // 触发选择事件
        Debug.Log("Triggering photo selected event");
        OnPhotoSelected?.Invoke();

        SetPreviewActive(true);
    }

    private void DeletePhoto(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Debug.Log($"照片已删除: {filePath}");
                
                // 如果删除的是当前预览的照片，关闭预览
                if (filePath == _currentSelectedPhotoPath)
                {
                    SetPreviewActive(false);
                }
                
                // 刷新照片网格
                RefreshPhotoGrid();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"删除照片失败: {filePath}\n{e.Message}");
        }
    }


    private void SetPreviewActive(bool active)
    {
        if (photoPreviewUI != null)
        {
            photoPreviewUI.SetActive(active);
            if (!active)
            {
                if (_currentSelectedFrame != null)
                {
                    _currentSelectedFrame.color = normalColor;
                    _currentSelectedFrame = null;
                }
                _currentSelectedPhotoPath = null;
                if (previewDisplayImage != null)
                {
                    previewDisplayImage.texture = null;
                }
                // 清除当前纹理
                CurrentTexture = null;
            }
        }
    }

    private void OnDisable()
    {
        if(photoInventoryUI != null)
        {
            photoInventoryUI.SetActive(false);
        }
        if(photoPreviewUI != null)
        {
            photoPreviewUI.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (closePreviewButton != null)
        {
            closePreviewButton.onClick.RemoveAllListeners();
        }
    }
}
