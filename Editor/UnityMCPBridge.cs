using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement; // 追加: 场景操作所需
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

/// <summary>
/// Unity MCP Bridge 类
/// 这是一个Unity编辑器工具类，用于在Unity编辑器和Python MCP服务器之间建立通信桥梁。
/// 主要功能：
/// 1. 建立TCP服务器监听来自Python的连接
/// 2. 处理和转发命令到Python服务器
/// 3. 执行Unity场景和游戏对象操作
/// 4. 提供实时通信和命令执行功能
/// </summary>
[InitializeOnLoad]
public static partial class UnityMCPBridge
{
    // TCP服务器相关字段
    private static TcpListener listener;                 // TCP监听器
    private static bool isRunning = false;              // 服务器运行状态
    private static readonly object lockObj = new object(); // 线程同步锁
    private static Dictionary<string, (string commandJson, TaskCompletionSource<string> tcs)> commandQueue = new Dictionary<string, (string commandJson, TaskCompletionSource<string> tcs)>(); // 命令队列
    private static readonly int unityPort = 6400;       // Unity服务器端口
    private static readonly int mcpPort = 6500;         // MCP服务器端口
    private static bool lastConnectionState = false;     // 上次连接状态（用于只记录状态变化）
    
    // 模拟响应存储
    private static Dictionary<string, string> simulatedResponses = new Dictionary<string, string>();

    // 公开运行状态属性
    public static bool IsRunning => isRunning;
    
    // 是否始终使用模拟模式（当Python TCP服务器使用自定义服务器时禁用）
    private static bool alwaysUseSimulation = false;
    // 模拟响应相关方法
    /// <summary>
    /// 检查是否存在指定消息ID的模拟响应
    /// </summary>
    /// <param name="messageId">消息ID</param>
    /// <returns>是否存在模拟响应</returns>
    public static bool HasSimulatedResponse(string messageId)
    {
        return simulatedResponses.ContainsKey(messageId);
    }
    
    /// <summary>
    /// 获取指定消息ID的模拟响应
    /// </summary>
    /// <param name="messageId">消息ID</param>
    /// <returns>模拟响应内容，如果不存在则返回null</returns>
    public static string GetSimulatedResponse(string messageId)
    {
        if (simulatedResponses.TryGetValue(messageId, out string response))
        {
            return response;
        }
        return null;
    }
    
    /// <summary>
    /// 移除指定消息ID的模拟响应
    /// </summary>
    /// <param name="messageId">要移除的消息ID</param>
    public static void RemoveSimulatedResponse(string messageId)
    {
        if (simulatedResponses.ContainsKey(messageId))
        {
            simulatedResponses.Remove(messageId);
        }
    }

    /// <summary>
    /// 检查指定路径的文件夹是否存在
    /// </summary>
    /// <param name="path">要检查的路径</param>
    /// <returns>文件夹是否存在</returns>
    public static bool FolderExists(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        if (path.Equals("Assets", StringComparison.OrdinalIgnoreCase))
            return true;

        string fullPath = Path.Combine(Application.dataPath, path.StartsWith("Assets/") ? path.Substring(7) : path);
        return Directory.Exists(fullPath);
    }

    /// <summary>
    /// 静态构造函数
    /// 在Unity编辑器加载时自动调用，初始化服务器并注册退出事件
    /// </summary>
    static UnityMCPBridge()
    {
        // Start();
        EditorApplication.quitting += Stop;
    }

    /// <summary>
    /// 启动TCP服务器
    /// 初始化监听器并开始接受连接
    /// </summary>
    public static void Start()
    {
        if (isRunning) return;
        isRunning = true;
        listener = new TcpListener(IPAddress.Loopback, unityPort);
        listener.Start();
        Debug.Log($"UnityMCPBridge已启动，监听端口：{unityPort}");
        Task.Run(ListenerLoop);
        EditorApplication.update += ProcessCommands;
    }

