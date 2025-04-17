"""
Configuration settings for the Unity MCP Server with Ollama integration.
This file contains all configurable parameters for the server.
"""

from dataclasses import dataclass
import os
import json
import logging

@dataclass
class ServerConfig:
    """Main configuration class for the MCP server."""
    
    # Network settings for Unity connection
    unity_host: str = "localhost"
    unity_port: int = 6400
    mcp_port: int = 6500
    
    # Connection settings
    connection_timeout: float = 15.0
    buffer_size: int = 32768
    
    # Logging settings
    log_level: str = "DEBUG"
    log_format: str = "%(asctime)s - %(name)s - %(levelname)s - %(message)s"
    
    # Server settings
    max_retries: int = 3
    retry_delay: float = 1.0
    
    # Ollama settings
    ollama_host: str = "localhost"
    ollama_port: int = 11434
    ollama_model: str = "llama3"  # Default model
    ollama_timeout: float = 120.0  # Longer timeout for LLM operations
    ollama_temperature: float = 0.7
    ollama_system_prompt: str = """You are a Unity development assistant that helps control the Unity Editor via commands.
    
When asked to perform an action in Unity, you should call the appropriate function.
Always respond with valid function calls when the user requests to modify the Unity scene.
Use the most appropriate functions from the available tools.

When working in Unity, follow these guidelines:
1. Check if objects exist before modifying them using find_objects_by_name()
2. Use descriptive names for any objects you create
3. Set appropriate transforms (position, rotation, scale) for new objects
4. Use proper colors for materials (RGB values between 0-1)
5. Save the scene after making significant changes
6. Use proper component names when adding components

Remember that your function calls will be parsed and executed directly in Unity, so ensure they are correct."""

    def __init__(self):
        """Initialize configuration and load from config file if available."""
        self._load_from_file()
    
    def _load_from_file(self):
        """Load configuration from a local config file if available."""
        config_file = os.path.join(os.path.dirname(__file__), 'local_config.json')
        if os.path.exists(config_file):
            try:
                with open(config_file, 'r') as f:
                    config_data = json.load(f)
                
                # Update fields from config file
                for key, value in config_data.items():
                    if hasattr(self, key):
                        setattr(self, key, value)
                
                logging.info(f"Loaded configuration from {config_file}")
            except Exception as e:
                logging.warning(f"Failed to load config from {config_file}: {str(e)}")
    
    def save_to_file(self):
        """Save the current configuration to a local config file."""
        config_file = os.path.join(os.path.dirname(__file__), 'local_config.json')
        try:
            # Convert to dictionary
            config_dict = {key: getattr(self, key) for key in self.__annotations__}
            
            with open(config_file, 'w') as f:
                json.dump(config_dict, f, indent=2)
            
            logging.info(f"Saved configuration to {config_file}")
            return True
        except Exception as e:
            logging.error(f"Failed to save config to {config_file}: {str(e)}")
            return False

# Create a global config instance
config = ServerConfig() 