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

    // Add public property to expose running state
    public static bool IsRunning => isRunning;

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
        listener.Stop();
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

            // Handle ping command for connection verification
            if (commandType == "ping")
            {
                var pingResponse = new { status = "success", result = new { message = "pong" } };
                return JsonConvert.SerializeObject(pingResponse);
            }

            // Process other commands based on type
            // This is where you would route to the appropriate handlers
            // For this implementation, we'll simply return a placeholder success
            var successResponse = new
            {
                status = "success",
                result = new
                {
                    message = $"Command {commandType} was received",
                    commandType = commandType,
                    paramsCount = parameters?.Count ?? 0
                }
            };
            return JsonConvert.SerializeObject(successResponse);
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
