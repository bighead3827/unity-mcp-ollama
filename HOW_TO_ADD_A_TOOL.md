# How to Add a New Tool

This guide explains how to add new tools to the Unity MCP Ollama package. Tools are Python functions that can be called by the LLM to perform actions in the Unity Editor.

## 1. Create a Tool Function

Tools are organized in Python files based on their functionality. Choose an existing file or create a new one in the `Python/tools/` directory.

A tool function follows this structure:

```python
from unity_connection import get_unity_connection
from mcp.server.fastmcp import FastMCP
from typing import Dict, Any, List, Optional

def my_new_tool(param1: str, param2: int = 0) -> Dict[str, Any]:
    """
    Description of what your tool does.
    
    Parameters:
    - param1: Description of param1
    - param2: Description of param2 with default value
    
    Returns:
    Dictionary with result data
    """
    # Get the Unity connection
    unity = get_unity_connection()
    
    # Prepare parameters to send to Unity
    params = {
        "param1": param1,
        "param2": param2
    }
    
    # Send command to Unity and return the result
    result = unity.send_command("my_new_command", params)
    return result
```

## 2. Register the Tool

Add your tool to the appropriate registration function in your tool module file.

For example, if you added a new scene-related tool in `scene_tools.py`:

```python
def register_scene_tools(mcp: FastMCP):
    """Register all scene-related tools with the MCP server."""
    mcp.tool(name="get_current_scene")(get_current_scene)
    mcp.tool(name="get_scene_list")(get_scene_list)
    
    # Add your new tool here
    mcp.tool(name="my_new_scene_tool")(my_new_scene_tool)
```

If you created a new tool module, update `Python/tools/__init__.py` to import and register your tools.

## 3. Implement the Unity Side

Now you need to implement the Unity command handler that your Python tool will call:

1. Create a new C# file in `Editor/Commands/` or add to an existing one.
2. Implement a command handler that follows this pattern:

```csharp
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Commands
{
    public class MyNewCommand : ICommand
    {
        public object Execute(Dictionary<string, object> parameters)
        {
            // Extract parameters
            string param1 = parameters.Get<string>("param1");
            int param2 = parameters.Get<int>("param2", 0); // Default value if not provided
            
            // Implement command logic
            // ...
            
            // Return results
            return new {
                success = true,
                message = "Operation completed",
                // Additional result data
            };
        }
    }
}
```

3. Register your command in `UnityMCPBridge.cs` by adding it to the command dictionary:

```csharp
private void RegisterCommands()
{
    // Existing commands...
    
    // Your new command
    commands.Add("my_new_command", new MyNewCommand());
}
```

## 4. Documentation

Update the asset creation strategy in `server.py` to include your new tool, explaining when and how to use it.

## 5. Testing

Test your tool by:

1. Starting the Unity Editor
2. Running the MCP server
3. Using the local LLM to invoke your tool via natural language

## Best Practices

- Include detailed docstrings for your Python functions
- Add proper error handling in both Python and C# code
- Follow the existing coding style and naming conventions
- Use meaningful names that indicate the tool's purpose
- Keep functions focused on a single responsibility
- Validate parameters before sending commands to Unity