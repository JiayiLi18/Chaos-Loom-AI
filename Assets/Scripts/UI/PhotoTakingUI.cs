using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 管理拍照功能的UI界面
/// </summary>
public class PhotoTakingUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] public Button takePhotoButton;
    [SerializeField] public Button closeButton;
    [SerializeField] public RawImage previewImage;
    [SerializeField] private TMP_Text statusText;
    public GameObject photoTakingUIPanel;
    
    private bool _isInitialized = false;
    
    // 关闭UI请求事件
    public System.Action OnCloseRequested;
    
    private void InitializeComponents()
    {
        if (takePhotoButton != null)
        {
            takePhotoButton.onClick.AddListener(HandleTakePhoto);
        }
        else
        {
            Debug.LogWarning("未设置拍照按钮引用");
        }
        
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HandleClose);
        }
        else
        {
            Debug.LogWarning("未设置关闭按钮引用");
        }

        _isInitialized = true;
    }
    
    private void HandleClose()
    {
        OnCloseRequested?.Invoke();
    }

    private void OnEnable()
    {
        if (!_isInitialized)
        {
            InitializeComponents();
        }
        // 当被启用时，激活子UI对象
        if (photoTakingUIPanel != null)
        {
            photoTakingUIPanel.SetActive(true);
        }
    }

    private void OnDisable()
    {
        // 当被禁用时，关闭子UI对象
        if (photoTakingUIPanel != null)
        {
            photoTakingUIPanel.SetActive(false);
        }
    }

    private void HandleTakePhoto()
    {
        if (!_isInitialized) return;
        if (statusText != null)
        {
            statusText.text = "Photo taken!";
            // 2秒后清除状态文本
            Invoke(nameof(ClearStatusText), 2f);
        }
    }

    private void ClearStatusText()
    {
        if (statusText != null)
        {
            statusText.text = "";
        }
    }

    private void OnDestroy()
    {
        if (takePhotoButton != null)
        {
            takePhotoButton.onClick.RemoveListener(HandleTakePhoto);
        }
        
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(HandleClose);
        }
        
        // 清理事件
        OnCloseRequested = null;
    }
} 