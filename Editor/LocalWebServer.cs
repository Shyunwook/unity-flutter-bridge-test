using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

public class LocalWebServer : MonoBehaviour
{
    // Singleton
    private static LocalWebServer instance;

    private TcpListener tcpListener;
    private Thread listenerThread;
    public int port = 8088;
    private bool isRunning = false;

    // Static 캐시 - 인스턴스가 재생성되어도 유지
    private static Dictionary<string, byte[]> fileCache = new Dictionary<string, byte[]>();

#pragma warning disable 0414 // 조건부 컴파일로 인한 미사용 경고 억제
    private static bool isCachingComplete = false;
#pragma warning restore 0414

    void Awake()
    {
        // Singleton 패턴
        if (instance != null && instance != this)
        {
            Debug.Log("LocalWebServer already exists. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Android: 캐싱 후 서버 시작
        if (isCachingComplete)
        {
            StartServer();
        }
        else
        {
            StartCoroutine(PreloadFiles());
        }
#else
        // iOS/Editor: 바로 서버 시작
        StartServer();
#endif
    }

    IEnumerator PreloadFiles()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        Debug.Log("Preloading StreamingAssets files for Android...");

        // Android: UnityWebRequest로 파일 로드 (실제 요청되는 파일만)
        string[] filesToLoad = new string[]
        {
            "flutter/index.html",
            "flutter/flutter_bootstrap.js",
            "flutter/main.dart.js",
            "flutter/favicon.png",
            "flutter/assets/assets/lion.png",
            "flutter/assets/packages/wakelock_plus/assets/no_sleep.js",
            "flutter/assets/AssetManifest.bin.json",
            "flutter/assets/FontManifest.json",
            "flutter/assets/fonts/MaterialIcons-Regular.otf",
            "flutter/canvaskit/chromium/canvaskit.js",
            "flutter/canvaskit/chromium/canvaskit.wasm"
        };

        int successCount = 0;
        int failCount = 0;

        foreach (string file in filesToLoad)
        {
            string path = Path.Combine(Application.streamingAssetsPath, file);
            UnityWebRequest request = UnityWebRequest.Get(path);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                fileCache[file] = request.downloadHandler.data;
                successCount++;
            }
            else
            {
                failCount++;
                Debug.LogError($"Failed to load {file}: {request.error}");
            }
        }

        isCachingComplete = true;
        Debug.Log($"Android file caching complete. Success: {successCount}, Failed: {failCount}");

        StartServer();
#else
        yield return null;
#endif
    }

    public void StartServer()
    {
        if (isRunning) return;

        listenerThread = new Thread(Listen);
        listenerThread.IsBackground = true;
        listenerThread.Start();
        Debug.Log($"Local web server started on http://localhost:{port}");
    }

    private void Listen()
    {
        try
        {
            isRunning = true;
            tcpListener = new TcpListener(IPAddress.Loopback, port);
            tcpListener.Start();
            Debug.Log($"TcpListener started on port {port}");

            while (isRunning)
            {
                try
                {
                    if (tcpListener.Pending())
                    {
                        TcpClient client = tcpListener.AcceptTcpClient();
                        Thread clientThread = new Thread(() => HandleClient(client));
                        clientThread.IsBackground = true;
                        clientThread.Start();
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
                catch (Exception e)
                {
                    if (isRunning)
                    {
                        Debug.LogError($"Error accepting client: {e.Message}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start TCP listener: {e.Message}");
        }
    }

    private void HandleClient(TcpClient client)
    {
        try
        {
            using (NetworkStream stream = client.GetStream())
            {
                byte[] buffer = new byte[4096];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                string[] requestLines = request.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                if (requestLines.Length > 0)
                {
                    string[] requestParts = requestLines[0].Split(' ');
                    if (requestParts.Length >= 2)
                    {
                        string method = requestParts[0];
                        string url = requestParts[1];

                        if (method == "GET")
                        {
                            ProcessGetRequest(stream, url);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error handling client: {e.Message}");
        }
        finally
        {
            client.Close();
        }
    }

    private void ProcessGetRequest(NetworkStream stream, string url)
    {
        // Remove leading slash
        if (url.StartsWith("/"))
            url = url.Substring(1);

        // Remove query string
        int queryIndex = url.IndexOf('?');
        if (queryIndex >= 0)
            url = url.Substring(0, queryIndex);

        // Default to index.html
        if (url == "" || url == "flutter" || url == "flutter/")
            url = "flutter/index.html";

        try
        {
            byte[] fileBytes = null;

#if UNITY_ANDROID && !UNITY_EDITOR
            // Android: 캐시에서 파일 가져오기
            if (fileCache.ContainsKey(url))
            {
                fileBytes = fileCache[url];
            }
            else
            {
                Debug.LogError($"File not in cache: {url}");
            }
#else
            // iOS/Editor: 파일 시스템에서 직접 읽기
            string filePath = Path.Combine(Application.streamingAssetsPath, url);
            if (File.Exists(filePath))
            {
                fileBytes = File.ReadAllBytes(filePath);
            }
#endif

            if (fileBytes != null)
            {
                string contentType = GetContentType(url);

                string response = "HTTP/1.1 200 OK\r\n";
                response += $"Content-Type: {contentType}\r\n";
                response += $"Content-Length: {fileBytes.Length}\r\n";
                response += "Access-Control-Allow-Origin: *\r\n";
                response += "Connection: close\r\n";
                response += "\r\n";

                byte[] responseHeader = Encoding.UTF8.GetBytes(response);
                stream.Write(responseHeader, 0, responseHeader.Length);
                stream.Write(fileBytes, 0, fileBytes.Length);
            }
            else
            {
                string response = "HTTP/1.1 404 Not Found\r\n";
                response += "Content-Type: text/plain\r\n";
                response += "Connection: close\r\n";
                response += "\r\n";
                response += "404 - File Not Found: " + url;

                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                stream.Write(responseBytes, 0, responseBytes.Length);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error serving file: {e.Message}");
            string response = "HTTP/1.1 500 Internal Server Error\r\n";
            response += "Content-Type: text/plain\r\n";
            response += "Connection: close\r\n";
            response += "\r\n";
            response += "500 - Internal Server Error";

            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            stream.Write(responseBytes, 0, responseBytes.Length);
        }
    }

    private string GetContentType(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLower();
        switch (extension)
        {
            case ".html": return "text/html; charset=utf-8";
            case ".js": return "application/javascript; charset=utf-8";
            case ".css": return "text/css; charset=utf-8";
            case ".json": return "application/json; charset=utf-8";
            case ".png": return "image/png";
            case ".jpg":
            case ".jpeg": return "image/jpeg";
            case ".gif": return "image/gif";
            case ".svg": return "image/svg+xml";
            case ".ico": return "image/x-icon";
            case ".wasm": return "application/wasm";
            case ".woff": return "font/woff";
            case ".woff2": return "font/woff2";
            case ".ttf": return "font/ttf";
            default: return "application/octet-stream";
        }
    }

    void OnDestroy()
    {
        // Singleton은 파괴하지 않음
        if (instance == this)
        {
            StopServer();
        }
    }

    void OnApplicationQuit()
    {
        StopServer();
    }

    public void StopServer()
    {
        if (!isRunning) return;

        isRunning = false;

        if (tcpListener != null)
        {
            tcpListener.Stop();
        }

        if (listenerThread != null && listenerThread.IsAlive)
        {
            listenerThread.Join(1000);
        }

        Debug.Log("Local web server stopped");
    }
}