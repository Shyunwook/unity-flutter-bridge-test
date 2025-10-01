using UnityEngine;
using System.Collections;
using System.IO;

public class WebViewController : MonoBehaviour
{
    private WebViewObject webViewObject;
    private LocalWebServer localServer;
    private BridgeManager bridgeManager;
    private const int SERVER_PORT = 8088;

    void Start()
    {
        StartCoroutine(InitializeWebView());
    }

    IEnumerator InitializeWebView()
    {
        // 모든 플랫폼에서 로컬 HTTP 서버 사용
        GameObject serverObj = new GameObject("LocalWebServer");
        localServer = serverObj.AddComponent<LocalWebServer>();
        localServer.port = SERVER_PORT;

        // Bridge Manager 찾기 (Scene에 있는 기존 것 사용)
        GameObject existingBridgeObj = GameObject.Find("BridgeManager");
        if (existingBridgeObj != null)
        {
            bridgeManager = existingBridgeObj.GetComponent<BridgeManager>();
            Debug.Log("✅ Found existing BridgeManager in scene");
        }
        else
        {
            // 없으면 새로 생성
            GameObject bridgeObj = new GameObject("BridgeManager");
            bridgeManager = bridgeObj.AddComponent<BridgeManager>();
            Debug.Log("⚠️ Created new BridgeManager");
        }

        // 서버가 시작될 때까지 대기
        yield return new WaitForSeconds(1.0f);

        string loadUrl = $"http://localhost:{SERVER_PORT}/flutter/";
        Debug.Log($"Platform: {Application.platform}, Loading URL: {loadUrl}");

        // WebView 오브젝트 생성
        webViewObject = (new GameObject("WebViewObject")).AddComponent<WebViewObject>();

        // Bridge Manager에 WebView 설정
        bridgeManager.SetWebView(webViewObject);

        // WebView 초기화
        webViewObject.Init(
            cb: (msg) =>
            {
                Debug.Log($"WebView Callback: {msg}");

                // JavaScript → Unity 브릿지 콜백 처리
                if (msg.StartsWith("bridge:"))
                {
                    string jsonData = msg.Substring(7);
                    bridgeManager.OnFlutterMessage(jsonData);
                }
            },
            err: (msg) =>
            {
                Debug.LogError($"WebView Error: {msg}");
            },
            httpErr: (msg) =>
            {
                Debug.LogError($"WebView HTTP Error: {msg}");
            },
            started: (msg) =>
            {
                Debug.Log($"WebView Started: {msg}");
            },
            hooked: (msg) =>
            {
                Debug.Log($"WebView Hooked: {msg}");
            },
            ld: (msg) =>
            {
                Debug.Log($"WebView Loaded: {msg}");
                webViewObject.SetVisibility(true);

                // WebView 로드 완료 후 브릿지 초기화
                StartCoroutine(InitializeBridge());
            },
            enableWKWebView: true,
            transparent: false
        );

        // Unity UI를 위한 공간 확보 (상단 300픽셀 여백)
        webViewObject.SetMargins(0, 300, 0, 0);

        // URL 로드
        webViewObject.LoadURL(loadUrl);

        // WebView 표시
        webViewObject.SetVisibility(true);
    }

    IEnumerator InitializeBridge()
    {
        // Flutter 페이지가 완전히 로드될 때까지 대기
        yield return new WaitForSeconds(2.0f);

        string initJS = @"
            // Unity → Flutter 메시지 수신 함수
            window.receiveFromUnity = function(message) {
                try {
                    const parsedMessage = JSON.parse(message);
                    const event = new CustomEvent('unityMessage', {
                        detail: parsedMessage
                    });
                    window.dispatchEvent(event);
                    console.log('Received from Unity:', parsedMessage);
                } catch (e) {
                    console.error('Error parsing Unity message:', e);
                }
            };

            // Flutter → Unity 메시지 전송 함수
            window.sendToUnity = function(message) {
                try {
                    const jsonMessage = JSON.stringify(message);

                    if (window.Unity && window.Unity.call) {
                        // Android
                        window.Unity.call('bridge:' + jsonMessage);
                    } else if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.unityControl) {
                        // iOS
                        window.webkit.messageHandlers.unityControl.postMessage('bridge:' + jsonMessage);
                    } else {
                        console.warn('No Unity bridge available');
                    }
                    
                    console.log('Sent to Unity:', message);
                } catch (e) {
                    console.error('Error sending to Unity:', e);
                }
            };

            // 브릿지 준비 완료 알림
            console.log('Unity Bridge initialized successfully');
        ";

        webViewObject.EvaluateJS(initJS);
        bridgeManager.SetBridgeReady(true);

        Debug.Log("JavaScript Bridge initialized successfully");
    }

    public BridgeManager GetBridgeManager()
    {
        return bridgeManager;
    }

    void OnDestroy()
    {
        // WebView 정리
        if (webViewObject != null)
        {
            Destroy(webViewObject.gameObject);
        }

        // 로컬 서버 정리
        if (localServer != null)
        {
            localServer.StopServer();
            Destroy(localServer.gameObject);
        }

        // 브릿지 정리
        if (bridgeManager != null)
        {
            Destroy(bridgeManager.gameObject);
        }
    }
}