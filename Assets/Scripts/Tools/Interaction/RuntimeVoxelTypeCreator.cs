using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using Voxels;

public class RuntimeVoxelTypeCreator : MonoBehaviour
{
    [Header("UI Source")]
    [SerializeField] private VoxelEditingUI voxelEditingUI;
    [SerializeField] private PaintingToolUI paintingToolUI;
    [SerializeField] private ColorPickerUI colorPickerUI;
    [SerializeField] private GameObject paintingObject; //绘制时需要用到的gameobject

    [Header("Dependencies")]
    [SerializeField] private VoxelJsonDB voxelJsonDB;

    [Header("Texture")]
    [SerializeField] private RenderTexture paintingRenderTexture;
    private string texSavePath;

    [Header("AI Texture Creator")]
    //直接找comfyUI生成texture，暂时不处理这部分

    private Texture2D _tex;
    private bool _isInitialized = false;

    private void OnEnable()
    {
        if (!_isInitialized)
        {
            Initialize();
        }

        if (paintingObject != null)
            paintingObject.SetActive(true);

        if (voxelEditingUI != null)
        {
            voxelEditingUI.enabled = true;
            // 添加Confirm按钮的监听
            if (voxelEditingUI.comfirmBtn != null)
            {
                voxelEditingUI.comfirmBtn.onClick.RemoveListener(OnConfirmButtonClicked); // 先移除以防重复
                voxelEditingUI.comfirmBtn.onClick.AddListener(OnConfirmButtonClicked);
            }
        }
        if (paintingToolUI != null)
            paintingToolUI.enabled = true;
        if (colorPickerUI != null)
            colorPickerUI.enabled = true;

        //当colorPickerUI的颜色改变时，把颜色赋给paintingToolUI
        if (paintingToolUI != null && colorPickerUI != null)
            colorPickerUI.onColorChanged.AddListener(paintingToolUI.SetPaintColor);
    }

    private void OnDisable()
    {
        if (paintingObject != null)
            paintingObject.SetActive(false);

        if (voxelEditingUI != null)
        {
            voxelEditingUI.enabled = false;
            // 移除Confirm按钮的监听
            if (voxelEditingUI.comfirmBtn != null)
            {
                voxelEditingUI.comfirmBtn.onClick.RemoveListener(OnConfirmButtonClicked);
            }
        }
        if (paintingToolUI != null)
            paintingToolUI.enabled = false;
        if (colorPickerUI != null)
            colorPickerUI.enabled = false;
    }

    // 新增：将RenderTexture转换为Texture2D
    private Texture2D ConvertRenderTextureToTexture2D(RenderTexture renderTexture)
    {
        if (renderTexture == null)
        {
            Debug.LogError("RenderTexture is null!");
            return null;
        }

        // 创建新的Texture2D，确保是可读的
        Texture2D texture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
        texture2D.filterMode = FilterMode.Point;
        texture2D.wrapMode = TextureWrapMode.Repeat;
        
        // 保存当前的RenderTexture
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        
        // 读取像素到Texture2D
        texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        texture2D.Apply();

        // 恢复之前的RenderTexture
        RenderTexture.active = previous;

        return texture2D;
    }

    private void OnConfirmButtonClicked()
    {
        if (voxelEditingUI == null)
        {
            Debug.LogError("VoxelEditingUI is not set!");
            return;
        }

        if (paintingRenderTexture == null)
        {
            Debug.LogError("PaintingRenderTexture is not set!");
            return;
        }

        string voxelName = voxelEditingUI.nameInput.text;
        string description = voxelEditingUI.descriptionInput.text;

        if (string.IsNullOrEmpty(voxelName))
        {
            Debug.LogWarning("Please enter a name for the voxel!");
            return;
        }

        // 直接从RenderTexture转换为Texture2D
        _tex = ConvertRenderTextureToTexture2D(paintingRenderTexture);
        if (_tex == null)
        {
            Debug.LogError("Failed to convert RenderTexture to Texture2D!");
            return;
        }
        
        // 设置贴图名称
        _tex.name = voxelName;

        CreateVoxelType(voxelName, description);
    }

    public void CreateVoxelType(string name, string description = "")
    {
        //确保名字不为空
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogError("Name is empty");
            return;
        }
        
        if (_tex == null)
        {
            Debug.LogError("Texture is not set (_tex is null). Cannot create voxel type.");
            return;
        }

        if (voxelJsonDB == null)
        {
            Debug.LogError("VoxelJsonDB reference is not set. Cannot create voxel type.");
            return;
        }

        // 生成唯一的文件名：原名字_时间戳_随机数
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string uniqueTexName = $"{name}_{timestamp}";
        _tex.name = uniqueTexName;  // 更新贴图名称为唯一名称

        // 确保目录存在
        if (!Directory.Exists(texSavePath))
        {
            Directory.CreateDirectory(texSavePath);
        }

        // 保存贴图
        byte[] pngData = _tex.EncodeToPNG();
        string texPath = Path.Combine(texSavePath, uniqueTexName + ".png");
        File.WriteAllBytes(texPath, pngData);
        
        // 刷新Unity的AssetDatabase以便编辑器能够检测到新文件
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif

        // 从Resources加载新保存的贴图
        Texture2D loadedTex = Resources.Load<Texture2D>(Path.Combine(TextureLibrary.Folder, uniqueTexName)); // 使用唯一的贴图名
        if (loadedTex == null)
        {
            Debug.LogError($"Failed to load texture {uniqueTexName} from Resources/{TextureLibrary.Folder} after saving. Check path and import settings.");
            loadedTex = _tex; // 如果加载失败，使用原始贴图
        }

        // 注册新的体素
        ushort newId = voxelJsonDB.AddVoxel(name, Color.white, _tex, description, false); // 使用原始的_tex而不是loadedTex
        if (newId > 0)
        {
            Debug.Log($"Voxel type '{name}' created and registered with ID {newId}. Texture saved as {uniqueTexName}");
            //TODO:把voxelInventory中的当前选择的slot设置为newId代表的voxel
        }
        else
        {
            Debug.LogError($"Failed to add voxel '{name}' to VoxelJsonDB.");
        }

        //Reset当前的UI
        Reset();
    }

    private void Initialize()
    {
        if (voxelEditingUI == null)
            voxelEditingUI = FindAnyObjectByType<VoxelEditingUI>();
        if (paintingToolUI == null)
            paintingToolUI = FindAnyObjectByType<PaintingToolUI>();
        if (voxelJsonDB == null)
            voxelJsonDB = FindAnyObjectByType<VoxelJsonDB>();

        // 设置贴图保存路径为和TextureLibrary一样的Resources路径
        texSavePath = TextureLibrary.TextureSavePath;
            
        _isInitialized = true;
    }

    private void Reset()
    {
        //TODO: 清空绘制的gameobject的material的masktexture并且把previewImage的texture重置
    }
}
