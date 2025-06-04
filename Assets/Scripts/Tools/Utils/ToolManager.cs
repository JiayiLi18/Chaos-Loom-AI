using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ToolManager : MonoBehaviour
{
    public Tool[] tools; // 所有工具
    private Tool currentTool; // 当前工具
    [SerializeField] private int currentIndex = 0; // 当前工具的索引
    private PlayerInput playerInput;
    private InputAction switchToolAction;

    private bool IsInteractingWithUI()
    {
        // 检查是否有UI元素被选中
        if (EventSystem.current == null) return false;
        
        // 检查当前选中的对象
        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        if (selectedObject == null) return false;

        // 检查是否是输入框或其他需要键盘输入的UI元素
        return selectedObject.GetComponent<TMP_InputField>() != null || 
               selectedObject.GetComponent<InputField>() != null;
    }

    private void Start()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogError("PlayerInput component not found! Please add a PlayerInput component to the ToolManager object.");
            return;
        }

        //启动的时候获取所有工具并确保脚本开启，关闭其他所有并激活第一个
        if(tools.Length > 0)
        {
            foreach (var tool in tools)
            {
                tool.enabled = true;
                tool.DeactivateTool();
                // 设置按钮点击事件
                if (tool.UIButton != null)
                {
                    int toolIndex = System.Array.IndexOf(tools, tool);
                    tool.UIButton.onClick.AddListener(() => SwitchTool(toolIndex));
                }
            }
            SwitchTool(0);
        }
    }

    private void OnEnable()
    {
        if (switchToolAction != null)
    {
        //switchToolAction.performed += OnSwitchTool; // 手动订阅事件
        switchToolAction.Enable();
    }
    }

    private void OnDisable()
    {
        if (switchToolAction != null)
    {
        //switchToolAction.performed -= OnSwitchTool; // 取消订阅事件
        switchToolAction.Disable();
    }
    }

    public void OnSwitchTool(InputAction.CallbackContext context)
    {
        // 如果正在与UI交互，不处理工具切换
        if (IsInteractingWithUI()) return;

        if (context.action.name == "SwitchTool")
        {
            if (context.phase == InputActionPhase.Performed)
            {
                float scrollValue = context.ReadValue<float>();
                Debug.Log("Scroll value: " + scrollValue);

                if (scrollValue > 0f)
                {
                    SwitchTool((currentIndex + 1) % tools.Length);
                    Debug.Log("Switching to next tool");
                }
                else if (scrollValue < 0f)
                {
                    SwitchTool((currentIndex - 1 + tools.Length) % tools.Length);
                    Debug.Log("Switching to previous tool");
                }
            }
        }
    }

    private void Update()
    {
        // 如果正在与UI交互，不处理工具切换
        if (IsInteractingWithUI()) return;

        // 切换工具：数字键 1-4
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchTool(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchTool(2);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchTool(3);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchTool(4);
        if (Input.GetKeyDown(KeyCode.Alpha0)) SwitchTool(0);
    }

    // 切换工具
    public void SwitchTool(int index)
    {
        if (index < 0 || index >= tools.Length) return;

        if (currentTool != null)
        {
            currentTool.DeactivateTool();
        }

        currentIndex = index;
        currentTool = tools[currentIndex];
        currentTool.ActivateTool();
    }
}
