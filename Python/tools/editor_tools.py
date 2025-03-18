from mcp.server.fastmcp import FastMCP, Context
from typing import Optional
from unity_connection import get_unity_connection

def register_editor_tools(mcp: FastMCP):
    """Register all editor control tools with the MCP server."""
    
    @mcp.tool()
    def undo(ctx: Context) -> str:
        """Undo the last action performed in the Unity editor.
        
        Returns:
            str: Success message or error details
        """
        try:
            response = get_unity_connection().send_command("EDITOR_CONTROL", {
                "command": "UNDO"
            })
            return response.get("message", "Undo performed successfully")
        except Exception as e:
            return f"Error performing undo: {str(e)}"

    @mcp.tool()
    def redo(ctx: Context) -> str:
        """Redo the last undone action in the Unity editor.
        
        Returns:
            str: Success message or error details
        """
        try:
            response = get_unity_connection().send_command("EDITOR_CONTROL", {
                "command": "REDO"
            })
            return response.get("message", "Redo performed successfully")
        except Exception as e:
            return f"Error performing redo: {str(e)}"

    @mcp.tool()
    def play(ctx: Context) -> str:
        """Start the game in play mode within the Unity editor.
        
        Returns:
            str: Success message or error details
        """
        try:
            response = get_unity_connection().send_command("EDITOR_CONTROL", {
                "command": "PLAY"
            })
            return response.get("message", "Entered play mode")
        except Exception as e:
            return f"Error entering play mode: {str(e)}"

    @mcp.tool()
    def pause(ctx: Context) -> str:
        """Pause the game while in play mode.
        
        Returns:
            str: Success message or error details
        """
        try:
            response = get_unity_connection().send_command("EDITOR_CONTROL", {
                "command": "PAUSE"
            })
            return response.get("message", "Game paused")
        except Exception as e:
            return f"Error pausing game: {str(e)}"

    @mcp.tool()
    def stop(ctx: Context) -> str:
        """Stop the game and exit play mode.
        
        Returns:
            str: Success message or error details
        """
        try:
            response = get_unity_connection().send_command("EDITOR_CONTROL", {
                "command": "STOP"
            })
            return response.get("message", "Exited play mode")
        except Exception as e:
            return f"Error stopping game: {str(e)}"

    @mcp.tool()
    def build(ctx: Context, platform: str, build_path: str) -> str:
        """Build the project for a specified platform.
        
        Args:
            platform: Target platform (windows, mac, linux, android, ios, webgl)
            build_path: Path where the build should be saved
            
        Returns:
            str: Success message or error details
        """
        try:
            # Validate platform
            valid_platforms = ["windows", "mac", "linux", "android", "ios", "webgl"]
            if platform.lower() not in valid_platforms:
                return f"Error: '{platform}' is not a valid platform. Valid platforms are: {', '.join(valid_platforms)}"
            
            # Check if build_path exists and is writable
            import os
            
            # Check if the directory exists
            build_dir = os.path.dirname(build_path)
            if not os.path.exists(build_dir):
                return f"Error: Build directory '{build_dir}' does not exist. Please create it first."
            
            # Check if the directory is writable
            if not os.access(build_dir, os.W_OK):
                return f"Error: Build directory '{build_dir}' is not writable."
            
            # If the build path itself exists, check if it's a file or directory 
            if os.path.exists(build_path):
                if os.path.isfile(build_path):
                    # If it's a file, check if it's writable
                    if not os.access(build_path, os.W_OK):
                        return f"Error: Existing build file '{build_path}' is not writable."
                elif os.path.isdir(build_path):
                    # If it's a directory, check if it's writable
                    if not os.access(build_path, os.W_OK):
                        return f"Error: Existing build directory '{build_path}' is not writable."
            
            response = get_unity_connection().send_command("EDITOR_CONTROL", {
                "command": "BUILD",
                "params": {
                    "platform": platform,
                    "buildPath": build_path
                }
            })
            return response.get("message", "Build completed successfully")
        except Exception as e:
            return f"Error building project: {str(e)}"

    @mcp.tool()
    def execute_command(ctx: Context, command_name: str, validate_command: bool = True) -> str:
        """Execute a specific editor command or custom script within the Unity editor.
        
        Args:
            command_name: Name of the editor command to execute (e.g., "Edit/Preferences")
            validate_command: Whether to validate the command existence before executing (default: True)
            
        Returns:
            str: Success message or error details
        """
        try:
            unity = get_unity_connection()
            
            # Optionally validate if the command exists
            if validate_command:
                # Get a list of available commands from Unity
                available_commands = unity.send_command("EDITOR_CONTROL", {
                    "command": "GET_AVAILABLE_COMMANDS"
                }).get("commands", [])
                
                # Check if the command exists in the list
                if available_commands and command_name not in available_commands:
                    # If command doesn't exist, try to find similar commands as suggestions
                    similar_commands = [cmd for cmd in available_commands if command_name.lower() in cmd.lower()]
                    suggestion_msg = ""
                    if similar_commands:
                        suggestion_msg = f" Did you mean one of these: {', '.join(similar_commands[:5])}" 
                        if len(similar_commands) > 5:
                            suggestion_msg += " or others?"
                        else:
                            suggestion_msg += "?"
                    
                    return f"Error: Command '{command_name}' not found.{suggestion_msg}"
            
            response = unity.send_command("EDITOR_CONTROL", {
                "command": "EXECUTE_COMMAND",
                "params": {
                    "commandName": command_name
                }
            })
            return response.get("message", f"Executed command: {command_name}")
        except Exception as e:
            return f"Error executing command: {str(e)}" 