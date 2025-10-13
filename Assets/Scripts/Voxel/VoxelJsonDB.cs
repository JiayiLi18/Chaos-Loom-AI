using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;

namespace Voxels
{
    [Serializable]
    public class VoxelEntry
    {
        public int id;
        public string name;
        // 兼容旧版本的单一贴图
        public string texture;
        // 新增：支持六面贴图
        public string[] face_textures;
        public int[] base_color;
        public string description;
        public bool is_transparent;
    }

    [Serializable]
    public class VoxelDB
    {
        public int next_id = 1;
        public string revision = DateTime.UtcNow.ToString("o");
        public List<VoxelEntry> voxels = new List<VoxelEntry>();
    }

    /// <summary>
    /// 体素JSON数据库，用于管理体素定义与外部API交互
    /// 
    /// 核心职责：
    /// 1. 读写voxel_definitions.json文件，同步Unity和外部API
    /// 2. 提供体素添加和修改的接口
    /// 3. 管理体素ID分配
    /// 
    /// 与其他组件的关系：
    /// - 使用 TextureLibrary：复用现有纹理注册机制
    /// - 使用 VoxelRegistry：注册体素定义
    /// </summary>
    public class VoxelJsonDB : MonoBehaviour
    {
        private static VoxelJsonDB _instance;
        public static VoxelJsonDB Instance { get; private set; }

        // 改为非静态字段，在 Awake 中初始化
        private string _dbDir;
        private string _jsonPath;
        private string _texDir;

        private VoxelDB _cache;
        private bool _isInitialized = false;

        // 如果开启自动刷新，每帧会检查JSON文件的修改时间
        //public bool autoRefresh = false;
        //private DateTime _lastModified;

        // 公开初始化状态
        public bool IsInitialized => _isInitialized;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 在 Awake 中初始化路径
            _dbDir = Path.Combine(Application.persistentDataPath, "VoxelsDB");
            _jsonPath = Path.Combine(_dbDir, "voxel_definitions.json");
            _texDir = Path.Combine(_dbDir, "textures");

            // 确保目录存在
            Directory.CreateDirectory(_dbDir);
            Directory.CreateDirectory(_texDir);

            // 只有在TextureLibrary就绪后再初始化
            TextureLibrary.OnLibraryInitialized += () => 
            {
                if (!_isInitialized && TextureLibrary.IsInitialized)
                {
                    LoadDatabase();
                    _isInitialized = true;
                }
            };

            // 如果TextureLibrary已经初始化，直接加载
            if (TextureLibrary.IsInitialized && !_isInitialized)
            {
                LoadDatabase();
                _isInitialized = true;
            }
        }

