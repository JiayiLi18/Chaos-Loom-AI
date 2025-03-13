using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class ColorManager : MonoBehaviour
{
    [SerializeField] private List<Color> optimizedColors = new List<Color>();
    
    public void ParseAndStoreColors(string gptResponse)
    {
        optimizedColors.Clear();
        
        string pattern = @"""hex"": ""(#[0-9A-Fa-f]{6})""";
        var matches = Regex.Matches(gptResponse, pattern);
        
        if (matches.Count == 0)
        {
            Debug.LogWarning("No colors found in GPT response!");
            return;
        }
        
        foreach (Match match in matches)
        {
            string hexColor = match.Groups[1].Value;
            Color color;
            if (ColorUtility.TryParseHtmlString(hexColor, out color))
            {
                optimizedColors.Add(color);
            }
        }
    }

    public void UpdateAllLineColors()
    {
        // 查找所有带有"DrawLine"标签的物体
        GameObject[] lines = GameObject.FindGameObjectsWithTag("DrawnLine");
        foreach (GameObject line in lines)
        {
            LineRenderer lineRenderer = line.GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                if(optimizedColors.Count != 0){
                Color newColor = GetRandomOptimizedColor();
                lineRenderer.startColor = newColor;
                lineRenderer.endColor = newColor;}
            }
        }
    }

    public Color GetRandomOptimizedColor()
    {
        if (optimizedColors.Count == 0)
            return Color.white; // 默认颜色
            
        return optimizedColors[UnityEngine.Random.Range(0, optimizedColors.Count)];
    }
} 