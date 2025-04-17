from mcp.server.fastmcp import FastMCP, Context, Image
import logging
import asyncio
from dataclasses import dataclass
from contextlib import asynccontextmanager
from typing import AsyncIterator, Dict, Any, List, Optional
import sys
import os
from config import config
from tools import register_all_tools
from unity_connection import get_unity_connection, UnityConnection
from ollama_connection import get_ollama_connection, OllamaConnection

# Configure logging using settings from config
log_file = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'unity_mcp.log')
logging.basicConfig(
    level=getattr(logging, config.log_level),
    format=config.log_format,
    handlers=[
        logging.StreamHandler(sys.stdout),
        logging.FileHandler(log_file)
    ]
)
logger = logging.getLogger("UnityMCP")
logger.setLevel(getattr(logging, config.log_level))

# Global connection states
_unity_connection: Optional[UnityConnection] = None
_ollama_connection: Optional[OllamaConnection] = None

@asynccontextmanager
async def server_lifespan(server: FastMCP) -> AsyncIterator[Dict[str, Any]]:
    """Handle server startup and shutdown."""
    global _unity_connection, _ollama_connection
    logger.info("UnityMCP server starting up")
    
    # Connect to Unity
    try:
        _unity_connection = get_unity_connection()
        logger.info("Connected to Unity on startup")
    except Exception as e:
        logger.warning(f"Could not connect to Unity on startup: {str(e)}")
        _unity_connection = None
    
    # Connect to Ollama
    try:
        _ollama_connection = await get_ollama_connection()
        is_connected = await _ollama_connection.test_connection()
        if is_connected:
            logger.info(f"Connected to Ollama with model {_ollama_connection.model}")
        else:
            logger.warning(f"Ollama connection test failed")
    except Exception as e:
        logger.warning(f"Could not connect to Ollama on startup: {str(e)}")
        _ollama_connection = None
    
    try:
        yield {}
    finally:
        if _unity_connection:
            _unity_connection.disconnect()
            _unity_connection = None
        logger.info("UnityMCP server shut down")

# Initialize MCP server
mcp = FastMCP(
    "UnityMCP",
    description="Unity Editor integration via Model Context Protocol with Ollama",
    lifespan=server_lifespan
)

# Register all tools
register_all_tools(mcp)

