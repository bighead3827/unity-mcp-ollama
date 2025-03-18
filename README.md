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

## Installation

### Prerequisites

- Unity 2020.3 LTS or newer
- Python 3.10 or newer
- [Ollama](https://ollama.ai/) installed on your system
- The following LLM models pulled in Ollama:
  - `ollama pull deepseek-r1:14b`
  - `ollama pull gemma3:12b`

### Step 1: Install the Unity Package

- Open Unity Package Manager (`Window > Package Manager`)
- Click the `+` button and select `Add package from git URL`
- Enter: `https://github.com/ZundamonnoVRChatkaisetu/unity-mcp-ollama.git`

### Step 2: Set Up Python Environment

- Navigate to the Python directory in your project
- Install dependencies:
  ```bash
  # Create a virtual environment
  python -m venv venv
  
  # Activate the virtual environment
  # On Windows:
  venv\Scripts\activate
  # On macOS/Linux:
  source venv/bin/activate
  
  # Install dependencies
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

### Configuration

1. Open the Unity MCP window (`Window > Unity MCP`)
2. In the "Ollama Configuration" section:
   - Verify/set the Host (default: localhost)
   - Verify/set the Port (default: 11434)
   - Select your desired model (deepseek-r1:14b or gemma3:12b)
   - Adjust the temperature setting if needed (0.0-1.0)
   - Click "Apply Ollama Configuration"

3. Start the Unity MCP Bridge by clicking "Start Bridge"
4. Start the Python server from the command line:
   ```bash
   # Navigate to the Python directory in your project
   cd path/to/Python
   
   # Activate the virtual environment
   source venv/bin/activate  # or venv\Scripts\activate on Windows
   
   # Start the server
   python server.py
   ```

### Chat Interface

The Unity MCP window includes a built-in chat interface to interact with your local LLM:

1. Click "Show Chat Interface" to expand the chat panel
2. Type your instructions in the message box
3. Click "Send" to process your request

Examples of commands you can use:
- "Create a red cube at position (0, 1, 0)"
- "Make a sphere and apply a blue material to it"
- "List all objects in the current scene"
- "Create a C# script named PlayerController"

### Connection Status

The Unity MCP window displays status information for:
- Unity Bridge: Shows if the local socket server is running
- Python Server: Shows if the Python MCP server is connected
- Ollama: Shows if the connection to Ollama is active

## Troubleshooting

### Common Issues

1. **"Not Connected" Status for Python Server**
   - Ensure the Python server is running (`python server.py` in the Python directory)
   - Check for errors in the Python console
   - Verify that the ports match in both Unity and Python configurations

2. **"Error checking status" for Ollama**
   - Ensure Ollama is running (`ollama serve`)
   - Verify that the models are pulled correctly
   - Check the Ollama host and port settings

3. **Model Not Working as Expected**
   - Try adjusting the temperature setting (lower for more deterministic outputs)
   - Some tasks might work better with specific models
   - Check if the model has been properly pulled in Ollama

### Logs

- Unity logs are available in the Unity Console
- Python server logs appear in the terminal where you run the server
- Ollama logs are in the terminal where Ollama is running

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
