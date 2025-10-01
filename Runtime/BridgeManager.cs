using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BridgeMessage
{
    public string type;
    public string action;
    public BridgeData data;
    public long timestamp;
}

[System.Serializable]
public class BridgeData
{
    public string target;
    public bool visible;
    public string animation;
    public string message;
    public string value;
}

// Flutter → Unity 메시지용 (필요시 사용)
[System.Serializable]
public class FlutterMessage
{
    public string type;
    public string action;
    public Dictionary<string, object> data;
    public long timestamp;
}

public class BridgeManager : MonoBehaviour
{
    private WebViewObject webViewObject;
    private bool bridgeReady = false;
    private Queue<string> messageQueue = new Queue<string>();

    // 위젯 상태 추적을 위한 Dictionary
    private Dictionary<string, bool> widgetStates = new Dictionary<string, bool>();

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public void SetWebView(WebViewObject webView)
    {
        webViewObject = webView;
    }

    public void SetBridgeReady(bool ready)
    {
        bridgeReady = ready;
        if (ready)
        {
            SendQueuedMessages();
        }
    }

    private void SendQueuedMessages()
    {
        while (messageQueue.Count > 0 && bridgeReady)
        {
            string js = messageQueue.Dequeue();
            if (webViewObject != null)
            {
                webViewObject.EvaluateJS(js);
            }
        }
    }

    public void SendToFlutter(string action, Dictionary<string, object> data)
    {
        var bridgeData = new BridgeData();

        if (data != null)
        {
            if (data.ContainsKey("target")) bridgeData.target = data["target"].ToString();
            if (data.ContainsKey("visible")) bridgeData.visible = (bool)data["visible"];
            if (data.ContainsKey("animation")) bridgeData.animation = data["animation"].ToString();
            if (data.ContainsKey("message")) bridgeData.message = data["message"].ToString();
            if (data.ContainsKey("value")) bridgeData.value = data["value"].ToString();
        }

        var message = new BridgeMessage
        {
            type = "command",
            action = action,
            data = bridgeData,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        string jsonMessage = JsonUtility.ToJson(message);
        string js = $"window.receiveFromUnity(`{jsonMessage}`)";

        if (bridgeReady && webViewObject != null)
        {
            webViewObject.EvaluateJS(js);
        }
        else
        {
            messageQueue.Enqueue(js);
        }
    }

    // Flutter → Unity 메시지 수신 (필요시 사용)
    public void OnFlutterMessage(string jsonMessage)
    {
        try
        {
            var message = JsonUtility.FromJson<FlutterMessage>(jsonMessage);
            // 필요한 경우 여기에 Flutter 메시지 처리 로직 추가
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing Flutter message: {e.Message}");
        }
    }

    // === 위젯 제어 메서드들 ===

    /// <summary>
    /// 위젯 상태를 자동으로 토글 (권장)
    /// </summary>
    public void ToggleWidgetState(string widgetId, string animation = "fade")
    {
        if (!widgetStates.ContainsKey(widgetId))
        {
            widgetStates[widgetId] = false; // 초기값: 숨김
        }

        bool newState = !widgetStates[widgetId];
        widgetStates[widgetId] = newState;

        var data = new Dictionary<string, object>
        {
            {"target", widgetId},
            {"visible", newState},
            {"animation", animation}
        };
        SendToFlutter("toggleWidget", data);
    }

    /// <summary>
    /// 위젯을 지정된 상태로 설정
    /// </summary>
    public void SetWidgetVisibility(string widgetId, bool visible, string animation = "fade")
    {
        var data = new Dictionary<string, object>
        {
            {"target", widgetId},
            {"visible", visible},
            {"animation", animation}
        };
        SendToFlutter("toggleWidget", data);
    }

    /// <summary>
    /// 위젯 스타일 변경
    /// </summary>
    public void ChangeWidgetStyle(string widgetId, Dictionary<string, object> styles)
    {
        var data = new Dictionary<string, object> { {"target", widgetId} };
        foreach(var style in styles)
        {
            data[style.Key] = style.Value;
        }
        SendToFlutter("changeStyle", data);
    }

    // === Unity Inspector 테스트 버튼용 메서드들 ===
    [Header("Test Settings")]
    public string testWidgetId = "testWidget";

    /// <summary>
    /// 위젯 토글 테스트 (권장)
    /// </summary>
    public void TestToggleWidget()
    {
        ToggleWidgetState(testWidgetId, "fade");
    }

    /// <summary>
    /// 위젯 강제 표시 테스트
    /// </summary>
    public void TestShowWidget()
    {
        SetWidgetVisibility(testWidgetId, true, "fadeIn");
    }

    /// <summary>
    /// 위젯 강제 숨김 테스트
    /// </summary>
    public void TestHideWidget()
    {
        SetWidgetVisibility(testWidgetId, false, "fadeOut");
    }

    /// <summary>
    /// 스타일 변경 테스트
    /// </summary>
    public void TestChangeStyle()
    {
        var styles = new Dictionary<string, object>
        {
            {"backgroundColor", "#FF0000"},
            {"color", "#FFFFFF"}
        };
        ChangeWidgetStyle(testWidgetId, styles);
    }
}