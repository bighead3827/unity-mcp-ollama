using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System;
using Newtonsoft.Json;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;

public class DefaultServerConfig : ServerConfig
{
    public new string unityHost = "localhost";
    public new int unityPort = 6400;
    public new int mcpPort = 6500;
    public new float connectionTimeout = 15.0f;
    public new int bufferSize = 32768;
    public new string logLevel = "INFO";
    public new string logFormat = "%(asctime)s - %(name)s - %(levelname)s - %(message)s";
    public new int maxRetries = 3;
    public new float retryDelay = 1.0f;
    
    // Ollama specific defaults
    public string ollamaHost = "localhost";
    public int ollamaPort = 11434;
    public string ollamaModel = "gemma3:12b";
    public float ollamaTemperature = 0.7f;
}

[Serializable]
public class OllamaConfig
{
    [JsonProperty("ollama_host")]
    public string ollamaHost = "localhost";
    
    [JsonProperty("ollama_port")]
    public int ollamaPort = 11434;
    
    [JsonProperty("ollama_model")]
    public string ollamaModel = "gemma3:12b";
    
    [JsonProperty("ollama_temperature")]
    public float ollamaTemperature = 0.7f;
}

[Serializable]
public class ServerConfig
{
    [JsonProperty("unity_host")]
    public string unityHost = "localhost";

    [JsonProperty("unity_port")]
    public int unityPort;

    [JsonProperty("mcp_port")]
    public int mcpPort;

    [JsonProperty("connection_timeout")]
    public float connectionTimeout;

    [JsonProperty("buffer_size")]
    public int bufferSize;

    [JsonProperty("log_level")]
    public string logLevel;

    [JsonProperty("log_format")]
    public string logFormat;

    [JsonProperty("max_retries")]
    public int maxRetries;

    [JsonProperty("retry_delay")]
    public float retryDelay;
    
    // Ollama settings
    [JsonProperty("ollama_host")]
    public string ollamaHost;
    
    [JsonProperty("ollama_port")]
    public int ollamaPort;
    
    [JsonProperty("ollama_model")]
    public string ollamaModel;
    
    [JsonProperty("ollama_temperature")]
    public float ollamaTemperature;
}

public class MCPEditorWindow : EditorWindow
{
    private bool isUnityBridgeRunning = false;
    private Vector2 scrollPosition;
    private string ollamaStatusMessage = "Not configured";
    private string pythonServerStatus = "Not Connected";
    private Color pythonServerStatusColor = Color.red;
    private const int unityPort = 6400;  // Hardcoded Unity port
    private const int mcpPort = 6500;    // Hardcoded MCP port
    private const float CONNECTION_CHECK_INTERVAL = 2f; // Check every 2 seconds
    private float lastCheckTime = 0f;
    private bool isPythonServerConnected = false;

    // Ollama configuration
    private string ollamaHost = "localhost";
    private int ollamaPort = 11434;
    private string ollamaModel = "gemma3:12b";
    private float ollamaTemperature = 0.7f;
    private string[] availableModels = new string[] { "deepseek-r1:14b", "gemma3:12b" };
    private int selectedModelIndex = 1; // Default to gemma3:12b

    // Chat interface
    private string userInput = "";
    private List<ChatMessage> chatHistory = new List<ChatMessage>();
    private Vector2 chatScrollPosition;
    private bool showChatInterface = false;
    private GUIStyle userMessageStyle;
    private GUIStyle assistantMessageStyle;
    
    // Message response tracking
    private Dictionary<string, int> pendingMessages = new Dictionary<string, int>();
    private float responseCheckTimer = 0.5f; // Check for responses every half second

    [MenuItem("Window/Unity MCP")]
    public static void ShowWindow()
    {
        GetWindow<MCPEditorWindow>("MCP Editor");
    }

