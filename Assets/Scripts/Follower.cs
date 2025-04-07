using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

public class Follower : MonoBehaviour
{
    [Header("跟随设置")]
    [Tooltip("Following target")]
    public bool canFollow = true; // 是否跟随玩家
    public Transform player;
    public float followDistance = 3f;
    public float updateInterval = 0.5f;

    [Header("转向设置")]
    public bool canFacePlayer;         // 是否面向玩家
    public float rotationSpeed = 5f;    // 面向玩家的旋转速度
    public float facePlayerDistance = 2f; // 触发面向玩家的距离

    [Header("交互设置")]
    public bool isInteractable = true; // 是否可交互
    public float interactionRadius = 2f; // 交互触发半径
    public KeyCode interactionKey = KeyCode.E;
    public GameObject interactionPrompt; // 交互提示UI
    public UnityEvent onInteract;        // 交互事件

    private NavMeshAgent agent;
    private float lastUpdateTime;
    private bool isInInteractionRange;  // 是否在交互范围内


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
}