        /// <summary>
        /// 载入JSON数据库
        /// </summary>
        public void LoadDatabase()
        {
            try
            {
                if (!File.Exists(_jsonPath))
                {
                    // 首次运行：复制模板或创建新的
                    CopyTemplateIfExists();
                }

                string json = File.ReadAllText(_jsonPath);
                _cache = JsonUtility.FromJson<VoxelDB>(json);

                // 注册所有体素到系统
                RegisterVoxelsFromDb();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoxelJsonDB] Failed to load database: {ex.Message}");
                // 创建新的空数据库
                _cache = new VoxelDB();
                SaveDatabase();
            }
        }

        /// <summary>
        /// 从数据库注册所有体素
        /// </summary>
        private void RegisterVoxelsFromDb()
        {
            if (_cache == null) return;

            foreach (var v in _cache.voxels)
            {
                try
                {
                    // 即使无法加载贴图，也创建体素定义
                    var def = ScriptableObject.CreateInstance<VoxelDefinition>();
                    def.name = v.name;
                    def.displayName = v.name;
                    def.description = v.description;
                    def.baseColor = ToColor32(v.base_color);
                    def.isTransparent = v.is_transparent;
                    
                    // 确保faceTextures数组被正确初始化
                    def.faceTextures = new VoxelDefinition.FaceTexture[6];
                    for (int i = 0; i < 6; i++)
                    {
                        def.faceTextures[i] = new VoxelDefinition.FaceTexture();
                    }
                    
                    // 先处理面贴图
                    Texture2D firstFaceTex = null;
                    if (v.face_textures != null && v.face_textures.Length > 0)
                    {
                        for (int i = 0; i < Math.Min(6, v.face_textures.Length); i++)
                        {
                            if (!string.IsNullOrEmpty(v.face_textures[i]))
                            {
                                var faceTex = LoadTextureFile(v.face_textures[i]);
                                def.faceTextures[i].texture = faceTex;
                                // 记录第一个有效的面贴图
                                if (firstFaceTex == null && faceTex != null)
                                {
                                    firstFaceTex = faceTex;
                                }
                            }
                        }
                    }
                    
                    // 设置默认贴图
                    if (!string.IsNullOrEmpty(v.texture))
                    {
                        def.texture = LoadTextureFile(v.texture);
                        //Debug.Log($"[VoxelJsonDB] Loading texture for {v.name}: {v.texture}");
                    }
                    // 如果没有默认贴图但有面贴图，使用第一个面贴图作为默认贴图
                    else if (firstFaceTex != null)
                    {
                        def.texture = firstFaceTex;
                        //Debug.Log($"[VoxelJsonDB] Using first face texture as default texture for {v.name}");
                    }
                    
                    // 更新所有贴图索引
                    def.UpdateTextureIfNeeded();

                    // 修改注册逻辑：特殊处理ID为0的情况
                    ushort assignedId;
                    if (v.id == 0)
                    {
                        // 对于ID为0的体素（air），强制使用ID 0
                        assignedId = VoxelRegistry.RegisterWithId(def, 0);
                    }
                    else
                    {
                        // 其他情况保持原有逻辑
                        assignedId = v.id > 0 ? 
                            VoxelRegistry.RegisterWithId(def, (ushort)v.id) : 
                            VoxelRegistry.Register(def);
                    }
                    
                    // 检查注册是否成功
                    if (assignedId == 0 && v.id > 0)
                    {
                        Debug.LogError($"[VoxelJsonDB] Failed to register voxel '{v.name}' with ID {v.id}");
                        continue; // 跳过此体素
                    }
                    
                    // 更新数据库中的 ID（以防是新分配的）
                    v.id = assignedId;
                    //Debug.Log($"[VoxelJsonDB] Registered voxel '{v.name}' with ID {assignedId}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[VoxelJsonDB] Error processing voxel {v.name}: {ex.Message}\nStackTrace: {ex.StackTrace}");
                }
            }
            
            // 保存更新后的 ID 到数据库
            SaveDatabase();
        }

        /// <summary>
        /// 刷新数据库，重新加载JSON和所有体素
        /// </summary>
        public void RefreshDatabase()
        {
            if (!_isInitialized) return;
            
            // 清除旧的体素
            VoxelRegistry.Clear();
            
            // 重新加载
            LoadDatabase();
        }

        /// <summary>
        /// 添加新的体素到数据库
        /// </summary>
        public ushort AddVoxel(string name, Color32 baseColor, Texture2D tex, string description = "", bool isTransparent = false)
        {
            if (_cache == null || !_isInitialized)
            {
                Debug.LogError("[VoxelJsonDB] Database not initialized!");
                return 0;
            }
            
            // 检查名称是否为空
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogError("[VoxelJsonDB] Cannot add voxel with empty name!");
                return 0;
            }

            try
            {
                // 生成唯一的名字
                string uniqueName = GenerateUniqueName(name);
                if (uniqueName != name)
                {
                    Debug.Log($"[VoxelJsonDB] Name '{name}' already exists, using '{uniqueName}' instead");
                }
                
                // 创建新的体素条目
                var entry = new VoxelEntry
                {
                    name = uniqueName,
                    texture = tex != null ? tex.name + ".png" : "",
                    face_textures = new string[6],
                    base_color = new int[] { baseColor.r, baseColor.g, baseColor.b },
                    description = description,
                    is_transparent = isTransparent
                };
                
                // 生成ScriptableObject并注册
                var def = ScriptableObject.CreateInstance<VoxelDefinition>();
                def.name = uniqueName;
                def.displayName = uniqueName;
                def.baseColor = baseColor;
                def.description = description;
                def.isTransparent = isTransparent;
                def.texture = tex;
                def.UpdateTextureIfNeeded();
                
                // 让 VoxelRegistry 自动分配 typeId
                ushort typeId = VoxelRegistry.Register(def);
                
                // 检查注册是否成功
                if (typeId == 0)
                {
                    Debug.LogError($"[VoxelJsonDB] Failed to register voxel '{uniqueName}'");
                    UnityEngine.Object.Destroy(def); // 清理未使用的对象
                    return 0;
                }
                
                // 更新数据库中的 ID
                entry.id = typeId;
                
                // 添加到数据库
                _cache.voxels.Add(entry);
                
                // 更新revision和保存
                _cache.revision = DateTime.UtcNow.ToString("o");
                SaveDatabase();

                return typeId;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoxelJsonDB] Failed to add voxel {name}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 添加新的体素到数据库（支持6个面不同纹理）
        /// </summary>
        public ushort AddVoxelWithFaces(string name, Color32 baseColor, Texture2D[] faceTextures = null, string description = "", bool isTransparent = false)
        {
            if (_cache == null || !_isInitialized)
            {
                Debug.LogError("[VoxelJsonDB] Database not initialized!");
                return 0;
            }
            
            // 检查名称是否为空
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogError("[VoxelJsonDB] Cannot add voxel with empty name!");
                return 0;
            }

            if (faceTextures == null || faceTextures.Length != 6)
            {
                Debug.LogError("[VoxelJsonDB] faceTextures must be an array of 6 textures!");
                return 0;
            }

            try
            {
                // 生成唯一的名字
                string uniqueName = GenerateUniqueName(name);
                if (uniqueName != name)
                {
                    Debug.Log($"[VoxelJsonDB] Name '{name}' already exists, using '{uniqueName}' instead");
                }
                
                // 创建新的体素条目
                var entry = new VoxelEntry
                {
                    name = uniqueName,
                    texture = faceTextures[0] != null ? faceTextures[0].name + ".png" : "",
                    face_textures = new string[6],
                    base_color = new int[] { baseColor.r, baseColor.g, baseColor.b },
                    description = description,
                    is_transparent = isTransparent
                };
                
                // 设置面纹理文件名
                for (int i = 0; i < 6; i++)
                {
                    entry.face_textures[i] = faceTextures[i] != null ? faceTextures[i].name + ".png" : "";
                }
                
                // 生成ScriptableObject并注册
                var def = ScriptableObject.CreateInstance<VoxelDefinition>();
                def.name = uniqueName;
                def.displayName = uniqueName;
                def.baseColor = baseColor;
                def.description = description;
                def.isTransparent = isTransparent;
                def.texture = faceTextures[0]; // 使用第一个面作为默认纹理
                
                // 设置面纹理
                def.faceTextures = new VoxelDefinition.FaceTexture[6];
                for (int i = 0; i < 6; i++)
                {
                    def.faceTextures[i] = new VoxelDefinition.FaceTexture();
                    def.faceTextures[i].texture = faceTextures[i];
                }
                
                // 更新纹理索引
                def.UpdateTextureIfNeeded();
                
                // 让 VoxelRegistry 自动分配 typeId
                ushort typeId = VoxelRegistry.Register(def);
                
                // 检查注册是否成功
                if (typeId == 0)
                {
                    Debug.LogError($"[VoxelJsonDB] Failed to register voxel '{uniqueName}'");
                    UnityEngine.Object.Destroy(def); // 清理未使用的对象
                    return 0;
                }
                
                // 更新数据库中的 ID
                entry.id = typeId;
                
                // 添加到数据库
                _cache.voxels.Add(entry);
                
                // 更新revision和保存
                _cache.revision = DateTime.UtcNow.ToString("o");
                SaveDatabase();

                return typeId;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoxelJsonDB] Failed to add voxel with faces {name}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 更新体素的面贴图
        /// </summary>
        public bool UpdateVoxelFaceTextures(ushort typeId, Texture2D[] faceTextures)
        {
            if (_cache == null || !_isInitialized)
            {
                Debug.LogError("[VoxelJsonDB] Database not initialized!");
                return false;
            }
            
            try
            {
                // 查找对应的体素条目
                VoxelEntry entry = null;
                foreach (var v in _cache.voxels)
                {
                    if (v.id == typeId)
                    {
                        entry = v;
                        break;
                    }
                }
                
                if (entry == null)
                {
                    Debug.LogError($"[VoxelJsonDB] Voxel with ID {typeId} not found!");
                    return false;
                }
                
                // 获取VoxelDefinition
                var def = VoxelRegistry.GetDefinition(typeId);
                if (def == null)
                {
                    Debug.LogError($"[VoxelJsonDB] VoxelDefinition with ID {typeId} not found!");
                    return false;
                }
                
                // 确保face_textures数组存在
                if (entry.face_textures == null || entry.face_textures.Length != 6)
                {
                    entry.face_textures = new string[6];
                }
                
                // 更新贴图
                for (int i = 0; i < Math.Min(6, faceTextures.Length); i++)
                {
                    if (faceTextures[i] != null)
                    {
                        // 更新JSON条目
                        entry.face_textures[i] = faceTextures[i].name + ".png";
                        
                        // 更新VoxelDefinition
                        if (def.faceTextures[i] == null)
                            def.faceTextures[i] = new VoxelDefinition.FaceTexture();
                            
                        def.faceTextures[i].texture = faceTextures[i];
                    }
                }
                
                // 更新贴图索引
                def.UpdateTextureIfNeeded();
                
                // 保存数据库
                _cache.revision = DateTime.UtcNow.ToString("o");
                SaveDatabase();
                
                Debug.Log($"[VoxelJsonDB] Updated face textures for voxel {entry.name} (ID: {typeId})");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoxelJsonDB] Failed to update face textures for voxel {typeId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 保存数据库到JSON文件
        /// </summary>
        public void SaveDatabase()
        {
            try
            {
                string json = JsonUtility.ToJson(_cache, true);
                File.WriteAllText(_jsonPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoxelJsonDB] Failed to save database: {ex.Message}");
            }
        }

        #region ————— 辅助方法 —————
        /// <summary>
        /// 为重复的名字生成一个唯一的名字，通过添加数字后缀
        /// </summary>
        private string GenerateUniqueName(string baseName)
        {
            if (_cache == null) return baseName;

            // 如果名字不存在，直接返回
            if (!_cache.voxels.Any(v => v.name == baseName))
                return baseName;

            int suffix = 1;
            string newName;
            
            // 循环尝试添加数字后缀，直到找到一个不存在的名字
            do
            {
                newName = $"{baseName}_{suffix}";
                suffix++;
            } while (_cache.voxels.Any(v => v.name == newName));

            return newName;
        }

        private void CopyTemplateIfExists()
        {
            string templatePath = Path.Combine(Application.streamingAssetsPath, "VoxelsDB/voxel_definitions.json");
            
            if (File.Exists(templatePath))
            {
                Debug.Log("[VoxelJsonDB] Creating database from template");
                File.Copy(templatePath, _jsonPath, true);
                
                // 不再需要复制纹理文件，因为纹理已经在 Resources/VoxelTextures 中
                Debug.Log("[VoxelJsonDB] Using textures from Resources/VoxelTextures");
            }
            else
            {
                Debug.Log("[VoxelJsonDB] Creating new empty database");
                VoxelDB newDb = new VoxelDB();
                string json = JsonUtility.ToJson(newDb, true);
                File.WriteAllText(_jsonPath, json);
            }
        }

        private Texture2D LoadTextureFile(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    return null;
                }

                // 从 Resources/VoxelTextures 加载纹理
                string resourcePath = $"VoxelTextures/{Path.GetFileNameWithoutExtension(fileName)}";
                //Debug.Log($"[VoxelJsonDB] Attempting to load texture from Resources path: {resourcePath}");
                
                Texture2D originalTex = Resources.Load<Texture2D>(resourcePath);
                
                if (originalTex != null)
                {
                    // 创建可读的副本
                    Texture2D readableTex = new Texture2D(originalTex.width, originalTex.height, TextureFormat.RGBA32, false);
                    RenderTexture rt = RenderTexture.GetTemporary(originalTex.width, originalTex.height);
                    rt.filterMode = FilterMode.Point;
                    
                    // 将原始贴图渲染到 RenderTexture
                    Graphics.Blit(originalTex, rt);
                    
                    // 保存当前的 RenderTexture
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = rt;
                    
                    // 读取像素数据
                    readableTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    readableTex.Apply();
                    
                    // 恢复 RenderTexture
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(rt);
                    
                    // 设置贴图属性
                    readableTex.filterMode = FilterMode.Point;
                    readableTex.wrapMode = TextureWrapMode.Repeat;
                    readableTex.name = Path.GetFileNameWithoutExtension(fileName);
                    
                    return readableTex;
                }
                else 
                {
                    Debug.LogError($"[VoxelJsonDB] Failed to load texture from Resources: {resourcePath}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoxelJsonDB] Error loading texture {fileName}: {ex.Message}");
                return null;
            }
        }
        
        private static Color32 ToColor32(int[] rgb)
        {
            if (rgb == null || rgb.Length < 3) return Color.white;
            return new Color32((byte)rgb[0], (byte)rgb[1], (byte)rgb[2], 255);
        }
        #endregion

        public IReadOnlyList<VoxelEntry> GetAllVoxelEntries()
        {
            if (_cache == null || !_isInitialized)
            {
                Debug.LogError("[VoxelJsonDB] Database not initialized!");
                return new List<VoxelEntry>();
            }
            return _cache.voxels;
        }

        /// <summary>
        /// 删除指定ID的体素
        /// </summary>
        /// <param name="typeId">要删除的体素ID</param>
        /// <returns>是否删除成功</returns>
        public bool DeleteVoxel(ushort typeId)
        {
            if (_cache == null || !_isInitialized)
            {
                Debug.LogError("[VoxelJsonDB] Database not initialized!");
                return false;
            }

            try
            {
                // 保护基础体素类型（Air和基础方块）
                if (typeId == 0 || typeId == 1)
                {
                    Debug.LogWarning($"[VoxelJsonDB] Cannot delete essential voxel type with ID {typeId}!");
                    return false;
                }

                // 查找要删除的体素
                var entry = _cache.voxels.Find(v => v.id == typeId);
                if (entry == null)
                {
                    Debug.LogError($"[VoxelJsonDB] Voxel with ID {typeId} not found!");
                    return false;
                }

                // 保存要删除的体素信息
                string voxelName = entry.name;
                
                // 从数据库中移除
                _cache.voxels.Remove(entry);

                // 从Registry中注销
                VoxelRegistry.Unregister(typeId);

                // 保存数据库更改
                _cache.revision = DateTime.UtcNow.ToString("o");
                SaveDatabase();

                Debug.Log($"[VoxelJsonDB] Successfully deleted voxel '{voxelName}' with ID {typeId}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoxelJsonDB] Failed to delete voxel {typeId}: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return false;
            }
        }
    }
} 