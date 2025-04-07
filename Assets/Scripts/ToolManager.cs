using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ToolManager : MonoBehaviour
{
    public Tool[] tools; // 所有工具
    private Tool currentTool; // 当前工具
    [SerializeField] private int currentIndex = 0; // 当前工具的索引
    private PlayerInput playerInput;
    private InputAction switchToolAction;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogError("PlayerInput component not found! Please add a PlayerInput component to the ToolManager object.");
            return;
        }

        // 获取切换工具的输入动作
        //switchToolAction = playerInput.actions["SwitchTool"];
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
        if (context.action.name == "SwitchTool")
        {
           if (context.phase == InputActionPhase.Performed) // 确保输入被执行
        {
            // 读取滚轮输入值
            float scrollValue = context.ReadValue<float>();
            Debug.Log("Scroll value: " + scrollValue);

            // 根据滚轮值进行切换
            if (scrollValue > 0f) // 滚轮向上
            {
                SwitchTool((currentIndex + 1) % tools.Length); // 切换到下一个工具
                Debug.Log("Switching to next tool");
            }
            else if (scrollValue < 0f) // 滚轮向下
            {
                SwitchTool((currentIndex - 1 + tools.Length) % tools.Length); // 切换到上一个工具
                Debug.Log("Switching to previous tool");
            }
            
        }
        }
    }

    private void Start()
    {
        // 默认激活第一个工具
        SwitchTool(0);
    }

    private void Update()
    {
         // 切换工具：数字键 1-4
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchTool(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchTool(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchTool(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchTool(3);
    }

    // 切换工具
    public void SwitchTool(int index)
    {
        if (currentTool != null)
        {
            currentTool.DeactivateTool(); // 禁用当前工具
        }

        currentIndex = index;
        currentTool = tools[currentIndex]; // 获取新的工具
        currentTool.ActivateTool(); // 激活新工具
    }

    // 使用当前工具
    public void UseCurrentTool()
    {
        if (currentTool != null)
        {
            currentTool.UseTool();
        }
    }
}
