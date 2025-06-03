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
    [SerializeField] public RawImage previewImage;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject photoUI;
    
    private bool _isInitialized = false;

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

        _isInitialized = true;
    }

    private void OnEnable()
    {
        if (!_isInitialized)
        {
            InitializeComponents();
        }
        if (photoUI != null)
        {
            photoUI.SetActive(true);
        }
    }

    private void OnDisable()
    {
        if (photoUI != null)
        {
            photoUI.SetActive(false);
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
    }
} 