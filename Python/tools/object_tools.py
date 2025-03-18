"""Tools for inspecting and manipulating Unity objects."""

from typing import Optional, List, Dict, Any
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection

def register_object_tools(mcp: FastMCP):
    """Register all object inspection and manipulation tools with the MCP server."""
    
    @mcp.tool()
    def get_object_properties(
        ctx: Context,
        name: str
    ) -> Dict[str, Any]:
        """Get all properties of a specified game object.

        Args:
            ctx: The MCP context
            name: Name of the game object to inspect

        Returns:
            Dict containing the object's properties, components, and their values
        """
        try:
            response = get_unity_connection().send_command("GET_OBJECT_PROPERTIES", {
                "name": name
            })
            return response
        except Exception as e:
            return {"error": f"Failed to get object properties: {str(e)}"}

    @mcp.tool()
    def get_component_properties(
        ctx: Context,
        object_name: str,
        component_type: str
    ) -> Dict[str, Any]:
        """Get properties of a specific component on a game object.

        Args:
            ctx: The MCP context
            object_name: Name of the game object
            component_type: Type of the component to inspect

        Returns:
            Dict containing the component's properties and their values
        """
        try:
            response = get_unity_connection().send_command("GET_COMPONENT_PROPERTIES", {
                "object_name": object_name,
                "component_type": component_type
            })
            return response
        except Exception as e:
            return {"error": f"Failed to get component properties: {str(e)}"}

    @mcp.tool()
    def find_objects_by_name(
        ctx: Context,
        name: str
    ) -> List[Dict[str, str]]:
        """Find game objects in the scene by name.

        Args:
            ctx: The MCP context
            name: Name to search for (partial matches are supported)

        Returns:
            List of dicts containing object names and their paths
        """
        try:
            response = get_unity_connection().send_command("FIND_OBJECTS_BY_NAME", {
                "name": name
            })
            return response.get("objects", [])
        except Exception as e:
            return [{"error": f"Failed to find objects: {str(e)}"}]

    @mcp.tool()
    def find_objects_by_tag(
        ctx: Context,
        tag: str
    ) -> List[Dict[str, str]]:
        """Find game objects in the scene by tag.

        Args:
            ctx: The MCP context
            tag: Tag to search for

        Returns:
            List of dicts containing object names and their paths
        """
        try:
            response = get_unity_connection().send_command("FIND_OBJECTS_BY_TAG", {
                "tag": tag
            })
            return response.get("objects", [])
        except Exception as e:
            return [{"error": f"Failed to find objects: {str(e)}"}]

    @mcp.tool()
    def get_scene_info(ctx: Context) -> Dict[str, Any]:
        """Get information about the current scene.

        Args:
            ctx: The MCP context

        Returns:
            Dict containing scene information including name and root objects
        """
        try:
            response = get_unity_connection().send_command("GET_SCENE_INFO")
            return response
        except Exception as e:
            return {"error": f"Failed to get scene info: {str(e)}"}

    @mcp.tool()
    def get_hierarchy(ctx: Context) -> Dict[str, Any]:
        """Get the current hierarchy of game objects in the scene.

        Args:
            ctx: The MCP context

        Returns:
            Dict containing the scene hierarchy as a tree structure
        """
        try:
            response = get_unity_connection().send_command("GET_HIERARCHY")
            return response
        except Exception as e:
            return {"error": f"Failed to get hierarchy: {str(e)}"}

    @mcp.tool()
    def select_object(
        ctx: Context,
        name: str
    ) -> Dict[str, str]:
        """Select a game object in the Unity Editor.

        Args:
            ctx: The MCP context
            name: Name of the object to select

        Returns:
            Dict containing the name of the selected object
        """
        try:
            response = get_unity_connection().send_command("SELECT_OBJECT", {
                "name": name
            })
            return response
        except Exception as e:
            return {"error": f"Failed to select object: {str(e)}"}

    @mcp.tool()
    def get_selected_object(ctx: Context) -> Optional[Dict[str, str]]:
        """Get the currently selected game object in the Unity Editor.

        Args:
            ctx: The MCP context

        Returns:
            Dict containing the selected object's name and path, or None if no object is selected
        """
        try:
            response = get_unity_connection().send_command("GET_SELECTED_OBJECT")
            return response.get("selected")
        except Exception as e:
            return {"error": f"Failed to get selected object: {str(e)}"}

    @mcp.tool()
    def get_asset_list(
        ctx: Context,
        type: Optional[str] = None,
        search_pattern: str = "*",
        folder: str = "Assets"
    ) -> List[Dict[str, str]]:
        """Get a list of assets in the project.

        Args:
            ctx: The MCP context
            type: Optional asset type to filter by
            search_pattern: Pattern to search for in asset names
            folder: Folder to search in (default: "Assets")

        Returns:
            List of dicts containing asset information
        """
        try:
            response = get_unity_connection().send_command("GET_ASSET_LIST", {
                "type": type,
                "search_pattern": search_pattern,
                "folder": folder
            })
            return response.get("assets", [])
        except Exception as e:
            return [{"error": f"Failed to get asset list: {str(e)}"}] 