# Asset Creation Strategy
@mcp.prompt()
def asset_creation_strategy() -> str:
    """Guide for creating and managing assets in Unity."""
    return (
        "Unity MCP Server Tools and Best Practices:\n\n"
        "1. **Editor Control**\n"
        "   - `editor_action` - Performs editor-wide actions such as `PLAY`, `PAUSE`, `STOP`, `BUILD`, `SAVE`\n"
        "2. **Scene Management**\n"
        "   - `get_current_scene()`, `get_scene_list()` - Get scene details\n"
        "   - `open_scene(path)`, `save_scene(path)` - Open/save scenes\n"
        "   - `new_scene(path)`, `change_scene(path, save_current)` - Create/switch scenes\n\n"
        "3. **Object Management**\n"
        "   - ALWAYS use `find_objects_by_name(name)` to check if an object exists before creating or modifying it\n"
        "   - `create_object(name, type)` - Create objects (e.g. `CUBE`, `SPHERE`, `EMPTY`, `CAMERA`)\n"
        "   - `delete_object(name)` - Remove objects\n"
        "   - `set_object_transform(name, location, rotation, scale)` - Modify object position, rotation, and scale\n"
        "   - `add_component(name, component_type)` - Add components to objects (e.g. `Rigidbody`, `BoxCollider`)\n"
        "   - `remove_component(name, component_type)` - Remove components from objects\n"
        "   - `get_object_properties(name)` - Get object properties\n"
        "   - `find_objects_by_name(name)` - Find objects by name\n"
        "   - `get_hierarchy()` - Get object hierarchy\n"
        "4. **Script Management**\n"
        "   - ALWAYS use `list_scripts(folder_path)` or `view_script(path)` to check if a script exists before creating or updating it\n"
        "   - `create_script(name, type, namespace, template)` - Create scripts\n"
        "   - `view_script(path)`, `update_script(path, content)` - View/modify scripts\n"
        "   - `attach_script(object_name, script_name)` - Add scripts to objects\n"
        "   - `list_scripts(folder_path)` - List scripts in folder\n\n"
        "5. **Asset Management**\n"
        "   - ALWAYS use `get_asset_list(type, search_pattern, folder)` to check if an asset exists before creating or importing it\n"
        "   - `import_asset(source_path, target_path)` - Import external assets\n"
        "   - `instantiate_prefab(path, pos_x, pos_y, pos_z, rot_x, rot_y, rot_z)` - Create prefab instances\n"
        "   - `create_prefab(object_name, path)`, `apply_prefab(object_name, path)` - Manage prefabs\n"
        "   - `get_asset_list(type, search_pattern, folder)` - List project assets\n"
        "   - Use relative paths for Unity assets (e.g., 'Assets/Models/MyModel.fbx')\n"
        "   - Use absolute paths for external files\n\n"
        "6. **Material Management**\n"
        "   - ALWAYS check if a material exists before creating or modifying it\n"
        "   - `set_material(object_name, material_name, color)` - Apply/create materials\n"
        "   - Use RGB colors (0.0-1.0 range)\n\n"
        "7. **Best Practices**\n"
        "   - ALWAYS verify existence before creating or updating any objects, scripts, assets, or materials\n"
        "   - Use meaningful names for objects and scripts\n"
        "   - Keep scripts organized in folders with namespaces\n"
        "   - Verify changes after modifications\n"
        "   - Save scenes before major changes\n"
        "   - Use full component names (e.g., 'Rigidbody', 'BoxCollider')\n"
        "   - Provide correct value types for properties\n"
        "   - Keep prefabs in dedicated folders\n"
        "   - Regularly apply prefab changes\n"
    )

# Add new Ollama-specific functionality
@mcp.tool()
async def process_user_request(ctx: Context, prompt: str) -> Dict[str, Any]:
    """
    Process a natural language request using Ollama and execute the resulting Unity commands.
    
    Args:
        prompt: The user's natural language request for Unity modifications
    
    Returns:
        A dictionary containing the result of the operations
    """
    global _ollama_connection, _unity_connection
    
    # Ensure both connections are available
    if not _ollama_connection:
        try:
            _ollama_connection = await get_ollama_connection()
            is_connected = await _ollama_connection.test_connection()
            if not is_connected:
                return {"status": "error", "message": "Could not connect to Ollama"}
        except Exception as e:
            return {"status": "error", "message": f"Ollama connection error: {str(e)}"}
    
    if not _unity_connection:
        try:
            _unity_connection = get_unity_connection()
        except Exception as e:
            return {"status": "error", "message": f"Unity connection error: {str(e)}"}
    
    # Get asset creation strategy for context
    strategy = asset_creation_strategy()
    
    # Combine the prompt with the strategy
    full_system_prompt = config.ollama_system_prompt + "\n\nAvailable functions:\n" + strategy
    
    try:
        # Get response from Ollama
        response_text, full_response = await _ollama_connection.get_completion(
            prompt=prompt,
            system_prompt=full_system_prompt,
            temperature=config.ollama_temperature
        )
        
        if not response_text:
            return {
                "status": "error", 
                "message": "Received empty response from Ollama",
                "llm_response": ""
            }
        
        # Extract commands from the response
        commands = await _ollama_connection.extract_mcp_commands(response_text)
        
        if not commands:
            return {
                "status": "success", 
                "message": "No executable commands found in LLM response",
                "llm_response": response_text,
                "commands_executed": 0,
                "results": []
            }
        
        # Execute each extracted command
        results = []
        for cmd in commands:
            try:
                function_name = cmd.get("function")
                arguments = cmd.get("arguments", {})
                
                logger.info(f"Executing command: {function_name} with args: {arguments}")
                
                # Execute the command using Unity connection
                result = _unity_connection.send_command(function_name, arguments)
                results.append({
                    "command": function_name,
                    "arguments": arguments,
                    "result": result,
                    "success": True
                })
            except Exception as e:
                logger.error(f"Error executing command {cmd}: {str(e)}")
                results.append({
                    "command": cmd.get("function"),
                    "arguments": cmd.get("arguments", {}),
                    "error": str(e),
                    "success": False
                })
        
        return {
            "status": "success",
            "message": f"Executed {len(results)} commands",
            "llm_response": response_text,
            "commands_executed": len(results),
            "results": results
        }
        
    except Exception as e:
        logger.error(f"Error processing request: {str(e)}")
        return {
            "status": "error",
            "message": f"Error processing request: {str(e)}"
        }

