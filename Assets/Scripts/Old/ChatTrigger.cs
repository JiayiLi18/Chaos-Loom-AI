using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;

public class ChatTrigger : MonoBehaviour
{
    public ChatGPTHandler chatHandler;
    public AreaHandler areaHandler;
    public TMP_InputField questionInputField;
    public TMP_Text answerText;
    private Texture2D screenshot;

    public void OnAskButtonClicked()
    {
        string userQuestion = questionInputField.text;
        chatHandler.AskChatGPT(userQuestion, UpdateAnswerText);
    }

    public void OnAskButtonWithImageClicked()
    {
        StartCoroutine(AskButtonWithImageClicked());
    }

    public void UpdateAnswerText(string answer)
    {
        answerText.text = answer;
    }

    private IEnumerator AskButtonWithImageClicked()
    {
        string savePath = Path.Combine(Application.dataPath, "Screenshots", "Screenshot.jpg");
        yield return areaHandler.TakeScreenshot(savePath, areaHandler.calculatedCaptureArea);

        yield return new WaitForEndOfFrame();

        string userQuestion = questionInputField.text;
        screenshot = areaHandler.screenshot;

        // Send the image to GPT-4
        if (screenshot != null)
        {
            chatHandler.AskChatGPT(userQuestion, screenshot, UpdateAnswerText);
        }
        else
        {
            Debug.LogError("Screenshot is null!");
        }
    }
}
