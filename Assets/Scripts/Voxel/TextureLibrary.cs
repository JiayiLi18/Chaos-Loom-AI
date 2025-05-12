using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;

/// <summary>
/// 体素纹理管理器，负责管理所有体素纹理并提供统一的纹理数组资源。
/// 
/// 核心职责：
/// 1. 将单独的纹理集中管理到一个 Texture2DArray 中，用于 GPU 实例化渲染
/// 2. 提供纹理注册接口，将纹理添加到纹理数组中并返回对应的索引
/// 3. 在初始化完成时触发 OnLibraryInitialized 事件，通知其他系统
/// 4. 通过 IsInitialized 属性对外暴露初始化状态
/// 
/// 与其他组件的关系：
/// - 被 VoxelDefinition 依赖：VoxelDefinition 将纹理注册到此组件获取 sliceIndex
/// - 被 VoxelRegistry 依赖：VoxelRegistry 订阅 OnLibraryInitialized 事件
/// 
/// 执行顺序：该组件设置为 DefaultExecutionOrder(-1000)，确保在 VoxelRegistry 之前初始化
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class TextureLibrary : MonoBehaviour
{
    public static TextureLibrary I { get; private set; }
    public Texture2DArray Array { get; private set; }
    readonly List<Texture2D> _textures = new();
    readonly Dictionary<string, int> _lookup = new();

    const string Folder = "VoxelTextures";   // Resources 根目录下

    /// <summary>
    /// Event triggered when TextureLibrary is fully initialized
    /// </summary>
    public static event Action OnLibraryInitialized;

    /// <summary>
    /// Returns true if the TextureLibrary singleton is initialized and ready to use
    /// </summary>
    public static bool IsInitialized => I != null && I.Array != null;

    void Awake()
    {
        if (I != null)
        {
            Debug.LogWarning("Multiple TextureLibrary instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        // 延迟执行资源加载，防止在 domain reload 阶段被执行
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            // 先初始化纹理数组，确保 IsInitialized 会返回 true
            InitializeTextureArray();
            LoadFolder();            // 使用path加载贴图避免贴图不可读的情况
            
            // 触发初始化完成事件，让 VoxelRegistry 注册所有的 VoxelDefinition 和纹理
            OnLibraryInitialized?.Invoke();
            Debug.Log("TextureLibrary initialized successfully");
        };
#else
        // 先初始化纹理数组，确保 IsInitialized 会返回 true
        InitializeTextureArray();
        LoadFolder();            // 使用path加载贴图避免贴图不可读的情况
        
        // 触发初始化完成事件，让 VoxelRegistry 注册所有的 VoxelDefinition 和纹理
        OnLibraryInitialized?.Invoke();
        Debug.Log("TextureLibrary initialized successfully");
#endif
    }

    void OnDestroy()
    {
        if (I == this)
        {
            I = null;
        }
    }

    #region Public_API
    public static int SafeRegister(Texture2D texture)
    {
        if (!IsInitialized || texture == null)
        {
            Debug.LogError($"TextureLibrary: Failed to register texture '{texture.name}'");
            return -1;
        }

        return I.Register(texture);
    }
    #endregion

    #region Internals
    private int Register(Texture2D tex)
    {
        if (tex == null) return 0; // 0是默认贴图

        if (_lookup.TryGetValue(tex.name, out int id)) //这里必须确保每个tex.name是唯一的   
        {
            Debug.Log($"TextureLibrary: Found existing texture '{tex.name}' at index {id}");
            return id;
        }

        // 如果不是 RGBA32，就转换一下
        if (tex.format != TextureFormat.RGBA32)
        {
            Texture2D converted = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            converted.SetPixels(tex.GetPixels());
            converted.Apply(false); // 不生成 mipmap
            converted.name = tex.name; // 保留原名称以便查找
            tex = converted;
        }
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;

        // 添加到纹理列表
        _textures.Add(tex);

        // 实际索引是列表位置+1（0保留给默认贴图）
        id = _textures.Count; // _textures.Count已经是列表最后一项的索引+1了
        _lookup[tex.name] = id;

        RebuildArray();          // ★ 可热插拔：立刻重建并更新所有材质
        Debug.Log($"TextureLibrary: Registered texture '{tex.name}' at index {id}");
        return id;      //返回的是贴图的 slice index，这个 index 就是 Texture2DArray 中的图层编号。
    }

    // 新增：从路径加载纹理
    private static Texture2D LoadTextureFromPath(string path)
    {
        try
        {
            byte[] fileData = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            if (tex.LoadImage(fileData))
            {
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Repeat;
                tex.name = Path.GetFileNameWithoutExtension(path);
                return tex;
            }
            else
            {
                Debug.LogError($"Failed to load texture from path: {path}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading texture from path {path}: {ex.Message}");
            return null;
        }
    }

    void LoadFolder()
    {
        string fullPath = Path.Combine(Application.dataPath, "Resources", Folder);
        if (Directory.Exists(fullPath))
        {
            string[] files = Directory.GetFiles(fullPath, "*.png");
            foreach (string file in files)
            {
                Texture2D tex = LoadTextureFromPath(file);
                tex.name = Path.GetFileNameWithoutExtension(file); // 保留原名称以便查找
                if (tex != null)
                {
                    SafeRegister(tex);
                }
            }
        }
    }

    void RebuildArray()
    {
        if (_textures.Count == 0)
        {
            // 如果没有找到任何纹理，使用默认纹理数组
            Array = Voxels.VoxelResources.DefaultTextureArray;
            Debug.Log("TextureLibrary: Using default texture array only");
            return;
        }

        Texture2DArray arr;
        // 检查是否已有现有的贴图数组
        if (Array == null)
        {
            // 首次创建：加载或创建默认贴图数组
            arr = Voxels.VoxelResources.DefaultTextureArray;
            // 如果默认贴图数组只有一层，需要扩展它
            if (arr.depth < _textures.Count + 1)
            {
                int size = arr.width;
                Texture2DArray newArr = new Texture2DArray(size, size, _textures.Count + 1,
                                        TextureFormat.RGBA32, false); // false 表示不生成 mipmap

                // 复制默认贴图(0号)
                Graphics.CopyTexture(arr, 0, 0, newArr, 0, 0);
                arr = newArr;
            }
        }
        else
        {
            // 已有贴图数组，检查是否需要扩展
            if (Array.depth < _textures.Count + 1)
            {
                int size = Array.width;
                Texture2DArray newArr = new Texture2DArray(size, size, _textures.Count + 1,
                                        TextureFormat.RGBA32, false); // false 表示不生成 mipmap

                // 复制原有的所有贴图，包括0号默认贴图
                for (int i = 0; i < Array.depth; i++)
                {
                    Graphics.CopyTexture(Array, i, 0, newArr, i, 0);
                }
                Debug.Log($"TextureLibrary: Rebuilt texture array with {_textures.Count + 1 - Array.depth} additional textures");
                arr = newArr;
            }
            else
            {
                // 现有数组足够大，直接使用
                arr = Array;
            }
        }

        // 复制/更新贴图库中的所有贴图（保持_lookup中的索引与实际位置一致）
        for (int i = 0; i < _textures.Count; ++i)
        {
            if (_textures[i] != null)
            {
                // 获取该纹理在_lookup中的索引
                int textureIndex = _lookup[_textures[i].name];
                // 确保索引有效
                if (textureIndex > 0 && textureIndex < arr.depth)
                {
                    try
                    {
                        Graphics.CopyTexture(_textures[i], 0, 0, arr, textureIndex, 0);
                        Debug.Log($"Added/Updated texture '{_textures[i].name}' to array at index {textureIndex}");
                    }
                    catch (UnityException ex)
                    {
                        Debug.LogWarning($"Failed to copy texture '{_textures[i].name}': {ex.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Invalid texture index {textureIndex} for '{_textures[i].name}'");
                }
            }
        }

        arr.Apply(false, false);
        arr.wrapMode = TextureWrapMode.Repeat;
        arr.filterMode = FilterMode.Point;
        Array = arr;

        // 通过VoxelResources全局更新所有材质
        Voxels.VoxelResources.UpdateMaterialsWithTextureArray(arr);

        Debug.Log($"TextureLibrary: Rebuilt texture array with default texture and {_textures.Count} additional textures");
    }

    // 初始化纹理数组
    private void InitializeTextureArray()
    {
        // 使用 VoxelResources 提供的默认纹理数组
        Array = Voxels.VoxelResources.DefaultTextureArray;
        Debug.Log("TextureLibrary: Initialized texture array");
    }

    #endregion
}