using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float mouseSensitivity = 2f;
    private float smoothSpeed = 10f; // 插值速度，可根据需要调整
    
    private Vector2 moveInput;
    private Vector2 mouseInput;
    private Rigidbody rb;
    private Camera mainCamera;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction lookAction;
    
    // 用于跟踪相机旋转
    private float currentRotationX = 0f;
    private float currentRotationY = 0f;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogError("PlayerInput component not found! Please add a PlayerInput component to the player object.");
            return;
        }

        // 获取输入动作引用
        moveAction = playerInput.actions["Move"];
        lookAction = playerInput.actions["Look"];
    }

    void OnEnable()
    {
        // 启用输入动作
        moveAction.Enable();
        lookAction.Enable();
    }

    void OnDisable()
    {
        // 禁用输入动作
        moveAction.Disable();
        lookAction.Disable();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning("Rigidbody component not found on player. Adding one...");
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            // 初始化相机旋转
            Vector3 currentRotation = mainCamera.transform.rotation.eulerAngles;
            currentRotationX = currentRotation.x;
            currentRotationY = currentRotation.y;
        }
        else
        {
            Debug.LogError("Main camera not found in the scene!");
        }
    }

    void Update()
    {
        // 获取输入值
        moveInput = moveAction.ReadValue<Vector2>();
        mouseInput = lookAction.ReadValue<Vector2>();
    }

    void FixedUpdate()
    {
        // 移动角色
        Move();
        
        // 处理鼠标视角
        HandleMouseLook();
    }

    private void Move()
    {
        // 计算移动方向
        Vector3 movement = new Vector3(moveInput.x, 0f, moveInput.y);
        
        // 如果有输入，则移动角色
        if (movement != Vector3.zero)
        {
            // 根据相机方向调整移动方向
            Vector3 cameraForward = mainCamera.transform.forward;
            Vector3 cameraRight = mainCamera.transform.right;
            
            // 将相机方向投影到水平面上
            cameraForward.y = 0;
            cameraRight.y = 0;
            cameraForward.Normalize();
            cameraRight.Normalize();

            // 计算相对于相机方向的移动
            Vector3 moveDirection = cameraForward * movement.z + cameraRight * movement.x;
            moveDirection.y = 0; // 确保没有垂直移动

            // 移动角色
            rb.MovePosition(rb.position + moveDirection * moveSpeed * Time.fixedDeltaTime);
        }
    }

    private void HandleMouseLook()
    {
        // 旋转相机
        if (mainCamera != null)
        {
            // 更新旋转值
            currentRotationX -= mouseInput.y * mouseSensitivity;
            currentRotationY += mouseInput.x * mouseSensitivity;
            
            // 限制垂直旋转角度
            currentRotationX = Mathf.Clamp(currentRotationX, -80f, 80f);
            
            // 应用旋转
            Quaternion targetRotation = Quaternion.Euler(currentRotationX, currentRotationY, 0f);

            // 平滑插值至目标旋转，Time.deltaTime保证帧率独立性
            mainCamera.transform.rotation = Quaternion.Lerp(mainCamera.transform.rotation, targetRotation, Time.deltaTime * smoothSpeed);
        }
    }
}
