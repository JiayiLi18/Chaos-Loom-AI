using UnityEngine;
using UnityEngine.InputSystem;

public class UIStateManager : MonoBehaviour
{
    [Header("Input Settings")]
    [SerializeField] private InputActionReference toggleAction; // Tab键的输入动作

    [Header("UI Objects")]
    [SerializeField] private static GameObject[] uiElementsToToggle; // 需要切换显示的UI对象
    public static bool currentGameplayMode = true;

    private void Awake()
    {
        if (toggleAction != null)
        {
            toggleAction.action.Enable();
            toggleAction.action.performed += OnTogglePerformed;
        }
        else
        {
            Debug.LogError("Toggle action not assigned in UIStateManager!");
        }
        uiElementsToToggle = GameObject.FindGameObjectsWithTag("ToggleTarget");

        // 初始状态设置
        SetGameplayState(true);
    }

    private void OnDestroy()
    {
        if (toggleAction != null)
        {
            toggleAction.action.performed -= OnTogglePerformed;
            toggleAction.action.Disable();
        }
    }

    private void OnTogglePerformed(InputAction.CallbackContext context)
    {
        SetGameplayState(!PlayerMove.canLook);
    }

    public static void SetGameplayState(bool isGameplayMode)
    {
        PlayerMove.canLook = isGameplayMode;
        Cursor.visible = !isGameplayMode;
        Cursor.lockState = isGameplayMode ? CursorLockMode.Locked : CursorLockMode.None;

        if (uiElementsToToggle != null)
        {
            // 切换UI元素的显示状态
            foreach (GameObject uiElement in uiElementsToToggle)
            {
                if (uiElement != null)
                {
                    uiElement.SetActive(!isGameplayMode);
                }
            }
        }
        currentGameplayMode = isGameplayMode;
    }
}