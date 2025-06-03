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
            Debug.Log($"Voxel created successfully: {message.voxel.voxel_name} (ID: {message.voxel.voxel_id})");
            HandleVoxelCreation();
        }
        else if (message.voxel.executed && !message.voxel.success)
        {
            Debug.LogError($"Voxel creation failed: {message.voxel.error}");
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

    // Add JSON extraction method, try multiple patterns
    private string ExtractJsonCommand(string text)
    {
        // 1. Try to find complete JSON object
        Match jsonMatch = Regex.Match(text, @"\{[\s\S]*?\}");
        if (jsonMatch.Success)
        {
            string potentialJson = jsonMatch.Value;
            // Verify if it contains command field
            if (potentialJson.Contains("\"command\""))
            {
                return potentialJson;
            }
        }
        
        // 2. Try to extract content from json code block
        Match codeBlockMatch = Regex.Match(text, @"```json\s*\n\s*(\{[\s\S]*?\})\s*\n\s*```");
        if (codeBlockMatch.Success && codeBlockMatch.Groups.Count > 1)
        {
            return codeBlockMatch.Groups[1].Value;
        }
        
        // 3. Try to extract from other code blocks
        Match otherCodeMatch = Regex.Match(text, @"```\s*\n\s*(\{[\s\S]*?\})\s*\n\s*```");
        if (otherCodeMatch.Success && otherCodeMatch.Groups.Count > 1)
        {
            string potentialJson = otherCodeMatch.Groups[1].Value;
            if (potentialJson.Contains("\"command\""))
            {
                return potentialJson;
            }
        }
        
        return string.Empty;
    }

    // --------------------------------- Command Processing ---------------------------------
    private void HandleVoxelCreation()
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
        
        // Send success message
        chatUIReference.OnReceiveMessage("Successfully refreshed voxel database, new voxel types have been loaded.");
    }

    // --------------------------------- Data Structures ---------------------------------
    [System.Serializable] private struct BaseCommand
    {
        public string command;
    }

    // Retain basic command structure, detailed data structures no longer needed
}