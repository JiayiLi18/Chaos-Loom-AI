using System.Text.RegularExpressions;
using UnityEngine;
using Voxels;

/// <summary>
/// Receives raw ChatGPT text, extracts JSON commands and executes them.
/// </summary>
public class AICommandProcessor : MonoBehaviour
{
    [SerializeField] private ChatUI chatUIReference;
    [SerializeField] private VoxelJsonDB voxelDBReference;

    void OnEnable()
    {
        if (chatUIReference == null)
        {
            chatUIReference = FindAnyObjectByType<ChatUI>();
        }
    }

    // --------------------------------- Public Entry ---------------------------------
    public void ProcessResponse(ParsedMessage message)
    {
        if (message.texture.executed && message.texture.success)
        {
            Debug.Log($"Texture generated successfully at: {message.texture.texture_path}");
            // Additional texture handling if needed
        }
        else if (message.texture.executed && !message.texture.success)
        {
            Debug.LogError($"Texture generation failed: {message.texture.error}");
        }

        if (message.voxel.executed && message.voxel.success)
        {
            string operationType = message.voxel.operation == "update" ? "updated" : "created";
            Debug.Log($"Voxel {operationType} successfully: {message.voxel.voxel_name} (ID: {message.voxel.voxel_id})");
            HandleVoxelOperation(message);
        }
        else if (message.voxel.executed && !message.voxel.success)
        {
            Debug.LogError($"Voxel operation failed: {message.voxel.error}");
        }

        if (message.database.executed && message.database.success)
        {
            Debug.Log($"Database operation successful for section: {message.database.section}");
            // Additional database handling if needed
        }
        else if (message.database.executed && !message.database.success)
        {
            Debug.LogError($"Database operation failed: {message.database.error}");
        }
    }

    // --------------------------------- Command Processing ---------------------------------
    private void HandleVoxelOperation(ParsedMessage message)
    {
        // Check VoxelJsonDB reference
        if (voxelDBReference == null)
        {
            Debug.LogError("AICommandProcessor - VoxelJsonDB reference is null, cannot refresh voxel database.");
            return;
        }

        // Refresh Unity asset database to ensure new textures are imported
        #if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
        
        // Make sure the texture is readable
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:texture2d", new[] { "Assets/Resources/VoxelTextures" });
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }
        #endif

        // Refresh database
        voxelDBReference.RefreshDatabase();
        
        // Send success message with operation details
        string operationMessage = $"System: {(message.voxel.operation == "update" ? $"Updated voxel '{message.voxel.voxel_name}'" : $"Created new voxel '{message.voxel.voxel_name}'")} (ID: {message.voxel.voxel_id})";
        chatUIReference.OnReceiveMessage(operationMessage, true);
    }

    // --------------------------------- Data Structures ---------------------------------
    [System.Serializable] private struct BaseCommand
    {
        public string command;
    }

    // Retain basic command structure, detailed data structures no longer needed
}