@mcp.tool()
async def get_ollama_status() -> Dict[str, Any]:
    """
    Check the status of the Ollama connection and model.
    
    Returns:
        A dictionary containing the Ollama connection status
    """
    global _ollama_connection
    
    try:
        if not _ollama_connection:
            _ollama_connection = await get_ollama_connection()
            
        is_connected = await _ollama_connection.test_connection()
        
        return {
            "status": "connected" if is_connected else "disconnected",
            "model": _ollama_connection.model,
            "host": _ollama_connection.host,
            "port": _ollama_connection.port
        }
    except Exception as e:
        logger.error(f"Error checking Ollama status: {str(e)}")
        return {
            "status": "error",
            "message": f"Error checking Ollama status: {str(e)}"
        }

@mcp.tool()
async def configure_ollama(host: str = None, port: int = None, 
                         model: str = None, temperature: float = None,
                         system_prompt: str = None) -> Dict[str, Any]:
    """
    Configure Ollama connection settings.
    
    Args:
        host: Ollama host address
        port: Ollama port number
        model: Model name to use
        temperature: Temperature setting (0.0-1.0)
        system_prompt: System prompt to use for all requests
        
    Returns:
        Updated configuration status
    """
    global _ollama_connection
    
    # Update config values
    if host is not None:
        config.ollama_host = host
    
    if port is not None:
        config.ollama_port = port
    
    if model is not None:
        config.ollama_model = model
    
    if temperature is not None:
        config.ollama_temperature = max(0.0, min(1.0, temperature))
    
    if system_prompt is not None:
        config.ollama_system_prompt = system_prompt
    
    # Save config
    config.save_to_file()
    
    # Reset connection to apply new settings
    _ollama_connection = await get_ollama_connection()
    
    # Test connection with new settings
    try:
        is_connected = await _ollama_connection.test_connection()
        
        return {
            "status": "connected" if is_connected else "error",
            "message": "Ollama configuration updated successfully" if is_connected else "Failed to connect with new settings",
            "config": {
                "host": config.ollama_host,
                "port": config.ollama_port,
                "model": config.ollama_model,
                "temperature": config.ollama_temperature
            }
        }
    except Exception as e:
        logger.error(f"Error connecting to Ollama with new settings: {str(e)}")
        return {
            "status": "error",
            "message": f"Error connecting to Ollama: {str(e)}",
            "config": {
                "host": config.ollama_host,
                "port": config.ollama_port,
                "model": config.ollama_model,
                "temperature": config.ollama_temperature
            }
        }

# MCP Server settings
MCP_SERVER_PORT = 6500

# Run the server
if __name__ == "__main__":
    logger.info("Starting MCP server using stdio transport")
    try:
        # FastMCPライブラリが'tcp'または'port'パラメータをサポートしていないようなので、
        # stdio トランスポートだけを使用します
        mcp.run(transport='stdio')
    except Exception as e:
        logger.error(f"Error starting MCP server: {e}")
        logger.info("Falling back to default mode")
        mcp.run()  # パラメータなしでデフォルト動作