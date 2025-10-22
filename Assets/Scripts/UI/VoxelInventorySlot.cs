using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Voxels;
using UnityEngine.Events;
using System.Runtime.CompilerServices;

public class VoxelInventorySlot : MonoBehaviour
{
    [SerializeField] private Renderer[] faceRenderers = new Renderer[3];//只需要right,up,front三个面
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image frameImage; //边框颜色
    [SerializeField] private Button editButton;
    
    private VoxelDefinition _voxelDef;
    private bool _isSelected;
    
    // 每面当前颜色/贴图缓存（便于 UI 同步）
    private readonly Color[] _colors = new Color[3];
    private readonly Texture[] _textures = new Texture[3];
    private readonly bool[] _faceTextureMode = new bool[3]; // 每个面是否处于贴图模式
    
    public VoxelDefinition VoxelDef => _voxelDef;
    public ushort slotId; //slot的id就是voxel的唯一typeId

    public event UnityAction<ushort> OnEditClicked;


    private void OnEnable()
    {
        // 初始化材质缓存
        InitializeMaterialCache();

        // 设置编辑按钮点击事件
        if (editButton != null)
        {
            editButton.onClick.AddListener(() => OnEditClicked?.Invoke(slotId));
        }
    }

    private void OnDestroy()
    {
        if (editButton != null)
        {
            editButton.onClick.RemoveAllListeners();
        }
    }

    /// <summary>
    /// 初始化材质缓存
    /// </summary>
    private void InitializeMaterialCache()
    {
        // 初始化每面颜色/贴图（从材质里读）
        for (int i = 0; i < faceRenderers.Length; i++)
        {
            if (faceRenderers[i] == null) continue;
            var mat = faceRenderers[i].material; // 实例化材质，独立改色
            _colors[i] = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") :
                         (mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white);
            _textures[i] = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") :
                           (mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null);
        }
    }

    /// <summary>
    /// 设置面的颜色（参考 Cube3DUI 的 SetFaceColor 方法）
    /// </summary>
    private void SetFaceColor(int faceIndex, Color c)
    {
        if (faceIndex < 0 || faceIndex >= faceRenderers.Length) return;
        
        _colors[faceIndex] = c;

        // 如果当前面是贴图模式，只清除当前面的贴图
        if (_faceTextureMode[faceIndex])
        {
            // 只清除当前面的贴图，保留其他面的贴图
            SetFaceTexture(faceIndex, null);
            _faceTextureMode[faceIndex] = false; // 标记为颜色模式
        }

        var mr = faceRenderers[faceIndex];
        if (mr == null) return;

        var mat = mr.material; // 实例化材质
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        else if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
    }

    /// <summary>
    /// 设置面的贴图（参考 Cube3DUI 的 SetFaceTexture 方法）
    /// </summary>
    private void SetFaceTexture(int faceIndex, Texture2D tex)
    {
        if (faceIndex < 0 || faceIndex >= faceRenderers.Length) return;
        
        _textures[faceIndex] = tex;

        // 标记当前面为贴图模式
        _faceTextureMode[faceIndex] = (tex != null);

        var mr = faceRenderers[faceIndex];
        if (mr == null) return;

        var mat = mr.material;
        
        // 设置纹理
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        
        // 重要：设置纹理时，同时将BaseColor设置为白色，避免颜色残留
        if (tex != null)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        }
    }
    
    public void SetVoxel(VoxelDefinition def)
    {
        _voxelDef = def;
        if (def != null)
        {
            slotId = def.typeId;
            
            // 使用 voxel 的三个面（Right, Up, Front）进行渲染
            // faceRenderers[0] = Right, faceRenderers[1] = Up, faceRenderers[2] = Front
            // 对应 VoxelDefinition 的面顺序：Right(0), Left(1), Up(2), Down(3), Front(4), Back(5)
            
            // 设置 Right 面 (faceRenderers[0] -> def.faceTextures[0])
            if (def.faceTextures != null && def.faceTextures.Length > 0 && def.faceTextures[0] != null)
            {
                if (def.faceTextures[0].texture != null)
                {
                    SetFaceTexture(0, def.faceTextures[0].texture);
                }
                else
                {
                    SetFaceColor(0, def.baseColor);
                }
            }
            else
            {
                SetFaceColor(0, def.baseColor);
            }
            
            // 设置 Up 面 (faceRenderers[1] -> def.faceTextures[2])
            if (def.faceTextures != null && def.faceTextures.Length > 2 && def.faceTextures[2] != null)
            {
                if (def.faceTextures[2].texture != null)
                {
                    SetFaceTexture(1, def.faceTextures[2].texture);
                }
                else
                {
                    SetFaceColor(1, def.baseColor);
                }
            }
            else
            {
                SetFaceColor(1, def.baseColor);
            }
            
            // 设置 Front 面 (faceRenderers[2] -> def.faceTextures[4])
            if (def.faceTextures != null && def.faceTextures.Length > 4 && def.faceTextures[4] != null)
            {
                if (def.faceTextures[4].texture != null)
                {
                    SetFaceTexture(2, def.faceTextures[4].texture);
                }
                else
                {
                    SetFaceColor(2, def.baseColor);
                }
            }
            else
            {
                SetFaceColor(2, def.baseColor);
            }
            
            nameText.text = def.displayName;
            gameObject.SetActive(true);

            // 如果是Air voxel，隐藏编辑按钮
            if (editButton != null)
            {
                editButton.gameObject.SetActive(def.name != "Air");
            }
        }
        else
        {
            Debug.LogWarning("[VoxelInventorySlot] Null VoxelDefinition provided");
            
            // 重置所有面为默认颜色
            for (int i = 0; i < faceRenderers.Length; i++)
            {
                SetFaceColor(i, Color.white);
            }
            
            if (editButton != null)
            {
                editButton.gameObject.SetActive(false);
            }
            gameObject.SetActive(false);
        }
    }
    
    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        frameImage.enabled = selected;
    }
} 