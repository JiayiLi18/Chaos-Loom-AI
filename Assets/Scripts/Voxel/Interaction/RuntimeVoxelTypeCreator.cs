using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using Voxels;

public class RuntimeVoxelTypeCreator : MonoBehaviour
{
     [Header("UI Refs")]
    public RawImage   texPreview;        // 显示当前选择的纹理
    public TMP_InputField nameField;
    public string texPath = "Assets/Resources/VoxelTextures/Texture_test.png";
    public Button     pickColorBtn, createTypeBtn, createTextureBtn;
    public Image      colorPreview;

    // 创建纹理 待实现
    public Toggle isCreatingTexture;
    public TMP_InputField pprompt;
    
    Color32 _baseColor = Color.white;
    Texture2D _tex;

    void Start()
    {
        pickColorBtn.onClick.AddListener(OnPickColor);
        createTypeBtn.onClick.AddListener(OnPlayerCreateType);
        createTextureBtn.onClick.AddListener(OnPlayerCreateTexture);
    }

    /// <summary>
    /// 根据指定路径加载纹理
    /// </summary>
    /// <param name="texturePath">纹理文件路径</param>
    /// <returns>加载的Texture2D对象，如果加载失败则返回null</returns>
    public static Texture2D LoadTextureFromPath(string texturePath)
    {
        if (string.IsNullOrEmpty(texturePath) || !File.Exists(texturePath))
        {
            Debug.LogWarning($"Invalid texture path: {texturePath}");
            return null;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(texturePath);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            tex.filterMode = FilterMode.Point;
            return tex;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load texture from {texturePath}: {e.Message}");
            return null;
        }
    }

    void OnPickColor()
    {
        _baseColor = new Color(Random.value, Random.value, Random.value, 1);
        colorPreview.color = _baseColor;
    }

    void OnPlayerCreateType()
    {
        ushort newId;
        if(texPath != null)
        {
            _tex = LoadTextureFromPath(texPath);
        }
        
        if (_tex != null)
        {
            _tex.name = Path.GetFileNameWithoutExtension(texPath); // 确保每个tex.name是唯一的!!!
            texPreview.texture = _tex;
            newId = CreateVoxelType(nameField.text, _baseColor, _tex);
        }
        else
        {
            newId = CreateVoxelType(nameField.text, _baseColor);
        }

        // 把新方块设为当前刷子
        FindAnyObjectByType<PlayerVoxelTool>().selectedType = (byte)newId;
    }

    void OnPlayerCreateTexture()
    {
    }

    public static ushort CreateVoxelType(string name, Color baseColor)
    {
        var def = ScriptableObject.CreateInstance<VoxelDefinition>();
        def.name = $"RuntimeVoxel_{VoxelRegistry.Count}";   // make sure the name is not the same
        def.displayName = name;
        def.baseColor = baseColor;
        def.textureIndex = 0;

        ushort newId = VoxelRegistry.Register(def);
        Debug.Log($"🆕 Created Voxel id={newId}");
        
        return newId;
    }

    // 添加支持贴图的方法
    public static ushort CreateVoxelType(string name, Color baseColor, Texture2D texture)
    {
        var def = ScriptableObject.CreateInstance<VoxelDefinition>();
        def.name = $"RuntimeVoxel_{VoxelRegistry.Count}";   // make sure the name is not the same
        def.displayName = name;
        def.baseColor = baseColor;
        def.textureIndex = 0;

        ushort newId = VoxelRegistry.Register(def);
        Debug.Log($"🆕 Created Voxel id={newId} with texture: {(texture != null ? texture.name : "none")}");
        
        return newId;
    }
}
