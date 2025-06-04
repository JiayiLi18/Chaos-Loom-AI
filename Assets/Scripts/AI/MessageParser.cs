using UnityEngine;
using System.Text.RegularExpressions;

[System.Serializable]
public struct ParsedMessage
{
    public string answer;
    public TextureCommand texture;
    public VoxelCommand voxel;
    public DatabaseCommand database;
    public bool IsValid => !string.IsNullOrEmpty(answer);
}

[System.Serializable]
public struct TextureCommand
{
    public bool executed;
    public bool success;
    public string texture_path;
    public string error;
}

[System.Serializable]
public struct VoxelCommand
{
    public bool executed;
    public bool success;
    public int voxel_id;
    public string voxel_name;
    public string texture_path;
    public string error;
    public string operation; // 'create' or 'update'
}

[System.Serializable]
public struct DatabaseCommand
{
    public bool executed;
    public bool success;
    public string section;
    public string error;
}

public static class MessageParser 
{
    public static ParsedMessage ParseMessage(string jsonString)
    {
        try 
        {
            // 反序列化基础结构
            var baseData = JsonUtility.FromJson<ResponseData>(jsonString);
            
            if (!baseData.success || baseData.data == null)
            {
                Debug.LogError($"Response indicates failure or null data: {baseData.error}");
                return new ParsedMessage();
            }

            return new ParsedMessage 
            {
                answer = baseData.data.answer,
                texture = baseData.data.commands.texture,
                voxel = baseData.data.commands.voxel,
                database = baseData.data.commands.database
            };
        }
        catch (System.Exception e)
        {
            Debug.LogError($"解析失败: {e.Message}\nJSON: {jsonString}");
            return new ParsedMessage();
        }
    }

    [System.Serializable]
    private class ResponseData
    {
        public bool success;
        public string error;
        public SessionData data;
    }

    [System.Serializable]
    private class SessionData
    {
        public string session_id;
        public string query;
        public string answer;
        public TokenUsage token_usage;
        public Commands commands;
    }

    [System.Serializable]
    private class TokenUsage
    {
        public int prompt_tokens;
        public int completion_tokens;
        public int total_tokens;
    }

    [System.Serializable]
    private class Commands
    {
        public TextureCommand[] textures;
        public VoxelCommand[] voxels;
        public DatabaseCommand database;

        public TextureCommand texture => textures != null && textures.Length > 0 ? textures[0] : new TextureCommand();
        public VoxelCommand voxel => voxels != null && voxels.Length > 0 ? voxels[0] : new VoxelCommand();
    }
}