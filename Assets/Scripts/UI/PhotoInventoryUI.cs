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
    [SerializeField] private GameObject openCamButtonPrefab; // 拍照按钮预制体
    [SerializeField] private string selectButtonPath = "SelectButton"; // 预制体中按钮的路径
    [SerializeField] private string previewImagePath = "PreviewImage";
    [SerializeField] private string frameImagePath = "SelectButton";//边框颜色
    [SerializeField] private Color selectedColor = new Color(0.055f, 0.9f, 0.21f, 1f);
    [SerializeField] private Color normalColor = new Color(0.23f, 0.23f, 0.23f, 1f);

    private static string _photoDirectory;
    private static string _currentSelectedPhotoPath;
    private Image _currentSelectedFrame;
    private bool _isInitialized = false;
    private GameObject _openCamButton; // 保存拍照按钮引用

    // 暴露当前选中的纹理
    public Texture CurrentTexture { get; private set; }
    public string CurrentPhotoPath => _currentSelectedPhotoPath;
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

        // 清除现有的照片（跳过TakePhoto按钮）
        foreach (Transform child in photoGrid)
        {
            // 检查是否是保存的拍照按钮引用
            if (child.gameObject == _openCamButton)
            {
                continue; // 跳过拍照按钮
            }
            Destroy(child.gameObject);
        }

        // 在第一个位置添加拍照按钮（如果不存在）
        if (openCamButtonPrefab != null)
        {
            // 检查是否已经有拍照按钮
            if (_openCamButton == null)
            {
                GameObject takePhotoBtn = Instantiate(openCamButtonPrefab, photoGrid);
                takePhotoBtn.name = "TakePhotoButton"; // 给一个标识名称
                _openCamButton = takePhotoBtn; // 保存引用
                // 设置按钮点击事件
                Button btn = takePhotoBtn.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(OpenPhotoTakingUI);
                }
            }
        }

        // 确保目录存在
        if (!Directory.Exists(_photoDirectory))
        {
            Debug.LogWarning("Photo directory does not exist");
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
    
    private void OpenPhotoTakingUI()
    {
        // 打开拍照UI前，清空当前选择并关闭预览
        SetPreviewActive(false);
        // 直接触发打开拍照UI的事件，由RuntimePhotoTaker监听
        TogglePhotoTakingUIRequested?.Invoke();
    }
    
    // UI按钮点击事件
    public System.Action TogglePhotoTakingUIRequested;
    // 关闭拍照UI请求事件（在选择照片时关闭拍照界面）
    public System.Action ClosePhotoTakingUIRequested;

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
            Debug.LogError($"Failed to load photo: {filePath}\n{e.Message}");
        }
    }

    private void SelectPhoto(string filePath, Texture2D tex, Image frameImage)
    {
        //Debug.Log($"SelectPhoto called with path: {filePath}");
        
        // 若拍照界面打开，请求关闭
        ClosePhotoTakingUIRequested?.Invoke();
        
        // 如果再次点击同一张照片，则作为开关：切换预览面板显示/隐藏
        if (_currentSelectedPhotoPath == filePath)
        {
            bool isPreviewActive = photoPreviewUI != null && photoPreviewUI.activeSelf;
            // 切换状态
            SetPreviewActive(!isPreviewActive);
            
            // 如果切换为显示，需要恢复当前选择的可视状态
            if (!isPreviewActive)
            {
                _currentSelectedFrame = frameImage;
                if (previewDisplayImage != null)
                {
                    previewDisplayImage.texture = tex;
                }
                if (frameImage != null)
                {
                    frameImage.color = selectedColor;
                }
                CurrentTexture = tex;
            }
            return;
        }
        
        // Reset previous selection if exists
        if (_currentSelectedFrame != null)
        {
            _currentSelectedFrame.color = normalColor;
        }

        _currentSelectedPhotoPath = filePath;
        //Debug.Log($"Set _currentSelectedPhotoPath to: {_currentSelectedPhotoPath}");
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
        //Debug.Log($"Current texture set, null? {CurrentTexture == null}");

        // 触发选择事件
        //Debug.Log($"Triggering photo selected event with path: {CurrentPhotoPath}");
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
                Debug.Log($"Photo deleted: {filePath}");
                
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
            Debug.LogError($"Failed to delete photo: {filePath}\n{e.Message}");
        }
    }


    private void SetPreviewActive(bool active)
    {
        //Debug.Log($"SetPreviewActive called with active: {active}");
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
                //Debug.Log("Clearing photo selection state");
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
        
        // 清理事件监听
        TogglePhotoTakingUIRequested = null;
        ClosePhotoTakingUIRequested = null;
        
        // 清理拍照按钮引用
        _openCamButton = null;
    }
}
