using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class PlayerMove : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float groundCheckDistance = 0.3f;
    [SerializeField] private LayerMask groundLayer; // 用于地面检测的层
    [SerializeField] private float flySpeed = 10f;
    [SerializeField] private float flyAcceleration = 5f; // 飞行加速度
    [SerializeField] private float flyDeceleration = 3f; // 飞行减速度
    [SerializeField] private float doubleTapTimeThreshold = 0.3f; // 双击检测的时间阈值
    public static bool canLook = true; // 是否可以自如旋转相机
    private float smoothSpeed = 10f; // 插值速度，可根据需要调整
    [SerializeField] private bool isUnlocked = false;//如果在不能自由移动相机的情况下，是否解锁了相机

    public enum MovementMode
    {
        Walking,
        Flying
    }
    
    [SerializeField] private MovementMode currentMode = MovementMode.Walking;

    private Vector2 moveInput;
    private Vector2 mouseInput;
    private Rigidbody rb;
    private Camera mainCamera;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction JumpAction;
    private InputAction lookLockerAction;
    private InputAction verticalMoveAction; // 垂直移动控制
    private Vector3 currentFlyVelocity; // 当前飞行速度

    // 用于跟踪相机旋转
    private float currentRotationX = 0f;
    private float currentRotationY = 0f;

    private float lastJumpTime; // 上次跳跃时间
    private bool isWaitingForSecondJump; // 是否在等待第二次跳跃

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
        JumpAction = playerInput.actions["Jump"];
        lookLockerAction = playerInput.actions["LookWhenLocked"];
        verticalMoveAction = playerInput.actions["VerticalMove"]; // 获取垂直移动动作
    }

    void OnEnable()
    {
        // 订阅锁定相机动作
        lookLockerAction.performed += OnLockerPressed;
        lookLockerAction.canceled += OnLockerReleased;

        // 启用输入动作
        moveAction.Enable();
        lookAction.Enable();
        JumpAction.Enable();
        lookLockerAction.Enable();
        verticalMoveAction.Enable(); // 启用垂直移动
    }

    void OnDisable()
    {
        // 禁用输入动作
        moveAction.Disable();
        lookAction.Disable();
        JumpAction.Disable();
        lookLockerAction.Disable();
        verticalMoveAction.Disable(); // 禁用垂直移动
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

        isUnlocked=false;

        // 设置默认的地面检测层
        if (groundLayer == 0)
            groundLayer = LayerMask.GetMask("Voxel");
    }

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

    void Update()
    {
        // 如果正在与UI交互，禁用所有玩家输入
        if (IsInteractingWithUI())
        {
            moveInput = Vector2.zero;
            mouseInput = Vector2.zero;
            return;
        }

        // 获取输入值
        moveInput = moveAction.ReadValue<Vector2>();
        mouseInput = lookAction.ReadValue<Vector2>();

        // 检查跳跃输入
        if (JumpAction.triggered)
        {
            float currentTime = Time.time;
            
            if (!isWaitingForSecondJump)
            {
                // 第一次跳跃
                lastJumpTime = currentTime;
                isWaitingForSecondJump = true;

                if (currentMode == MovementMode.Walking && IsGrounded())
                {
                    Jump();
                }
            }
            else
            {
                // 检查是否是双击
                if (currentTime - lastJumpTime <= doubleTapTimeThreshold)
                {
                    // 双击成功，切换模式
                    ToggleMovementMode();
                    isWaitingForSecondJump = false;
                }
                else
                {
                    // 超时，重置为新的第一次跳跃
                    lastJumpTime = currentTime;
                    if (currentMode == MovementMode.Walking && IsGrounded())
                    {
                        Jump();
                    }
                    else if (currentMode == MovementMode.Flying)
                    {
                        FlyUp();
                    }
                }
            }
        }

        // 重置双击检测
        if (isWaitingForSecondJump && Time.time - lastJumpTime > doubleTapTimeThreshold)
        {
            isWaitingForSecondJump = false;
        }
    }

    void FixedUpdate()
    {
        // 如果正在与UI交互，禁用所有玩家输入
        if (IsInteractingWithUI())
        {
            return;
        }

        // 移动角色
        Move();

        // 处理鼠标视角
        if (!EventSystem.current.IsPointerOverGameObject())
        {
            HandleMouseLook();
        }
    }

    private void Move()
    {
        Vector3 movement = new Vector3(moveInput.x, 0f, moveInput.y);
        float verticalInput = currentMode == MovementMode.Flying ? verticalMoveAction.ReadValue<float>() : 0f;

        if (movement != Vector3.zero || (currentMode == MovementMode.Flying && verticalInput != 0))
        {
            Vector3 cameraForward = mainCamera.transform.forward;
            Vector3 cameraRight = mainCamera.transform.right;

            if (currentMode == MovementMode.Walking)
            {
                cameraForward.y = 0;
                cameraRight.y = 0;
                cameraForward.Normalize();
                cameraRight.Normalize();
            }

            Vector3 moveDirection = cameraForward * movement.z + cameraRight * movement.x;
            
            if (currentMode == MovementMode.Walking)
            {
                moveDirection.y = 0;
                rb.MovePosition(rb.position + moveDirection * moveSpeed * Time.fixedDeltaTime);
            }
            else if (currentMode == MovementMode.Flying)
            {
                moveDirection.y = verticalInput;
                moveDirection.Normalize();
                
                // 计算目标速度
                Vector3 targetVelocity = moveDirection * flySpeed;
                
                // 使用加速度平滑过渡到目标速度
                currentFlyVelocity = Vector3.Lerp(
                    currentFlyVelocity,
                    targetVelocity,
                    flyAcceleration * Time.fixedDeltaTime
                );
                
                rb.velocity = currentFlyVelocity;
            }
        }
        else if (currentMode == MovementMode.Flying)
        {
            // 在没有输入时，平滑减速到停止
            currentFlyVelocity = Vector3.Lerp(
                currentFlyVelocity,
                Vector3.zero,
                flyDeceleration * Time.fixedDeltaTime
            );
            rb.velocity = currentFlyVelocity;
        }
    }

    private void HandleMouseLook()
    {
        // 旋转相机
        if (mainCamera != null)
        {
            if (canLook || (!canLook && isUnlocked))
            {
                // 更新旋转值
                currentRotationX -= mouseInput.y * mouseSensitivity;
                currentRotationY += mouseInput.x * mouseSensitivity;

                // 限制垂直旋转角度
                currentRotationX = Mathf.Clamp(currentRotationX, -70f, 70f);

                // 应用旋转
                Quaternion targetRotation = Quaternion.Euler(currentRotationX, currentRotationY, 0f);

                // 平滑插值至目标旋转，Time.deltaTime保证帧率独立性
                mainCamera.transform.rotation = Quaternion.Lerp(mainCamera.transform.rotation, targetRotation, Time.deltaTime * smoothSpeed);
            }
        }
    }

    private void OnLockerPressed(InputAction.CallbackContext context)
    {
        if (!canLook)
        {
            isUnlocked=true;//解锁了所以可以旋转相机
        }
    }

    private void OnLockerReleased(InputAction.CallbackContext context)
    { 
        if (!canLook)
        {
            isUnlocked=false;//锁定了所以不能旋转相机
        }
    }

    private void Jump()
    {
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private void FlyUp()
    {
        if (currentMode == MovementMode.Flying)
        {
            rb.AddForce(Vector3.up * flySpeed, ForceMode.Impulse);
        }
    }

    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);
    }

    private void ToggleMovementMode()
    {
        currentMode = (currentMode == MovementMode.Walking) ? MovementMode.Flying : MovementMode.Walking;
        
        if (currentMode == MovementMode.Walking)
        {
            rb.useGravity = true;
            currentFlyVelocity = Vector3.zero; // 重置飞行速度
        }
        else
        {
            rb.useGravity = false;
            rb.velocity = Vector3.zero;
            currentFlyVelocity = Vector3.zero; // 初始化飞行速度
        }
    }
}