    /// <summary>
    /// 停止TCP服务器
    /// 清理资源并停止监听
    /// </summary>
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
            Debug.LogWarning($"停止监听器时出错：{ex.Message}");
        }
        EditorApplication.update -= ProcessCommands;
        Debug.Log("UnityMCPBridge已停止");
    }

    /// <summary>
    /// 监听循环
    /// 持续监听并接受新的客户端连接
    /// </summary>
    private static async Task ListenerLoop()
    {
        while (isRunning)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync();
                // 启用基本的socket保活机制
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                // 设置较长的接收超时以防止快速断开
                client.ReceiveTimeout = 60000; // 60秒

                // 异步处理每个客户端连接
                _ = HandleClientAsync(client);
            }
            catch (Exception ex)
            {
                if (isRunning) Debug.LogError($"监听器错误: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 处理客户端连接
    /// 接收和处理来自客户端的命令
    /// </summary>
    /// <param name="client">TCP客户端连接</param>
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
                    if (bytesRead == 0) break; // 客户端断开连接

                    string commandText = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    string commandId = Guid.NewGuid().ToString();
                    var tcs = new TaskCompletionSource<string>();

                    // 特殊处理ping命令，避免JSON解析
                    if (commandText.Trim() == "ping")
                    {
                        // 直接响应ping而不进行JSON解析
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
                    Debug.LogError($"客户端处理错误: {ex.Message}");
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 处理命令队列
    /// 在Unity编辑器更新循环中执行排队的命令
    /// </summary>
    private static void ProcessCommands()
    {
        List<string> processedIds = new List<string>();
        lock (lockObj)
        {
            foreach (var kvp in commandQueue.ToList())
            {
                string id = kvp.Key;
                string commandText = kvp.Value.commandJson;
                var tcs = kvp.Value.tcs;

                try
                {
                    // 特殊情况处理
                    if (string.IsNullOrEmpty(commandText))
                    {
                        var emptyResponse = new
                        {
                            status = "error",
                            error = "收到空命令"
                        };
                        tcs.SetResult(JsonConvert.SerializeObject(emptyResponse));
                        processedIds.Add(id);
                        continue;
                    }

                    // 去除命令文本的空白字符
                    commandText = commandText.Trim();

                    // 处理非JSON直接命令（如ping）
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

                    // 在尝试反序列化之前检查命令是否为有效的JSON
                    if (!IsValidJson(commandText))
                    {
                        var invalidJsonResponse = new
                        {
                            status = "error",
                            error = "无效的JSON格式",
                            receivedText = commandText.Length > 50 ? commandText.Substring(0, 50) + "..." : commandText
                        };
                        tcs.SetResult(JsonConvert.SerializeObject(invalidJsonResponse));
                        processedIds.Add(id);
                        continue;
                    }

                    // 正常的JSON命令处理
                    var command = JsonConvert.DeserializeObject<JObject>(commandText);
                    if (command == null)
                    {
                        var nullCommandResponse = new
                        {
                            status = "error",
                            error = "命令反序列化为空",
                            details = "该命令是有效的JSON但无法反序列化为Command对象"
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
                    Debug.LogError($"处理命令时出错: {ex.Message}\n{ex.StackTrace}");

                    var response = new
                    {
                        status = "error",
                        error = ex.Message,
                        commandType = "未知（处理过程中出错）",
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

    /// <summary>
    /// 检查字符串是否为有效的JSON
    /// </summary>
    /// <param name="text">要检查的文本</param>
    /// <returns>是否为有效的JSON</returns>
    private static bool IsValidJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        if ((text.StartsWith("{") && text.EndsWith("}")) || // 对象
            (text.StartsWith("[") && text.EndsWith("]")))   // 数组
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
    
    /// <summary>
    /// 检查Python服务器连接状态
    /// 尝试连接并发送ping消息来验证连接是否有效
    /// </summary>
    /// <returns>连接是否成功</returns>
    public static async Task<bool> CheckPythonServerConnection()
    {
        bool isConnected = false;
        try
        {
            using (var client = new TcpClient())
            {
                // Debugger.Log("检查Python服务器连接...");
                // 尝试连接（带短超时）
                var connectTask = client.ConnectAsync("localhost", unityPort);
                if (await Task.WhenAny(connectTask, Task.Delay(1000)) == connectTask)
                {
                    // 尝试发送ping消息验证连接是否活跃
                    try
                    {
                        if (client.Connected == false)
                        {
                            client.Connect("localhost", unityPort);
                        }
                        NetworkStream stream = client.GetStream();
                        byte[] pingMessage = System.Text.Encoding.UTF8.GetBytes("ping");
                        await stream.WriteAsync(pingMessage, 0, pingMessage.Length);

                        // 等待响应（带超时）
                        byte[] buffer = new byte[1024];
                        var readTask = stream.ReadAsync(buffer, 0, buffer.Length);
                        if (await Task.WhenAny(readTask, Task.Delay(1000)) == readTask)
                        {
                            isConnected = true;
                            // 仅在连接状态改变时记录
                            if (isConnected != lastConnectionState)
                            {
                                Debug.Log($"Python服务器连接成功，端口：{unityPort}");
                                lastConnectionState = isConnected;
                            }
                        }
                        else
                        {
                            // 仅在连接状态改变时记录
                            if (isConnected != lastConnectionState)
                            {
                                Debug.LogWarning($"Python服务器未响应，端口：{unityPort}");
                                lastConnectionState = isConnected;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        // 仅在连接状态改变时记录
                        if (isConnected != lastConnectionState)
                        {
                            lastConnectionState = isConnected;
                        }
                        Debug.LogError("与Python服务器通信出错" + e.Message);
                    }
                }
                else
                {
                    // 仅在连接状态改变时记录
                    if (isConnected != lastConnectionState)
                    {
                        Debug.LogWarning($"Python服务器未运行或无法访问，端口：{unityPort}");
                        lastConnectionState = isConnected;
                    }
                }
            }
        }
        catch (Exception)
        {
            // 仅在连接状态改变时记录
            if (isConnected != lastConnectionState)
            {
                Debug.LogError("检查Python服务器状态时发生连接错误");
                lastConnectionState = isConnected;
            }
        }
        
        return isConnected;
    }

    // Helper method to forward a command to the Python MCP TCP server
    private static async Task<string> ForwardToMCPServer(string commandType, JObject parameters)
    {
        string messageId = null;
        int maxRetries = 3;
        int currentRetry = 0;
        int retryDelayMs = 1000; // 1秒延迟
        
        try
        {
            var command = new
            {
                type = commandType,
                @params = parameters
            };
            
            string commandJson = JsonConvert.SerializeObject(command);
            
            if (parameters != null && parameters["messageId"] != null)
            {
                messageId = parameters["messageId"].ToString();
            }
            else
            {
                messageId = Guid.NewGuid().ToString();
            }

            while (currentRetry < maxRetries)
            {
                try
                {
                    using (var client = new TcpClient())
                    {
                        Debug.Log($"尝试连接到MCP服务器 (localhost:{mcpPort}) - 第 {currentRetry + 1} 次尝试");
                        
                        var connectTask = client.ConnectAsync("localhost", mcpPort);
                        if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask)
                        {
                            throw new TimeoutException($"连接MCP服务器超时 (端口 {mcpPort})");
                        }
                        
                        if (client.Connected == false)
                        {
                            client.Connect("localhost", mcpPort);
                        }
                        // await connectTask;
                        
                        if (!client.Connected)
                        {
                            throw new SocketException((int)SocketError.NotConnected);
                        }

                        Debug.Log($"成功连接到MCP服务器 (localhost:{mcpPort})");
                        
                        using (var stream = client.GetStream())
                        {
                            client.ReceiveTimeout = 30000;
                            client.SendTimeout = 30000;
                            
                            Debug.Log($"正在发送命令: {commandType}");
                            byte[] commandBytes = System.Text.Encoding.UTF8.GetBytes(commandJson);
                            await stream.WriteAsync(commandBytes, 0, commandBytes.Length);
                            
                            byte[] buffer = new byte[32768];
                            var readTask = stream.ReadAsync(buffer, 0, buffer.Length);
                            if (await Task.WhenAny(readTask, Task.Delay(30000)) != readTask)
                            {
                                throw new TimeoutException("等待MCP服务器响应超时");
                            }
                            
                            int bytesRead = await readTask;
                            string response = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            
                            if (!string.IsNullOrEmpty(messageId))
                            {
                                simulatedResponses[messageId] = response;
                            }
                            
                            return response;
                        }
                    }
                }
                catch (Exception ex) when (ex is SocketException || ex is TimeoutException)
                {
                    currentRetry++;
                    if (currentRetry < maxRetries)
                    {
                        Debug.LogWarning($"连接失败 ({ex.Message}) - 将在 {retryDelayMs/1000} 秒后重试...");
                        await Task.Delay(retryDelayMs);
                    }
                    else
                    {
                        Debug.LogError($"在 {maxRetries} 次尝试后仍无法连接到MCP服务器");
                        Debug.LogError("请检查:");
                        Debug.LogError("1. Python TCP服务器是否正在运行 (python tcp_server.py)");
                        Debug.LogError($"2. 端口 {mcpPort} 是否可用且未被其他程序占用");
                        Debug.LogError("3. 防火墙设置是否允许本地连接");
                        Debug.LogError("4. Python服务器的日志输出是否有错误信息");
                        throw;
                    }
                }
            }
            
            throw new Exception($"无法在 {maxRetries} 次尝试后建立连接");
        }
        catch (Exception ex)
        {
            Debug.LogError($"转发命令到MCP服务器时出错: {ex.Message}");
            
            if (alwaysUseSimulation || ex is SocketException || ex is TimeoutException)
            {
                Debug.LogWarning("由于连接错误切换到模拟模式");
                
                if (commandType == "process_user_request")
                {
                    string prompt = parameters?["prompt"]?.ToString() ?? "未提供提示";
                    
                    var simulatedResponse = new
                    {
                        status = "error",
                        result = new
                        {
                            status = "error",
                            message = $"连接错误: {ex.Message}",
                            llm_response = $"TCP连接错误：{ex.Message}\n\n请确保：\n1. Python TCP服务器正在运行\n2. 端口 {mcpPort} 未被占用\n3. 防火墙允许本地连接\n\n要启动服务器，请运行：\npython tcp_server.py",
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
                    var errorResponse = new
                    {
                        status = "error",
                        result = new
                        {
                            status = "error",
                            message = $"与MCP服务器通信失败: {ex.Message}",
                            details = $"请确保Python TCP服务器正在运行且端口 {mcpPort} 可用"
                        }
                    };
                    
                    return JsonConvert.SerializeObject(errorResponse);
                }
            }
            else
            {
                var errorResponse = new
                {
                    status = "error",
                    result = new
                    {
                        status = "error",
                        message = $"处理命令失败: {ex.Message}",
                        llm_response = "抱歉，处理您的请求时出现错误。"
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
                
                // 在此执行Unity命令
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
                        
                    // 追加：find_objects_by_name 命令的实现
                    case "find_objects_by_name":
                        // 即使参数不存在，也不报错，而是作为空字符串处理
                        string searchName = arguments?["name"]?.ToString() ?? "";
                        FindObjectsByName(searchName);
                        break;
                        
                    // 追加：transform 命令的实现
                    case "transform":
                        TransformObject(arguments);
                        break;
                        
                    // 新实现：get_object_properties 命令的实现
                    case "get_object_properties":
                        string objName = arguments?["name"]?.ToString() ?? "";
                        GetObjectProperties(objName);
                        break;
                        
                    // 新实现：scene 命令的实现
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
        
        // 获取对象的基本信息
        Debug.Log($"Object Properties for '{objectName}':");
        Debug.Log($"- Position: {obj.transform.position}");
        Debug.Log($"- Rotation: {obj.transform.eulerAngles}");
        Debug.Log($"- Scale: {obj.transform.localScale}");
        Debug.Log($"- Active: {obj.activeSelf}");
        
        // 组件信息
        Component[] components = obj.GetComponents<Component>();
        Debug.Log($"- Components ({components.Length}):");
        foreach (Component component in components)
        {
            if (component != null)
            {
                Debug.Log($"  - {component.GetType().Name}");
            }
        }
        
        // 子对象
        int childCount = obj.transform.childCount;
        Debug.Log($"- Child Objects ({childCount}):");
        for (int i = 0; i < childCount; i++)
        {
            Transform child = obj.transform.GetChild(i);
            Debug.Log($"  - {child.name}");
        }
    }
    
    // 新实现：scene 命令
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
                GetSceneInfo(); // 默认显示信息
                break;
        }
    }
    
    // 获取场景信息
    private static void GetSceneInfo()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        Debug.Log("Current Scene Information:");
        Debug.Log($"- Name: {activeScene.name}");
        Debug.Log($"- Path: {activeScene.path}");
        Debug.Log($"- Build Index: {activeScene.buildIndex}");
        Debug.Log($"- Is Loaded: {activeScene.isLoaded}");
        Debug.Log($"- Root Objects Count: {activeScene.rootCount}");
        
        // 显示场景内的根对象
        GameObject[] rootObjects = activeScene.GetRootGameObjects();
        Debug.Log($"Root Objects ({rootObjects.Length}):");
        foreach (GameObject obj in rootObjects)
        {
            Debug.Log($"- {obj.name}");
        }
        
        // 显示所有场景的列表
        int sceneCount = SceneManager.sceneCount;
        Debug.Log($"Total Scenes Loaded: {sceneCount}");
        for (int i = 0; i < sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            Debug.Log($"Scene {i}: {scene.name} (Path: {scene.path})");
        }
    }
    
    // 创建新场景
    private static void CreateNewScene(string name)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Cannot create a new scene while in Play mode");
            return;
        }
        
        // 检查当前场景是否有未保存的更改
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
        
        // 创建新场景
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        
        // 保存场景并命名
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
    
    // 加载场景
    private static void LoadScene(string name)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Cannot load a scene while in Play mode");
            return;
        }
        
        // 检查当前场景是否有未保存的更改
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
        
        // 加载场景
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
    
    // 追加：FindObjectsByName 的实现 - 改进错误处理
    private static void FindObjectsByName(string objectName)
    {
        // 即使对象名为空，也不报错，而是显示警告
        if (string.IsNullOrEmpty(objectName))
        {
            Debug.LogWarning("No name provided for find_objects_by_name, searching for all objects");
            // 如果为空，则列出所有对象
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
    
    // 追加：TransformObject 的实现 - 改进错误处理
    private static void TransformObject(JObject arguments)
    {
        // 处理参数为 null 的情况
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
        
        // 如果有位置信息则进行转换
        if (arguments["position"] is JArray posArray && posArray.Count >= 3)
        {
            position = ObjectCommands.ParseVector3(posArray);
        }
        
        // 如果有旋转信息则进行转换
        if (arguments["rotation"] is JArray rotArray && rotArray.Count >= 3)
        {
            rotation = ObjectCommands.ParseVector3(rotArray);
        }
        
        // 如果有缩放信息则进行转换
        if (arguments["scale"] is JArray scaleArray && scaleArray.Count >= 3)
        {
            scale = ObjectCommands.ParseVector3(scaleArray);
        }
        
        // 设置变换
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
        // 增加参数为 null 时的错误处理
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
                            // 通过TCP将请求转发到Python服务器
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
                                    // 在新线程中执行Unity命令
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
                    forwardTask.Wait(5000); 
                    
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
                    
                    // 返回错误响应
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
                    forwardTask.Wait(5000); // 5秒
                    
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
                    
                    // 返回错误响应
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