    private void OnEnable()
    {
        // Check initial states
        isUnityBridgeRunning = UnityMCPBridge.IsRunning;
        CheckPythonServerConnection();
        LoadOllamaConfig();
        
        // Initialize chat styles
        userMessageStyle = new GUIStyle();
        userMessageStyle.normal.textColor = new Color(0.2f, 0.4f, 0.8f);
        userMessageStyle.wordWrap = true;
        userMessageStyle.richText = true;
        userMessageStyle.padding = new RectOffset(10, 10, 5, 5);
        
        assistantMessageStyle = new GUIStyle();
        assistantMessageStyle.normal.textColor = new Color(0.1f, 0.6f, 0.3f);
        assistantMessageStyle.wordWrap = true;
        assistantMessageStyle.richText = true;
        assistantMessageStyle.padding = new RectOffset(10, 10, 5, 5);
    }

    private void Update()
    {
        // Check Python server connection periodically
        if (Time.realtimeSinceStartup - lastCheckTime >= CONNECTION_CHECK_INTERVAL)
        {
            CheckPythonServerConnection();
            lastCheckTime = Time.realtimeSinceStartup;
        }
        
        // Check for pending message responses
        if (pendingMessages.Count > 0 && Time.realtimeSinceStartup - responseCheckTimer >= 0.5f)
        {
            responseCheckTimer = Time.realtimeSinceStartup;
            CheckForSimulatedResponses();
        }
    }
    
