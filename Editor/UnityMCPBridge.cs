using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement; // 追加: シーン操作に必要
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

[InitializeOnLoad]
public static partial class UnityMCPBridge
{
    private static TcpListener listener;
    private static bool isRunning = false;
    private static readonly object lockObj = new object();
    private static Dictionary<string, (string commandJson, TaskCompletionSource<string> tcs)> commandQueue = new();
    private static readonly int unityPort = 6400;  // Hardcoded port for Unity
    private static readonly int mcpPort = 6500;    // MCP port for forwarding commands
    private static bool lastConnectionState = false; // For logging connection state changes only
    
    // Simulated response storage
    private static Dictionary<string, string> simulatedResponses = new Dictionary<string, string>();

    // Add public property to expose running state
    public static bool IsRunning => isRunning;
    
    // Python側がカスタムTCPサーバーを使用するようになったので、シミュレーションを無効化
    private static bool alwaysUseSimulation = false;
    
    // Simulated response handling
    public static bool HasSimulatedResponse(string messageId)
    {
        return simulatedResponses.ContainsKey(messageId);
    }
    
    public static string GetSimulatedResponse(string messageId)
    {
        if (simulatedResponses.TryGetValue(messageId, out string response))
        {
            return response;
        }
        return null;
    }
    
    public static void RemoveSimulatedResponse(string messageId)
    {
        if (simulatedResponses.ContainsKey(messageId))
        {
            simulatedResponses.Remove(messageId);
        }
    }

    // Add method to check existence of a folder
    public static bool FolderExists(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        if (path.Equals("Assets", StringComparison.OrdinalIgnoreCase))
            return true;

        string fullPath = Path.Combine(Application.dataPath, path.StartsWith("Assets/") ? path.Substring(7) : path);
        return Directory.Exists(fullPath);
    }

    static UnityMCPBridge()
    {
        Start();
        EditorApplication.quitting += Stop;
    }

    public static void Start()
    {
        if (isRunning) return;
        isRunning = true;
        listener = new TcpListener(IPAddress.Loopback, unityPort);
        listener.Start();
        Debug.Log($"UnityMCPBridge started on port {unityPort}.");
        Task.Run(ListenerLoop);
        EditorApplication.update += ProcessCommands;
    }

    public static void Stop()
    {
        if (!isRunning) return;
        isRunning = false;
        try
        {
            listener.Stop();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Error stopping listener: {ex.Message}");
        }
        EditorApplication.update -= ProcessCommands;
        Debug.Log("UnityMCPBridge stopped.");
    }

