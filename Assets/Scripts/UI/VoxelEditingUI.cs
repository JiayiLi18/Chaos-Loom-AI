using UnityEngine;
using UnityEngine.UI;
using Voxels;
using TMPro;

/// <summary>
/// 体素编辑UI管理器，负责协调各个编辑功能
/// </summary>
public class VoxelEditingUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject voxelEditingPanel;
    [SerializeField] public Button comfirmBtn; //comfirm and create voxel type
    [SerializeField] public Button resetBtn; //cancel current operation and reset to default
    [SerializeField] public TMP_InputField nameInput;
    [SerializeField] public TMP_InputField descriptionInput;

    private bool _isInitialized = false;

    private void Start()
    {
        InitializeComponents();
    }

    private void OnEnable()
    {
        if (!_isInitialized)
        {
            InitializeComponents();
        }
        voxelEditingPanel.SetActive(true);
    }

    private void InitializeComponents()
    {
        comfirmBtn = voxelEditingPanel.transform.Find("ComfirmBtn").GetComponent<Button>();
        resetBtn = voxelEditingPanel.transform.Find("ResetBtn").GetComponent<Button>();
        nameInput = voxelEditingPanel.transform.Find("NameInput").GetComponent<TMP_InputField>();
        descriptionInput = voxelEditingPanel.transform.Find("DescriptionInput").GetComponent<TMP_InputField>();
        if (comfirmBtn != null)
            comfirmBtn.onClick.AddListener(Comfirm);
        if (resetBtn != null)
            resetBtn.onClick.AddListener(Reset);

        _isInitialized = true;
    }

    private void OnDisable()
    {
        if (voxelEditingPanel != null)
        {
            voxelEditingPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (comfirmBtn != null)
            comfirmBtn.onClick.RemoveListener(Comfirm);
        if (resetBtn != null)
            resetBtn.onClick.RemoveListener(Reset);
    }

    private void Comfirm()
    {
        Debug.Log("Comfirm");
    }

    private void Reset()
    {
        Debug.Log("Reset");
    }
} 