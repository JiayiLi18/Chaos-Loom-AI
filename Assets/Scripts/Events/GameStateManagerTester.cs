using UnityEngine;

/// <summary>
/// GameStateManager 自动监控功能测试器
/// 用于验证自动监控功能是否正常工作
/// </summary>
public class GameStateManagerTester : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private GameStateManager gameStateManager;
    [SerializeField] private bool autoFindGameStateManager = true;
    
    [Header("Test Controls")]
    [SerializeField] private bool testManualUpdate = false;

    private void Start()
    {
        if (autoFindGameStateManager && gameStateManager == null)
        {
            gameStateManager = FindFirstObjectByType<GameStateManager>();
            if (gameStateManager == null)
            {
                Debug.LogError("GameStateManagerTester: Could not find GameStateManager in scene!");
                enabled = false;
                return;
            }
        }

        if (gameStateManager == null)
        {
            Debug.LogError("GameStateManagerTester: GameStateManager reference is null!");
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        if (testManualUpdate)
        {
            gameStateManager.PerformAutoMonitoring();
            testManualUpdate = false;
        }
    }


    [ContextMenu("Test Manual Update")]
    private void TestManualUpdate()
    {
        Debug.Log("=== Testing Manual Update ===");
        gameStateManager.PerformAutoMonitoring();
        Debug.Log("=== Manual Update Test Complete ===");
    }
}