    private static async Task ListenerLoop()
    {
        while (isRunning)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync();
                // Enable basic socket keepalive
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                // Set longer receive timeout to prevent quick disconnections
                client.ReceiveTimeout = 60000; // 60 seconds

                // Fire and forget each client connection
                _ = HandleClientAsync(client);
            }
            catch (Exception ex)
            {
                if (isRunning) Debug.LogError($"Listener error: {ex.Message}");
            }
        }
    }

    private static async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            var buffer = new byte[8192];
            while (isRunning)
            {
                try
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break; // Client disconnected

                    string commandText = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    string commandId = Guid.NewGuid().ToString();
                    var tcs = new TaskCompletionSource<string>();

                    // Special handling for ping command to avoid JSON parsing
                    if (commandText.Trim() == "ping")
                    {
                        // Direct response to ping without going through JSON parsing
                        byte[] pingResponseBytes = System.Text.Encoding.UTF8.GetBytes("{\"status\":\"success\",\"result\":{\"message\":\"pong\"}}");
                        await stream.WriteAsync(pingResponseBytes, 0, pingResponseBytes.Length);
                        continue;
                    }

                    lock (lockObj)
                    {
                        commandQueue[commandId] = (commandText, tcs);
                    }

                    string response = await tcs.Task;
                    byte[] responseBytes = System.Text.Encoding.UTF8.GetBytes(response);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Client handler error: {ex.Message}");
                    break;
                }
            }
        }
    }

    private static void ProcessCommands()
    {
        List<string> processedIds = new();
        lock (lockObj)
        {
            foreach (var kvp in commandQueue.ToList())
            {
                string id = kvp.Key;
                string commandText = kvp.Value.commandJson;
                var tcs = kvp.Value.tcs;

                try
                {
                    // Special case handling
                    if (string.IsNullOrEmpty(commandText))
                    {
                        var emptyResponse = new
                        {
                            status = "error",
                            error = "Empty command received"
                        };
                        tcs.SetResult(JsonConvert.SerializeObject(emptyResponse));
                        processedIds.Add(id);
                        continue;
                    }

                    // Trim the command text to remove any whitespace
                    commandText = commandText.Trim();

                    // Non-JSON direct commands handling (like ping)
                    if (commandText == "ping")
                    {
                        var pingResponse = new
                        {
                            status = "success",
                            result = new { message = "pong" }
                        };
                        tcs.SetResult(JsonConvert.SerializeObject(pingResponse));
                        processedIds.Add(id);
                        continue;
                    }

                    // Check if the command is valid JSON before attempting to deserialize
                    if (!IsValidJson(commandText))
                    {
                        var invalidJsonResponse = new
                        {
                            status = "error",
                            error = "Invalid JSON format",
                            receivedText = commandText.Length > 50 ? commandText.Substring(0, 50) + "..." : commandText
                        };
                        tcs.SetResult(JsonConvert.SerializeObject(invalidJsonResponse));
                        processedIds.Add(id);
                        continue;
                    }

                    // Normal JSON command processing
                    var command = JsonConvert.DeserializeObject<JObject>(commandText);
                    if (command == null)
                    {
                        var nullCommandResponse = new
                        {
                            status = "error",
                            error = "Command deserialized to null",
                            details = "The command was valid JSON but could not be deserialized to a Command object"
                        };
                        tcs.SetResult(JsonConvert.SerializeObject(nullCommandResponse));
                    }
                    else
                    {
                        string responseJson = ExecuteCommand(command);
                        tcs.SetResult(responseJson);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error processing command: {ex.Message}\n{ex.StackTrace}");

                    var response = new
                    {
                        status = "error",
                        error = ex.Message,
                        commandType = "Unknown (error during processing)",
                        receivedText = commandText?.Length > 50 ? commandText.Substring(0, 50) + "..." : commandText
                    };
                    string responseJson = JsonConvert.SerializeObject(response);
                    tcs.SetResult(responseJson);
                }

                processedIds.Add(id);
            }

            foreach (var id in processedIds)
            {
                commandQueue.Remove(id);
            }
        }
    }

    // Helper method to check if a string is valid JSON
    private static bool IsValidJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        if ((text.StartsWith("{") && text.EndsWith("}")) || // Object
            (text.StartsWith("[") && text.EndsWith("]")))   // Array
        {
            try
            {
                JToken.Parse(text);
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }
    
    // Improved method to check Python server connection status and avoid repeated log messages
    public static async Task<bool> CheckPythonServerConnection()
    {
        bool isConnected = false;
        try
        {
            using (var client = new TcpClient())
            {
                // Try to connect with a short timeout
                var connectTask = client.ConnectAsync("localhost", unityPort);
                if (await Task.WhenAny(connectTask, Task.Delay(1000)) == connectTask)
                {
                    // Try to send a ping message to verify connection is alive
                    try
                    {
                        NetworkStream stream = client.GetStream();
                        byte[] pingMessage = System.Text.Encoding.UTF8.GetBytes("ping");
                        await stream.WriteAsync(pingMessage, 0, pingMessage.Length);

                        // Wait for response with timeout
                        byte[] buffer = new byte[1024];
                        var readTask = stream.ReadAsync(buffer, 0, buffer.Length);
                        if (await Task.WhenAny(readTask, Task.Delay(1000)) == readTask)
                        {
                            isConnected = true;
                            // Only log if connection state changed
                            if (isConnected != lastConnectionState)
                            {
                                Debug.Log($"Python server connected successfully on port {unityPort}");
                                lastConnectionState = isConnected;
                            }
                        }
                        else
                        {
                            // Only log if connection state changed
                            if (isConnected != lastConnectionState)
                            {
                                Debug.LogWarning($"Python server not responding on port {unityPort}");
                                lastConnectionState = isConnected;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Only log if connection state changed
                        if (isConnected != lastConnectionState)
                        {
                            Debug.LogWarning("Communication error with Python server");
                            lastConnectionState = isConnected;
                        }
                    }
                }
                else
                {
                    // Only log if connection state changed
                    if (isConnected != lastConnectionState)
                    {
                        Debug.LogWarning($"Python server is not running or not accessible on port {unityPort}");
                        lastConnectionState = isConnected;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Only log if connection state changed
            if (isConnected != lastConnectionState)
            {
                Debug.LogError("Connection error when checking Python server status");
                lastConnectionState = isConnected;
            }
        }
        
        return isConnected;
    }

    // Helper method to forward a command to the Python MCP TCP server
    private static async Task<string> ForwardToMCPServer(string commandType, JObject parameters)
    {
        // これが追加されるmessageId変数
        string messageId = null;
        
        try
        {
            var command = new
            {
                type = commandType,
                @params = parameters
            };
            
            string commandJson = JsonConvert.SerializeObject(command);
            
            // Extract message ID if present for simulated responses
            if (parameters != null && parameters["messageId"] != null)
            {
                messageId = parameters["messageId"].ToString();
            }
            else
            {
                // Generate a random ID if none provided
                messageId = Guid.NewGuid().ToString();
            }
            
            // TCP接続でPythonサーバーと通信
            using (var client = new TcpClient())
            {
                // Try to connect to MCP server with timeout
                var connectionTask = client.ConnectAsync("localhost", mcpPort);
                if (await Task.WhenAny(connectionTask, Task.Delay(3000)) != connectionTask)
                {
                    throw new TimeoutException("Connection to MCP server timed out");
                }

                // 接続成功の場合のみこのコードが実行される
                using (var stream = client.GetStream())
                {
                    // Send the command
                    byte[] commandBytes = System.Text.Encoding.UTF8.GetBytes(commandJson);
                    await stream.WriteAsync(commandBytes, 0, commandBytes.Length);
                    
                    // Read response with timeout
                    byte[] buffer = new byte[32768]; // Large buffer
                    var readTask = stream.ReadAsync(buffer, 0, buffer.Length);
                    if (await Task.WhenAny(readTask, Task.Delay(30000)) != readTask)
                    {
                        throw new TimeoutException("Timeout waiting for MCP server response");
                    }
                    
                    int bytesRead = await readTask;
                    string response = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    
                    // Store the response for later retrieval by the UI
                    if (!string.IsNullOrEmpty(messageId))
                    {
                        simulatedResponses[messageId] = response;
                    }
                    
                    return response;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error forwarding command to MCP server: {ex.Message}");
            
            // TCP接続に失敗した場合のみシミュレーションモードで応答を返す
            if (alwaysUseSimulation || ex is SocketException || ex is TimeoutException)
            {
                Debug.LogWarning("Falling back to simulation mode due to connection error");
                
                if (commandType == "process_user_request")
                {
                    string prompt = parameters?["prompt"]?.ToString() ?? "No prompt provided";
                    
                    Debug.Log($"Simulating response for chat prompt: {prompt}");
                    
                    var simulatedResponse = new
                    {
                        status = "error",
                        result = new
                        {
                            status = "error",
                            message = $"Connection error: {ex.Message}",
                            llm_response = $"TCP接続エラーが発生しました：{ex.Message}\n\nPythonのTCPサーバーが起動していることを確認してください。\n\ntcp_server.pyを起動するには以下のコマンドを実行してください：\npython tcp_server.py",
                            commands_executed = 0,
                            results = new object[] { }
                        }
                    };
                    
                    string responseJson = JsonConvert.SerializeObject(simulatedResponse);
                    
                    if (!string.IsNullOrEmpty(messageId))
                    {
                        simulatedResponses[messageId] = responseJson;
                    }
                    
                    return responseJson;
                }
                else
                {
                    // その他のコマンドの場合のエラー応答
                    var errorResponse = new
                    {
                        status = "error",
                        result = new
                        {
                            status = "error",
                            message = $"Failed to communicate with MCP server: {ex.Message}",
                            details = "Please make sure the Python TCP server is running (python tcp_server.py)"
                        }
                    };
                    
                    return JsonConvert.SerializeObject(errorResponse);
                }
            }
            else
            {
                // その他のエラーの場合は通常のエラーレスポンスを返す
                var errorResponse = new
                {
                    status = "error",
                    result = new
                    {
                        status = "error",
                        message = $"Failed to process command: {ex.Message}",
                        llm_response = "Sorry, there was an error processing your request."
                    }
                };
                
                return JsonConvert.SerializeObject(errorResponse);
            }
        }
    }

    // Process commands extracted from llm_response
    private static void ProcessExtractedCommands(JArray commands)
    {
        if (commands == null || !commands.Any()) return;
        
        Debug.Log($"Processing {commands.Count} extracted commands");
        
        foreach (JObject cmd in commands)
        {
            try
            {
                string function = cmd["function"]?.ToString();
                JObject arguments = cmd["arguments"] as JObject;
                
                if (string.IsNullOrEmpty(function))
                {
                    Debug.LogWarning("Skipping command with empty function name");
                    continue;
                }
                
                Debug.Log($"Executing Unity command: {function}");
                
                // ここでUnityコマンドを実行
                switch (function.ToLower())
                {
                    case "create_object":
                        string objectName = arguments?["name"]?.ToString() ?? "NewObject";
                        string objectType = arguments?["type"]?.ToString() ?? "CUBE";
                        CreateObject(objectName, objectType, arguments);
                        break;
                        
                    case "set_object_transform":
                        SetObjectTransform(arguments);
                        break;
                        
                    case "delete_object":
                        DeleteObject(arguments?["name"]?.ToString());
                        break;
                        
                    case "editor_action":
                        EditorAction(arguments?["action"]?.ToString());
                        break;
                        
                    // 追加：find_objects_by_name コマンドの実装
                    case "find_objects_by_name":
                        // パラメータが存在しない場合でもエラーを出さずに空文字列として処理
                        string searchName = arguments?["name"]?.ToString() ?? "";
                        FindObjectsByName(searchName);
                        break;
                        
                    // 追加：transform コマンドの実装
                    case "transform":
                        TransformObject(arguments);
                        break;
                        
                    // 新規実装：get_object_properties コマンドの実装
                    case "get_object_properties":
                        string objName = arguments?["name"]?.ToString() ?? "";
                        GetObjectProperties(objName);
                        break;
                        
                    // 新規実装：scene コマンドの実装
                    case "scene":
                        string sceneAction = arguments?["action"]?.ToString() ?? "info";
                        HandleSceneCommand(sceneAction, arguments);
                        break;
                        
                    default:
                        Debug.LogWarning($"Unimplemented command: {function}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error executing command {cmd["function"]}: {ex.Message}");
            }
        }
    }
    
    // 新規実装：get_object_properties
    private static void GetObjectProperties(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            Debug.LogWarning("No name provided for get_object_properties, operation aborted");
            return;
        }
        
        GameObject obj = GameObject.Find(objectName);
        if (obj == null)
        {
            Debug.LogWarning($"Object '{objectName}' not found for get_object_properties");
            return;
        }
        
        // オブジェクトの基本情報を取得
        Debug.Log($"Object Properties for '{objectName}':");
        Debug.Log($"- Position: {obj.transform.position}");
        Debug.Log($"- Rotation: {obj.transform.eulerAngles}");
        Debug.Log($"- Scale: {obj.transform.localScale}");
        Debug.Log($"- Active: {obj.activeSelf}");
        
        // コンポーネント情報
        Component[] components = obj.GetComponents<Component>();
        Debug.Log($"- Components ({components.Length}):");
        foreach (Component component in components)
        {
            if (component != null)
            {
                Debug.Log($"  - {component.GetType().Name}");
            }
        }
        
        // 子オブジェクト
        int childCount = obj.transform.childCount;
        Debug.Log($"- Child Objects ({childCount}):");
        for (int i = 0; i < childCount; i++)
        {
            Transform child = obj.transform.GetChild(i);
            Debug.Log($"  - {child.name}");
        }
    }
    
    // 新規実装：scene コマンド
    private static void HandleSceneCommand(string action, JObject arguments)
    {
        switch (action.ToLower())
        {
            case "info":
                GetSceneInfo();
                break;
                
            case "create":
                string sceneName = arguments?["name"]?.ToString();
                if (string.IsNullOrEmpty(sceneName))
                {
                    Debug.LogError("Scene name is required for scene create action");
                    return;
                }
                CreateNewScene(sceneName);
                break;
                
            case "load":
                string sceneToLoad = arguments?["name"]?.ToString();
                if (string.IsNullOrEmpty(sceneToLoad))
                {
                    Debug.LogError("Scene name is required for scene load action");
                    return;
                }
                LoadScene(sceneToLoad);
                break;
                
            default:
                Debug.LogWarning($"Unknown scene action: {action}");
                GetSceneInfo(); // デフォルトで情報表示
                break;
        }
    }
    
    // シーン情報を取得
    private static void GetSceneInfo()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        Debug.Log("Current Scene Information:");
        Debug.Log($"- Name: {activeScene.name}");
        Debug.Log($"- Path: {activeScene.path}");
        Debug.Log($"- Build Index: {activeScene.buildIndex}");
        Debug.Log($"- Is Loaded: {activeScene.isLoaded}");
        Debug.Log($"- Root Objects Count: {activeScene.rootCount}");
        
        // シーン内のルートオブジェクトを表示
        GameObject[] rootObjects = activeScene.GetRootGameObjects();
        Debug.Log($"Root Objects ({rootObjects.Length}):");
        foreach (GameObject obj in rootObjects)
        {
            Debug.Log($"- {obj.name}");
        }
        
        // 全シーンのリストを表示
        int sceneCount = SceneManager.sceneCount;
        Debug.Log($"Total Scenes Loaded: {sceneCount}");
        for (int i = 0; i < sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            Debug.Log($"Scene {i}: {scene.name} (Path: {scene.path})");
        }
    }
    
    // 新しいシーンを作成
    private static void CreateNewScene(string name)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Cannot create a new scene while in Play mode");
            return;
        }
        
        // 現在のシーンが保存されていない変更を持っているか確認
        if (EditorSceneManager.GetActiveScene().isDirty)
        {
            bool save = EditorUtility.DisplayDialog(
                "Save Current Scene",
                "The current scene has unsaved changes. Do you want to save them before creating a new scene?",
                "Save",
                "Don't Save"
            );
            
            if (save)
            {
                EditorSceneManager.SaveOpenScenes();
            }
        }
        
        // 新しいシーンを作成
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        
        // シーンを名前を付けて保存
        string scenePath = $"Assets/{name}.unity";
        bool saved = EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath);
        
        if (saved)
        {
            Debug.Log($"Created and saved new scene: {name} at {scenePath}");
        }
        else
        {
            Debug.LogWarning($"Failed to save scene: {name}");
        }
    }
    
    // シーンをロード
    private static void LoadScene(string name)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Cannot load a scene while in Play mode");
            return;
        }
        
        // 現在のシーンが保存されていない変更を持っているか確認
        if (EditorSceneManager.GetActiveScene().isDirty)
        {
            bool save = EditorUtility.DisplayDialog(
                "Save Current Scene",
                "The current scene has unsaved changes. Do you want to save them before loading another scene?",
                "Save",
                "Don't Save"
            );
            
            if (save)
            {
                EditorSceneManager.SaveOpenScenes();
            }
        }
        
        // シーンをロード
        string scenePath = $"Assets/{name}.unity";
        if (File.Exists(scenePath))
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Debug.Log($"Loaded scene: {name}");
        }
        else
        {
            Debug.LogError($"Scene file not found: {scenePath}");
        }
    }
    
    // 追加：FindObjectsByName の実装 - エラー処理を改善
    private static void FindObjectsByName(string objectName)
    {
        // オブジェクト名が空の場合でもエラーを出さずに警告を表示
        if (string.IsNullOrEmpty(objectName))
        {
            Debug.LogWarning("No name provided for find_objects_by_name, searching for all objects");
            // 空の場合は全オブジェクトをリスト表示
            GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            if (allObjects.Length == 0)
            {
                Debug.Log("No objects found in scene");
            }
            else
            {
                Debug.Log($"Found {allObjects.Length} objects in scene:");
                int maxDisplayCount = Math.Min(10, allObjects.Length); // 表示数を制限
                for (int i = 0; i < maxDisplayCount; i++)
                {
                    Debug.Log($"- {allObjects[i].name}");
                }
                if (allObjects.Length > maxDisplayCount)
                {
                    Debug.Log($"... and {allObjects.Length - maxDisplayCount} more objects");
                }
            }
            return;
        }
        
        GameObject[] objects = ObjectCommands.FindObjectsByName(objectName);
        
        if (objects.Length == 0)
        {
            Debug.Log($"No objects found with name containing '{objectName}'");
        }
        else
        {
            Debug.Log($"Found {objects.Length} objects with name containing '{objectName}':");
            foreach (var obj in objects)
            {
                Debug.Log($"- {obj.name}");
            }
        }
    }
    
    // 追加：TransformObject の実装 - エラー処理を改善
    private static void TransformObject(JObject arguments)
    {
        // 引数がnullの場合の対策
        if (arguments == null)
        {
            Debug.LogWarning("No arguments provided for transform command");
            return;
        }
        
        string name = arguments["name"]?.ToString();
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogWarning("Object name is required for transform command");
            return;
        }
        
        GameObject obj = GameObject.Find(name);
        if (obj == null)
        {
            Debug.LogError($"Object '{name}' not found");
            return;
        }
        
        Vector3? position = null;
        Vector3? rotation = null;
        Vector3? scale = null;
        
        // 位置情報があれば変換
        if (arguments["position"] is JArray posArray && posArray.Count >= 3)
        {
            position = ObjectCommands.ParseVector3(posArray);
        }
        
        // 回転情報があれば変換
        if (arguments["rotation"] is JArray rotArray && rotArray.Count >= 3)
        {
            rotation = ObjectCommands.ParseVector3(rotArray);
        }
        
        // スケール情報があれば変換
        if (arguments["scale"] is JArray scaleArray && scaleArray.Count >= 3)
        {
            scale = ObjectCommands.ParseVector3(scaleArray);
        }
        
        // トランスフォームを設定
        ObjectCommands.SetTransform(obj, position, rotation, scale);
        
        Debug.Log($"Transformed object '{name}'");
    }
    
    // Simple Unity command implementations
    private static void CreateObject(string name, string type, JObject arguments)
    {
        GameObject obj = null;
        
        // Normalize type name to uppercase for matching
        type = type?.ToUpper() ?? "CUBE";
        
        switch (type)
        {
            case "CUBE":
                obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                break;
            case "SPHERE":
                obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                break;
            case "CAPSULE":
                obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                break;
            case "CYLINDER":
                obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                break;
            case "PLANE":
                obj = GameObject.CreatePrimitive(PrimitiveType.Plane);
                break;
            case "QUAD":
                obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                break;
            case "EMPTY":
                obj = new GameObject();
                break;
            default:
                obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Debug.LogWarning($"Unknown primitive type: {type}. Created a cube instead.");
                break;
        }
        
        if (obj != null)
        {
            obj.name = name;
            
            // Set position if provided
            if (arguments?["position"] is JArray posArray && posArray.Count >= 3)
            {
                float x = posArray[0].Value<float>();
                float y = posArray[1].Value<float>();
                float z = posArray[2].Value<float>();
                obj.transform.position = new Vector3(x, y, z);
            }
            
            // Set scale if provided
            if (arguments?["scale"] is JArray scaleArray && scaleArray.Count >= 3)
            {
                float x = scaleArray[0].Value<float>();
                float y = scaleArray[1].Value<float>();
                float z = scaleArray[2].Value<float>();
                obj.transform.localScale = new Vector3(x, y, z);
            }
            
            Debug.Log($"Created {type} object named '{name}'");
        }
    }
    
    private static void SetObjectTransform(JObject arguments)
    {
        // 引数がnullの場合のエラー処理を追加
        if (arguments == null)
        {
            Debug.LogWarning("No arguments provided for set_object_transform");
            return;
        }
        
        string name = arguments["name"]?.ToString();
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogError("Object name is required for set_object_transform");
            return;
        }
        
        GameObject obj = GameObject.Find(name);
        if (obj == null)
        {
            Debug.LogError($"Object '{name}' not found");
            return;
        }
        
        // Set position if provided
        if (arguments["position"] is JArray posArray && posArray.Count >= 3)
        {
            float x = posArray[0].Value<float>();
            float y = posArray[1].Value<float>();
            float z = posArray[2].Value<float>();
            obj.transform.position = new Vector3(x, y, z);
        }
        
        // Set rotation if provided
        if (arguments["rotation"] is JArray rotArray && rotArray.Count >= 3)
        {
            float x = rotArray[0].Value<float>();
            float y = rotArray[1].Value<float>();
            float z = rotArray[2].Value<float>();
            obj.transform.eulerAngles = new Vector3(x, y, z);
        }
        
        // Set scale if provided
        if (arguments["scale"] is JArray scaleArray && scaleArray.Count >= 3)
        {
            float x = scaleArray[0].Value<float>();
            float y = scaleArray[1].Value<float>();
            float z = scaleArray[2].Value<float>();
            obj.transform.localScale = new Vector3(x, y, z);
        }
        
        Debug.Log($"Updated transform for object '{name}'");
    }
    
    private static void DeleteObject(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogError("Object name is required for delete_object");
            return;
        }
        
        GameObject obj = GameObject.Find(name);
        if (obj == null)
        {
            Debug.LogError($"Object '{name}' not found");
            return;
        }
        
        UnityEngine.Object.DestroyImmediate(obj);
        Debug.Log($"Deleted object '{name}'");
    }
    
    private static void EditorAction(string action)
    {
        if (string.IsNullOrEmpty(action))
        {
            Debug.LogError("Action is required for editor_action");
            return;
        }
        
        action = action.ToUpper();
        
        switch (action)
        {
            case "PLAY":
                EditorApplication.isPlaying = true;
                Debug.Log("Started Play mode");
                break;
                
            case "STOP":
                EditorApplication.isPlaying = false;
                Debug.Log("Stopped Play mode");
                break;
                
            case "PAUSE":
                EditorApplication.isPaused = !EditorApplication.isPaused;
                Debug.Log($"Play mode paused: {EditorApplication.isPaused}");
                break;
                
            case "SAVE":
                AssetDatabase.SaveAssets();
                Debug.Log("Saved assets");
                break;
                
            default:
                Debug.LogWarning($"Unknown editor action: {action}");
                break;
        }
    }

    private static string ExecuteCommand(JObject commandObj)
    {
        try
        {
            if (!commandObj.TryGetValue("type", out JToken typeToken) || string.IsNullOrEmpty(typeToken.ToString()))
            {
                var errorResponse = new
                {
                    status = "error",
                    error = "Command type cannot be empty",
                    details = "A valid command type is required for processing"
                };
                return JsonConvert.SerializeObject(errorResponse);
            }

            string commandType = typeToken.ToString();
            JObject parameters = null;
            if (commandObj.TryGetValue("params", out JToken paramsToken))
            {
                parameters = paramsToken as JObject;
            }
            else
            {
                parameters = new JObject();
            }

            // Handle ping command for connection verification
            if (commandType == "ping")
            {
                var pingResponse = new { status = "success", result = new { message = "pong" } };
                return JsonConvert.SerializeObject(pingResponse);
            }
            
            // Handle process_user_request command for chat functionality
            if (commandType == "process_user_request")
            {
                try
                {
                    // Extract the message ID if present
                    string messageId = null;
                    if (parameters != null && parameters["messageId"] != null)
                    {
                        messageId = parameters["messageId"].ToString();
                    }
                    
                    // Mark that we're processing the request immediately
                    var processingResponse = new
                    {
                        status = "success",
                        result = new
                        {
                            status = "success",
                            message = "Processing your request...",
                            llm_response = "Processing your request...",
                            commands_executed = 0,
                            results = new object[] { }
                        }
                    };
                    
                    // Create a separate thread to handle this async operation in the main EditorUpdate
                    EditorApplication.delayCall += async () => {
                        try
                        {
                            // TCP経由でPythonサーバーにリクエストを転送
                            string taskResponse = await ForwardToMCPServer("process_user_request", parameters);
                            
                            // Parse the response to extract commands if any
                            try
                            {
                                JObject responseObj = JObject.Parse(taskResponse);
                                
                                // If we have executable commands, process them
                                JObject resultObj = responseObj["result"] as JObject;
                                JArray commands = resultObj?["commands"] as JArray;
                                
                                if (commands != null && commands.Count > 0)
                                {
                                    // 新しいスレッドでUnityコマンドを実行
                                    EditorApplication.delayCall += () => {
                                        try
                                        {
                                            ProcessExtractedCommands(commands);
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.LogError($"Error processing extracted commands: {ex.Message}");
                                        }
                                    };
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"Error parsing response for command extraction: {ex.Message}");
                            }
                            
                            // Log the taskResponse for debugging (but limit the length)
                            int maxLogLength = 200;
                            string logText = taskResponse.Length > maxLogLength ? 
                                taskResponse.Substring(0, maxLogLength) + "..." : 
                                taskResponse;
                            Debug.Log($"MCP server response: {logText}");
                            
                            // Continue processing in the UI if needed
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error in delayed process_user_request: {ex.Message}");
                        }
                    };
                    
                    // Return the processing response immediately
                    return JsonConvert.SerializeObject(processingResponse);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error handling process_user_request: {ex.Message}");
                    var errorResponse = new
                    {
                        status = "error",
                        result = new
                        {
                            status = "error",
                            message = $"Error: {ex.Message}",
                            llm_response = "Sorry, there was an error processing your request."
                        }
                    };
                    return JsonConvert.SerializeObject(errorResponse);
                }
            }
            
            // Handle get_ollama_status command - forward to TCP server
            if (commandType == "get_ollama_status")
            {
                try 
                {
                    Task<string> forwardTask = ForwardToMCPServer("get_ollama_status", parameters);
                    forwardTask.Wait(5000); // 5秒のタイムアウトを設定
                    
                    if (forwardTask.IsCompleted)
                    {
                        return forwardTask.Result;
                    }
                    else
                    {
                        throw new TimeoutException("Operation timed out");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error forwarding get_ollama_status: {ex.Message}");
                    
                    // エラー応答を返す
                    var statusResponse = new
                    {
                        status = "error",
                        result = new
                        {
                            status = "error",
                            message = $"Error checking Ollama status: {ex.Message}",
                            details = "Please make sure the Python TCP server is running (python tcp_server.py)"
                        }
                    };
                    return JsonConvert.SerializeObject(statusResponse);
                }
            }
            
            // Handle configure_ollama command - forward to TCP server
            if (commandType == "configure_ollama")
            {
                try 
                {
                    Task<string> forwardTask = ForwardToMCPServer("configure_ollama", parameters);
                    forwardTask.Wait(5000); // 5秒のタイムアウトを設定
                    
                    if (forwardTask.IsCompleted)
                    {
                        return forwardTask.Result;
                    }
                    else
                    {
                        throw new TimeoutException("Operation timed out");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error forwarding configure_ollama: {ex.Message}");
                    
                    // エラー応答を返す
                    var configResponse = new
                    {
                        status = "error",
                        result = new
                        {
                            status = "error",
                            message = $"Error configuring Ollama: {ex.Message}",
                            details = "Please make sure the Python TCP server is running (python tcp_server.py)"
                        }
                    };
                    return JsonConvert.SerializeObject(configResponse);
                }
            }

            // For other commands - use original placeholder implementation
            var defaultResponse = new
            {
                status = "success",
                result = new
                {
                    message = $"Command {commandType} was received but not implemented",
                    commandType = commandType,
                    paramsCount = parameters?.Count ?? 0
                }
            };
            return JsonConvert.SerializeObject(defaultResponse);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error executing command: {ex.Message}\n{ex.StackTrace}");
            var response = new
            {
                status = "error",
                error = ex.Message,
                stackTrace = ex.StackTrace,
            };
            return JsonConvert.SerializeObject(response);
        }
    }
}