    // Check for simulated responses to update the chat UI
    private void CheckForSimulatedResponses()
    {
        var processedKeys = new List<string>();
        
        foreach (var kvp in pendingMessages)
        {
            string messageId = kvp.Key;
            int messageIndex = kvp.Value;
            
            // If response is available, update the chat message
            if (UnityMCPBridge.HasSimulatedResponse(messageId))
            {
                string response = UnityMCPBridge.GetSimulatedResponse(messageId);
                
                try
                {
                    var responseObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
                    if (responseObj.TryGetValue("result", out object resultObj))
                    {
                        var resultDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(resultObj));
                        
                        if (resultDict.TryGetValue("llm_response", out object llmObj))
                        {
                            string llmResponse = llmObj.ToString();
                            
                            // Update the message in the chat history
                            if (messageIndex < chatHistory.Count)
                            {
                                chatHistory[messageIndex] = new ChatMessage { 
                                    sender = "Assistant", 
                                    content = llmResponse 
                                };
                                
                                // Mark this message as processed
                                processedKeys.Add(messageId);
                                
                                // Force UI update
                                Repaint();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"Error parsing simulated response: {ex.Message}");
                }
            }
        }
        
        // Remove processed messages from pending list
        foreach (var key in processedKeys)
        {
            pendingMessages.Remove(key);
            UnityMCPBridge.RemoveSimulatedResponse(key);
        }
    }

    private async void CheckPythonServerConnection()
    {
        bool wasConnected = isPythonServerConnected;
        isPythonServerConnected = await UnityMCPBridge.CheckPythonServerConnection();
        
        // Only update UI if connection state changed
        if (isPythonServerConnected != wasConnected)
        {
            if (isPythonServerConnected)
            {
                pythonServerStatus = "Connected";
                pythonServerStatusColor = Color.green;
                
                // Check Ollama status if we just connected
                CheckOllamaStatus();
            }
            else
            {
                pythonServerStatus = "Not Connected";
                pythonServerStatusColor = Color.red;
                ollamaStatusMessage = "Not connected";
            }
            
            // Force UI update
            Repaint();
        }
    }

    private async void CheckOllamaStatus()
    {
        try
        {
            // Send a command to the Python server to check Ollama status
            var command = new
            {
                type = "get_ollama_status"
            };
            
            string commandJson = JsonConvert.SerializeObject(command);
            string response = await SendCommandToPythonServer(commandJson);
            
            try
            {
                var responseObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
                if (responseObj.TryGetValue("result", out object resultObj))
                {
                    var resultDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(resultObj));
                    if (resultDict.TryGetValue("status", out object statusObj))
                    {
                        string status = statusObj.ToString();
                        if (status == "connected")
                        {
                            ollamaStatusMessage = "Connected";
                            if (resultDict.TryGetValue("model", out object modelObj))
                            {
                                ollamaModel = modelObj.ToString();
                                // Update the selected model index
                                for (int i = 0; i < availableModels.Length; i++)
                                {
                                    if (availableModels[i] == ollamaModel)
                                    {
                                        selectedModelIndex = i;
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            ollamaStatusMessage = "Not Connected: " + status;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error parsing Ollama status response: {ex.Message}");
                ollamaStatusMessage = "Error checking status";
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Error checking Ollama status: {e.Message}");
            ollamaStatusMessage = "Error checking status";
        }
    }

    private async Task<string> SendCommandToPythonServer(string commandJson)
    {
        try
        {
            using (var client = new TcpClient())
            {
                await client.ConnectAsync("localhost", unityPort);
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] commandBytes = Encoding.UTF8.GetBytes(commandJson);
                    await stream.WriteAsync(commandBytes, 0, commandBytes.Length);
                    
                    // Read response
                    byte[] buffer = new byte[32768]; // Large buffer size for potentially large responses
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    return Encoding.UTF8.GetString(buffer, 0, bytesRead);
                }
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Error sending command to Python server: {e.Message}");
            throw;
        }
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("MCP Editor with Ollama", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // Python Server Status Section
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Python Server Status", EditorStyles.boldLabel);

        // Status bar
        var statusRect = EditorGUILayout.BeginHorizontal();
        EditorGUI.DrawRect(new Rect(statusRect.x, statusRect.y, 10, 20), pythonServerStatusColor);
        EditorGUILayout.LabelField(pythonServerStatus);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField($"Unity Port: {unityPort}");
        EditorGUILayout.LabelField($"MCP Port: {mcpPort}");
        EditorGUILayout.HelpBox("Start the Python server using command: 'python -m venv venv && source venv/bin/activate && pip install -e . && python server.py' in the Python directory", MessageType.Info);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // Unity Bridge Section
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Unity MCP Bridge", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Status: {(isUnityBridgeRunning ? "Running" : "Stopped")}");
        EditorGUILayout.LabelField($"Port: {unityPort}");

        if (GUILayout.Button(isUnityBridgeRunning ? "Stop Bridge" : "Start Bridge"))
        {
            ToggleUnityBridge();
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // Ollama Configuration Section
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Ollama Configuration", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Status: {ollamaStatusMessage}");
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.LabelField("Host:");
        ollamaHost = EditorGUILayout.TextField(ollamaHost);
        
        EditorGUILayout.LabelField("Port:");
        ollamaPort = EditorGUILayout.IntField(ollamaPort);
        
        EditorGUILayout.LabelField("Model:");
        selectedModelIndex = EditorGUILayout.Popup(selectedModelIndex, availableModels);
        ollamaModel = availableModels[selectedModelIndex];
        
        EditorGUILayout.LabelField("Temperature (0.0 - 1.0):");
        ollamaTemperature = EditorGUILayout.Slider(ollamaTemperature, 0.0f, 1.0f);
        
        if (GUILayout.Button("Apply Ollama Configuration"))
        {
            SaveOllamaConfig();
        }
        
        if (GUILayout.Button("Check Ollama Status"))
        {
            CheckOllamaStatus();
        }
        
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);
        
        // Chat Interface Toggle
        if (GUILayout.Button(showChatInterface ? "Hide Chat Interface" : "Show Chat Interface"))
        {
            showChatInterface = !showChatInterface;
        }
        
        // Chat Interface
        if (showChatInterface)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Chat with Unity via Ollama", EditorStyles.boldLabel);
            
            // Chat history
            chatScrollPosition = EditorGUILayout.BeginScrollView(chatScrollPosition, GUILayout.Height(200));
            foreach (var message in chatHistory)
            {
                EditorGUILayout.LabelField($"<b>{message.sender}:</b>", message.sender == "You" ? userMessageStyle : assistantMessageStyle);
                EditorGUILayout.LabelField(message.content, message.sender == "You" ? userMessageStyle : assistantMessageStyle);
                EditorGUILayout.Space(5);
            }
            EditorGUILayout.EndScrollView();
            
            // Input field
            EditorGUILayout.LabelField("Message:");
            userInput = EditorGUILayout.TextField(userInput, GUILayout.Height(60));
            
            // Send button
            if (GUILayout.Button("Send") && !string.IsNullOrEmpty(userInput))
            {
                string message = userInput;
                userInput = "";
                SendChatMessage(message);
            }
            
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.EndScrollView();
    }

    private void ToggleUnityBridge()
    {
        if (isUnityBridgeRunning)
        {
            UnityMCPBridge.Stop();
        }
        else
        {
            UnityMCPBridge.Start();
        }
        isUnityBridgeRunning = !isUnityBridgeRunning;
    }

    private void LoadOllamaConfig()
    {
        try
        {
            // Check if config file exists in the Python directory
            string configFilePath = GetLocalConfigPath();
            if (File.Exists(configFilePath))
            {
                string configJson = File.ReadAllText(configFilePath);
                ServerConfig config = JsonConvert.DeserializeObject<ServerConfig>(configJson);
                
                // Update fields
                ollamaHost = config.ollamaHost ?? "localhost";
                ollamaPort = config.ollamaPort != 0 ? config.ollamaPort : 11434;
                ollamaModel = string.IsNullOrEmpty(config.ollamaModel) ? availableModels[selectedModelIndex] : config.ollamaModel;
                ollamaTemperature = config.ollamaTemperature != 0 ? config.ollamaTemperature : 0.7f;
                
                // Update selected model index
                for (int i = 0; i < availableModels.Length; i++)
                {
                    if (availableModels[i] == ollamaModel)
                    {
                        selectedModelIndex = i;
                        break;
                    }
                }
                
                UnityEngine.Debug.Log("Loaded Ollama configuration from file");
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Error loading Ollama configuration: {e.Message}");
        }
    }

    private void SaveOllamaConfig()
    {
        try
        {
            // Send update to Python server
            UpdateOllamaConfigInServer();
            
            // Also save locally for UI state persistence
            string configFilePath = GetLocalConfigPath();
            
            var config = new OllamaConfig
            {
                ollamaHost = ollamaHost,
                ollamaPort = ollamaPort,
                ollamaModel = ollamaModel,
                ollamaTemperature = ollamaTemperature
            };
            
            string configJson = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configFilePath, configJson);
            
            UnityEngine.Debug.Log($"Saved Ollama configuration to {configFilePath}");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Error saving Ollama configuration: {e.Message}");
        }
    }
    
    private string GetLocalConfigPath()
    {
        // Store in the Unity project's temp folder
        string configDir = Path.Combine(Application.dataPath, "..", "Temp", "OllamaConfig");
        Directory.CreateDirectory(configDir);
        return Path.Combine(configDir, "ollama_config.json");
    }
    
    private async void UpdateOllamaConfigInServer()
    {
        try
        {
            // Construct command to update Ollama config
            var command = new
            {
                type = "configure_ollama",
                @params = new
                {
                    host = ollamaHost,
                    port = ollamaPort,
                    model = ollamaModel,
                    temperature = ollamaTemperature
                }
            };
            
            string commandJson = JsonConvert.SerializeObject(command);
            string response = await SendCommandToPythonServer(commandJson);
            
            try
            {
                var responseObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
                if (responseObj.TryGetValue("result", out object resultObj))
                {
                    var resultDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(resultObj));
                    if (resultDict.TryGetValue("status", out object statusObj))
                    {
                        string status = statusObj.ToString();
                        if (status == "connected")
                        {
                            ollamaStatusMessage = "Connected and configured";
                        }
                        else if (resultDict.TryGetValue("message", out object messageObj))
                        {
                            ollamaStatusMessage = messageObj.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error parsing Ollama configuration response: {ex.Message}");
                ollamaStatusMessage = "Error updating configuration";
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Error updating Ollama configuration: {e.Message}");
            ollamaStatusMessage = "Error updating configuration";
        }
    }
    
    private async void SendChatMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        
        // Add user message to chat history
        chatHistory.Add(new ChatMessage { sender = "You", content = message });
        
        try
        {
            // Generate a unique ID for this request
            string messageId = Guid.NewGuid().ToString();
            
            // Construct command to send to Python server
            var command = new
            {
                type = "process_user_request",
                @params = new
                {
                    prompt = message,
                    messageId = messageId  // Include the message ID
                }
            };
            
            string commandJson = JsonConvert.SerializeObject(command);
            
            // Show "thinking" message
            int thinkingIndex = chatHistory.Count;
            chatHistory.Add(new ChatMessage { sender = "Assistant", content = "Processing your request..." });
            
            // Register this message as pending
            pendingMessages[messageId] = thinkingIndex;
            
            // Send command to Python server
            string response = await SendCommandToPythonServer(commandJson);
            
            // Parse immediate response
            try
            {
                var responseObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
                
                // If we got an immediate error, update the chat right away
                if (responseObj.TryGetValue("status", out object statusObj) && statusObj.ToString() == "error")
                {
                    if (responseObj.TryGetValue("error", out object errorObj))
                    {
                        // Update the "thinking" message with the error
                        chatHistory[thinkingIndex] = new ChatMessage { 
                            sender = "Assistant", 
                            content = "Error: " + errorObj.ToString() 
                        };
                        
                        // Remove from pending
                        pendingMessages.Remove(messageId);
                    }
                }
                // Otherwise, the response will be picked up by the CheckForSimulatedResponses method
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error parsing chat response: {ex.Message}");
                
                // Update the "thinking" message with the error
                chatHistory[thinkingIndex] = new ChatMessage { 
                    sender = "Assistant", 
                    content = "Error processing your request. Please try again." 
                };
                
                // Remove from pending
                pendingMessages.Remove(messageId);
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Error sending chat message: {e.Message}");
            
            // Add error message to chat history
            chatHistory.Add(new ChatMessage { 
                sender = "Assistant", 
                content = "Error: Could not connect to the server. Please check your connection." 
            });
        }
        
        // Force UI update
        Repaint();
    }
}

// Simple class to represent a chat message
public class ChatMessage
{
    public string sender;
    public string content;
}

// Editor window to display manual configuration instructions
public class ManualConfigWindow : EditorWindow
{
    private string configPath;
    private string configJson;
    private Vector2 scrollPos;
    private bool pathCopied = false;
    private bool jsonCopied = false;
    private float copyFeedbackTimer = 0;

    public static void ShowWindow(string configPath, string configJson)
    {
        var window = GetWindow<ManualConfigWindow>("Manual Configuration");
        window.configPath = configPath;
        window.configJson = configJson;
        window.minSize = new Vector2(500, 400);
        window.Show();
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // Header
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Ollama Configuration", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // Instructions
        EditorGUILayout.LabelField("Please follow these steps to configure Ollama:", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("1. Ensure Ollama is installed and running", EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("2. Pull one of the supported models:", EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("   - deepseek-r1:14b", EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("   - gemma3:12b", EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("3. Configuration file location:", EditorStyles.wordWrappedLabel);

        // Config path section with copy button
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.SelectableLabel(configPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

        if (GUILayout.Button("Copy Path", GUILayout.Width(80)))
        {
            EditorGUIUtility.systemCopyBuffer = configPath;
            pathCopied = true;
            copyFeedbackTimer = 2f;
        }

        EditorGUILayout.EndHorizontal();

        if (pathCopied)
        {
            EditorGUILayout.LabelField("Path copied to clipboard!", EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(10);

        // JSON configuration
        EditorGUILayout.LabelField("4. Sample configuration:", EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(5);

        // JSON text area with copy button
        GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea)
        {
            wordWrap = true,
            richText = true
        };

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.SelectableLabel(configJson, textAreaStyle, GUILayout.MinHeight(200));
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Copy JSON Configuration"))
        {
            EditorGUIUtility.systemCopyBuffer = configJson;
            jsonCopied = true;
            copyFeedbackTimer = 2f;
        }

        if (jsonCopied)
        {
            EditorGUILayout.LabelField("JSON copied to clipboard!", EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(10);

        // Additional note
        EditorGUILayout.HelpBox("After configuring, restart the server to apply the changes.", MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    private void Update()
    {
        // Handle the feedback message timer
        if (copyFeedbackTimer > 0)
        {
            copyFeedbackTimer -= Time.deltaTime;
            if (copyFeedbackTimer <= 0)
            {
                pathCopied = false;
                jsonCopied = false;
                Repaint();
            }
        }
    }
}