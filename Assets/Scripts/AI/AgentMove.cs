using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

public class AgentMove : MonoBehaviour
{
    [Header("Follow Settings")]
    [Tooltip("Following target")]
    public bool canFollow = true; // 是否跟随玩家
    public Transform player;
    public float followDistance = 3f;
    public float updateInterval = 0.5f;

    [Header("Face Player Settings")]
    public bool canFacePlayer;         // 是否面向玩家
    public float rotationSpeed = 5f;    // 面向玩家的旋转速度
    public float facePlayerDistance = 2f; // 触发面向玩家的距离

    [Header("Interaction Settings")]
    public bool isInteractable = true; // 是否可交互
    public float interactionRadius = 2f; // 交互触发半径
    public KeyCode interactionKey = KeyCode.E;
    public GameObject interactionPrompt; // 交互提示UI
    public UnityEvent onInteract;        // 交互事件

    private NavMeshAgent agent;
    private float lastUpdateTime;
    private bool isInInteractionRange;  // 是否在交互范围内
    
    // 方向检测相关
    public enum DirectionInterval
    {
        North,    // -45° ~ 45° (0°为中心)
        East,     // 45° ~ 135° (90°为中心)
        South,    // 135° ~ 225° (180°为中心)
        West      // 225° ~ 315° (270°为中心)
    }
    
    public struct DirectionAngle
    {
        public float angle;
        public string direction;
        
        public DirectionAngle(float angle, string direction)
        {
            this.angle = angle;
            this.direction = direction;
        }
    }


    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        InitializeComponents();
    }

    void InitializeComponents()
    {
        // 自动添加球形触发器
        SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
        trigger.radius = interactionRadius;
        trigger.isTrigger = true;

        // 初始化UI提示
        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);
    }

    void Update()
    {
        if (canFollow)
        {
            HandleFollowing();
        }
        if(canFacePlayer)
        {
            HandleFacing();
        }
        if (isInteractable)
            HandleInteraction();
    }

    #region 跟随逻辑
    void HandleFollowing()
    {
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            if (player != null && agent.isActiveAndEnabled)
            {
                Vector3 targetPosition = CalculateFollowPosition();
                agent.SetDestination(targetPosition);
                lastUpdateTime = Time.time;
            }
        }

        agent.isStopped = ShouldStopMovement();
    }

    Vector3 CalculateFollowPosition()
    {
        return player.position -
            (player.position - transform.position).normalized * followDistance;
    }

    bool ShouldStopMovement()
    {
        return agent.remainingDistance <= agent.stoppingDistance;
    }
    #endregion

    #region 面向玩家逻辑
    void HandleFacing()
    {
        if (ShouldFacePlayer())
        {
            RotateTowardsPlayer();
            canFacePlayer = true;
        }
        else
        {
            canFacePlayer = false;
        }
    }

    bool ShouldFacePlayer()
    {
        return agent.remainingDistance <= facePlayerDistance &&
               player != null &&
               !agent.pathPending;
    }

    void RotateTowardsPlayer()
    {
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0; // 保持水平旋转

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
    #endregion

    #region 交互逻辑
    void HandleInteraction()
    {
        if (isInInteractionRange && Input.GetKeyDown(interactionKey))
        {
            onInteract.Invoke();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.transform == player)
        {
            isInInteractionRange = true;
            ShowInteractionPrompt(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.transform == player)
        {
            isInInteractionRange = false;
            ShowInteractionPrompt(false);
        }
    }

    void ShowInteractionPrompt(bool show)
    {
        if (interactionPrompt != null)
            interactionPrompt.SetActive(show);
    }
    #endregion
    
    #region 方向检测逻辑
    /// <summary>
    /// 标准化角度到0-360范围
    /// </summary>
    public float NormalizeAngle(float angle)
    {
        while (angle < 0) angle += 360;
        while (angle >= 360) angle -= 360;
        return angle;
    }
    
    /// <summary>
    /// 根据角度判断方向区间
    /// </summary>
    public DirectionInterval GetDirectionInterval(float angle)
    {
        if (angle >= 315 || angle < 45) return DirectionInterval.North;  // -45° ~ 45°
        if (angle >= 45 && angle < 135) return DirectionInterval.East;   // 45° ~ 135°
        if (angle >= 135 && angle < 225) return DirectionInterval.South;  // 135° ~ 225°
        return DirectionInterval.West;  // 225° ~ 315°
    }
    
    /// <summary>
    /// 根据方向区间获取标准角度
    /// </summary>
    public DirectionAngle[] GetStandardAnglesForInterval(DirectionInterval interval)
    {
        switch (interval)
        {
            case DirectionInterval.North: // 当前面向北方
                return new DirectionAngle[]
                {
                    new DirectionAngle(0f, "North"),    // 北
                    new DirectionAngle(90f, "East"),    // 东
                    new DirectionAngle(180f, "South"),   // 南
                    new DirectionAngle(270f, "West")     // 西
                };
            case DirectionInterval.East: // 当前面向东方
                return new DirectionAngle[]
                {
                    new DirectionAngle(90f, "East"),     // 东
                    new DirectionAngle(180f, "South"),   // 南
                    new DirectionAngle(270f, "West"),     // 西
                    new DirectionAngle(0f, "North")       // 北
                };
            case DirectionInterval.South: // 当前面向南方
                return new DirectionAngle[]
                {
                    new DirectionAngle(180f, "South"),    // 南
                    new DirectionAngle(270f, "West"),     // 西
                    new DirectionAngle(0f, "North"),      // 北
                    new DirectionAngle(90f, "East")       // 东
                };
            case DirectionInterval.West: // 当前面向西方
                return new DirectionAngle[]
                {
                    new DirectionAngle(270f, "West"),     // 西
                    new DirectionAngle(0f, "North"),      // 北
                    new DirectionAngle(90f, "East"),      // 东
                    new DirectionAngle(180f, "South")      // 南
                };
            default:
                return new DirectionAngle[]
                {
                    new DirectionAngle(0f, "North"),
                    new DirectionAngle(90f, "East"),
                    new DirectionAngle(180f, "South"),
                    new DirectionAngle(270f, "West")
                };
        }
    }
    
    /// <summary>
    /// 获取当前Agent的方向区间
    /// </summary>
    public DirectionInterval GetCurrentDirectionInterval()
    {
        float currentYRotation = NormalizeAngle(transform.eulerAngles.y);
        return GetDirectionInterval(currentYRotation);
    }
    
    /// <summary>
    /// 获取当前Agent的四方向标准角度
    /// </summary>
    public DirectionAngle[] GetCurrentDirectionAngles()
    {
        DirectionInterval interval = GetCurrentDirectionInterval();
        return GetStandardAnglesForInterval(interval);
    }
    #endregion
}