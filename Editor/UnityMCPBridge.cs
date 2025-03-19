using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
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
    private static readonly int unityPort = 6400;  // Hardcoded port
    private static readonly int mcpPort = 6500;    // MCP port for forwarding commands
    private static bool lastConnectionState = false; // For logging connection state changes only
    
    // Simulated response storage
    private static Dictionary<string, string> simulatedResponses = new Dictionary<string, string>();

    // Add public property to expose running state
    public static bool IsRunning => isRunning;
    
    // Python側がstdioモードでのみ動作するため、常にシミュレーションモードを使用
    private static bool alwaysUseSimulation = true;
    
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

    // Helper method to simulate responses (since TCP mode isn't working)
    private static async Task<string> ForwardToMCPServer(string commandType, JObject parameters)
    {
        try
        {
            // Extract message ID if present
            string messageId = null;
            if (parameters != null && parameters["messageId"] != null)
            {
                messageId = parameters["messageId"].ToString();
            }
            else
            {
                // Generate a random ID if none provided
                messageId = Guid.NewGuid().ToString();
            }
            
            // Pythonサーバーが実際にはTCPモードをサポートしていないため、常にシミュレーション応答を提供
            Debug.Log("Using simulation mode for all requests because the Python server doesn't support TCP mode");
            
            // シミュレーション応答
            if (commandType == "process_user_request")
            {
                string prompt = parameters?["prompt"]?.ToString() ?? "No prompt provided";
                
                Debug.Log($"Processing chat prompt in simulation mode: {prompt}");
                
                var simulatedResponse = new
                {
                    status = "success",
                    result = new
                    {
                        status = "success",
                        message = "Request processed successfully (simulation mode)",
                        llm_response = $"Ollamaのシミュレーション応答:\n\n「{prompt}」に対する回答です。\n\n現在、PythonサーバーはTCPモードをサポートしていないため、シミュレーションモードで動作しています。\n\nPython側のサーバーはstdioトランスポートで実行されているため、直接TCPで通信できません。\n\n実際の実装では、以下のいずれかの方法で回避できます：\n1. PythonサーバーをTCPサポート付きで再実装する\n2. シミュレーションモードを完全に活用する\n3. 代替のプロトコルを検討する",
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
            else if (commandType == "get_ollama_status")
            {
                var statusResponse = new
                {
                    status = "success",
                    result = new
                    {
                        status = "simulated",
                        model = "gemma3:12b (simulated)",
                        host = "localhost",
                        port = 11434
                    }
                };
                return JsonConvert.SerializeObject(statusResponse);
            }
            else if (commandType == "configure_ollama")
            {
                string host = parameters?["host"]?.ToString() ?? "localhost";
                int port = parameters?["port"]?.ToObject<int>() ?? 11434;
                string model = parameters?["model"]?.ToString() ?? "gemma3:12b";
                float temperature = parameters?["temperature"]?.ToObject<float>() ?? 0.7f;
                
                var configResponse = new
                {
                    status = "success",
                    result = new
                    {
                        status = "simulated",
                        message = "Configuration update simulated successfully",
                        config = new
                        {
                            host = host,
                            port = port,
                            model = model,
                            temperature = temperature
                        }
                    }
                };
                return JsonConvert.SerializeObject(configResponse);
            }
            else
            {
                // その他のコマンドの汎用的なシミュレーション応答
                var genericResponse = new
                {
                    status = "success",
                    result = new
                    {
                        message = $"Command {commandType} was simulated",
                        details = "Using simulation mode because the Python server doesn't support TCP connections"
                    }
                };
                return JsonConvert.SerializeObject(genericResponse);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in ForwardToMCPServer: {ex.Message}");
            
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
                            // シミュレーションモードを使用して応答を生成
                            string taskResponse = await ForwardToMCPServer("process_user_request", parameters);
                            
                            // Log the taskResponse for debugging (but limit the length)
                            int maxLogLength = 200;
                            string logText = taskResponse.Length > maxLogLength ? 
                                taskResponse.Substring(0, maxLogLength) + "..." : 
                                taskResponse;
                            Debug.Log($"Simulated response: {logText}");
                            
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
            
            // Handle get_ollama_status command - now use simulation mode
            if (commandType == "get_ollama_status")
            {
                try 
                {
                    Task<string> forwardTask = ForwardToMCPServer("get_ollama_status", parameters);
                    forwardTask.Wait(3000); // 3秒のタイムアウトを設定
                    
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
                    
                    // シミュレーション応答を返す
                    var statusResponse = new
                    {
                        status = "success",
                        result = new
                        {
                            status = "simulated",
                            model = "gemma3:12b (simulated)",
                            host = "localhost",
                            port = 11434,
                            message = "Simulation mode active"
                        }
                    };
                    return JsonConvert.SerializeObject(statusResponse);
                }
            }
            
            // Handle configure_ollama command - now use simulation mode
            if (commandType == "configure_ollama")
            {
                try 
                {
                    Task<string> forwardTask = ForwardToMCPServer("configure_ollama", parameters);
                    forwardTask.Wait(3000); // 3秒のタイムアウトを設定
                    
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
                    
                    // シミュレーション応答を返す
                    string host = parameters?["host"]?.ToString() ?? "localhost";
                    int port = parameters?["port"]?.ToObject<int>() ?? 11434;
                    string model = parameters?["model"]?.ToString() ?? "gemma3:12b";
                    float temperature = parameters?["temperature"]?.ToObject<float>() ?? 0.7f;
                    
                    var configResponse = new
                    {
                        status = "success",
                        result = new
                        {
                            status = "simulated",
                            message = "Configuration update simulated successfully",
                            config = new
                            {
                                host = host,
                                port = port,
                                model = model,
                                temperature = temperature
                            }
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
                    message = $"Command {commandType} was received",
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