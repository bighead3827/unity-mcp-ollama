# Unity MCP with Ollama Integration

A Unity MCP (Model Context Protocol) package that enables seamless communication between Unity and local Large Language Models (LLMs) via Ollama. This package extends [justinpbarnett/unity-mcp](https://github.com/justinpbarnett/unity-mcp) to work with local LLMs, allowing developers to automate workflows, manipulate assets, and control the Unity Editor programmatically without relying on cloud-based LLMs.

## Overview

The Unity MCP with Ollama Integration provides a bidirectional communication channel between:

1. Unity (via C#) 
2. A Python MCP server
3. Local LLMs running through Ollama

This enables:

- **Asset Management**: Create, import, and manipulate Unity assets programmatically
- **Scene Control**: Manage scenes, objects, and their properties
- **Material Editing**: Modify materials and their properties
- **Script Integration**: View, create, and update Unity scripts
- **Editor Automation**: Control Unity Editor functions like undo, redo, play, and build

All powered by your own local LLMs, with no need for an internet connection or API keys.

## Supported Models

This implementation is specifically configured to work with the following Ollama models:

- **deepseek-r1:14b** - A 14 billion parameter model with strong reasoning capabilities
- **gemma3:12b** - Google's 12 billion parameter model with good general capabilities

You can easily switch between these models in the Unity MCP window.

## Installation (Asset Method)

Due to Unity's package manager compatibility issues, we recommend using the **Asset Method** for installation.

### Prerequisites

- Unity 2020.3 LTS or newer
- Python 3.10 or newer
- [Ollama](https://ollama.ai/) installed on your system
- The following LLM models pulled in Ollama:
  - `ollama pull deepseek-r1:14b`
  - `ollama pull gemma3:12b`

### Step 1: Download and Install Editor Scripts

1. Download or clone this repository:
   ```
   git clone https://github.com/ZundamonnoVRChatkaisetu/unity-mcp-ollama.git
   ```

2. Create a folder in your Unity project's Assets directory:
   ```
   Assets/UnityMCPOllama
   ```

3. Copy the `Editor` folder from the cloned repository to your Unity project:
   ```
   # Copy the entire Editor folder
   [Repository]/Editor → Assets/UnityMCPOllama/Editor
   ```

4. Verify the folder structure is correct:
   ```
   Assets/
     UnityMCPOllama/
       Editor/
         MCPEditorWindow.cs
         UnityMCPBridge.cs
   ```

5. Let Unity import and compile the scripts

### Step 2: Set Up Python Environment

1. Create a folder for the Python environment (outside your Unity project):
   ```
   mkdir PythonMCP
   cd PythonMCP
   ```

2. Copy the Python folder from the cloned repository:
   ```
   cp -r [Repository]/Python .
   ```

3. Create and activate a virtual environment:
   ```bash
   # Create a virtual environment
   python -m venv venv
   
   # Activate the virtual environment
   # On Windows:
   venv\Scripts\activate
   # On macOS/Linux:
   source venv/bin/activate
   ```

4. Install dependencies:
   ```bash
   cd Python
   pip install -e .
   ```

### Step 3: Configure Ollama

1. Ensure Ollama is installed and running on your system
2. Pull the supported models:
   ```bash
   ollama pull deepseek-r1:14b
   ollama pull gemma3:12b
   ```
3. Start Ollama server:
   ```bash
   ollama serve
   ```

## Using Unity MCP with Ollama

### Step 1: Start Unity Bridge

1. Open your Unity project
2. Navigate to `Window > Unity MCP` to open the MCP window
3. Click the **Start Bridge** button to start the Unity bridge

### Step 2: Start Python Server

1. Open a command prompt or terminal
2. Navigate to your Python environment:
   ```bash
   cd PythonMCP
   ```
3. Activate the virtual environment:
   ```bash
   # On Windows:
   venv\Scripts\activate
   # On macOS/Linux:
   source venv/bin/activate
   ```
4. Navigate to the Python directory and start the server:
   ```bash
   cd Python
   python server.py
   ```

### Step 3: Configure Ollama Settings

1. In the Unity MCP window, locate the **Ollama Configuration** section
2. Verify or update the following settings:
   - **Host**: localhost (default)
   - **Port**: 11434 (default)
   - **Model**: Select either `deepseek-r1:14b` or `gemma3:12b`
   - **Temperature**: Adjust as needed (0.0-1.0)
3. Click **Apply Ollama Configuration**

### Step 4: Use the Chat Interface

1. Click the **Show Chat Interface** button in the Unity MCP window
2. Type your instructions in the message field
3. Click **Send** to process your request

Example prompts:
- "Create a red cube at position (0, 1, 0)"
- "Add a sphere to the scene and apply a blue material"
- "List all objects in the current scene"
- "Write a simple movement script and attach it to the cube"

## Connection Status Indicators

The Unity MCP window provides status information for each component:

- **Python Server Status**: Indicates whether the Python server is running
  - Green: Connected
  - Yellow: Connected but with issues
  - Red: Not connected

- **Unity Bridge Status**: Shows if the Unity socket server is running
  - Running: Unity is listening for connections
  - Stopped: Unity socket server is not active

- **Ollama Status**: Shows the connection status to Ollama
  - Connected: Successfully connected to Ollama server
  - Not Connected: Unable to connect to Ollama

## Troubleshooting

### Common Issues

1. **"Not Connected" Status for Python Server**
   - Ensure the Python server is running (`python server.py`)
   - Check for errors in the Python console
   - Verify the Unity Bridge is running

2. **Cannot find Unity MCP menu**
   - Make sure the Editor scripts are properly imported in your project
   - Check the Unity console for any errors
   - Restart Unity if necessary

3. **Ollama Connection Issues**
   - Verify Ollama is running with `ollama serve`
   - Check that models are properly pulled
   - Ensure no firewall is blocking port 11434

4. **MCP Command Execution Fails**
   - Check Python console for detailed error messages
   - Verify that the Unity Bridge is running
   - Make sure the prompt is clear and specific

### Explicit Setup Instructions for Python Environment

If you encounter issues setting up the Python environment:

1. Install Python 3.10 or newer
2. Install Ollama from [ollama.ai](https://ollama.ai/)
3. Create a dedicated directory for the Python environment:
   ```
   mkdir C:\PythonMCP
   cd C:\PythonMCP
   ```
4. Clone or download this repository and copy the Python folder:
   ```
   git clone https://github.com/ZundamonnoVRChatkaisetu/unity-mcp-ollama.git
   copy unity-mcp-ollama\Python .
   ```
5. Create a virtual environment:
   ```
   python -m venv venv
   ```
6. Activate the virtual environment:
   ```
   venv\Scripts\activate
   ```
7. Install dependencies:
   ```
   cd Python
   pip install -e .
   ```
8. Run the server:
   ```
   python server.py
   ```

## Performance Considerations

Local LLM performance depends on your hardware:

- For **deepseek-r1:14b**: Recommended minimum 12GB VRAM
- For **gemma3:12b**: Recommended minimum 10GB VRAM
- CPU-only operation is possible but will be significantly slower

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request or open an Issue.

## License

This project is licensed under the MIT License.

## Acknowledgments

- Based on [justinpbarnett/unity-mcp](https://github.com/justinpbarnett/unity-mcp)
- Uses [Ollama](https://ollama.ai/) for local LLM integration
