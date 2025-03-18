"""
Ollama connection module for Unity MCP.
This module handles communication with local LLMs via Ollama.
"""

import json
import logging
import httpx
from dataclasses import dataclass
from typing import Dict, Any, Optional, List, Tuple
from config import config

# Configure logging
logger = logging.getLogger("UnityMCP.Ollama")

@dataclass
class OllamaConnection:
    """Manages the connection to Ollama service."""
    
    host: str = config.ollama_host
    port: int = config.ollama_port
    model: str = config.ollama_model
    timeout: float = config.ollama_timeout
    
    def __post_init__(self):
        self.base_url = f"http://{self.host}:{self.port}"
        logger.info(f"Initialized Ollama connection to {self.base_url} using model {self.model}")
    
    async def test_connection(self) -> bool:
        """Test if Ollama is reachable and the model is available."""
        try:
            async with httpx.AsyncClient(timeout=self.timeout) as client:
                # Check if the Ollama server is up
                response = await client.get(f"{self.base_url}/api/tags")
                if response.status_code != 200:
                    logger.error(f"Ollama server returned status {response.status_code}")
                    return False
                
                # Check if our model is available
                models = response.json().get("models", [])
                for model_info in models:
                    if model_info.get("name") == self.model:
                        logger.info(f"Successfully connected to Ollama, model {self.model} is available")
                        return True
                
                logger.error(f"Model {self.model} not found in Ollama")
                return False
                
        except Exception as e:
            logger.error(f"Failed to connect to Ollama: {str(e)}")
            return False

    async def get_completion(self, prompt: str, system_prompt: Optional[str] = None, 
                           temperature: float = 0.7) -> Tuple[str, Dict[str, Any]]:
        """
        Get a completion from Ollama.
        
        Args:
            prompt: The user's prompt
            system_prompt: Optional system instructions
            temperature: Controls randomness (0-1)
            
        Returns:
            Tuple of (generated_text, full_response_data)
        """
        try:
            request_data = {
                "model": self.model,
                "prompt": prompt,
                "temperature": temperature,
                "stream": False,
            }
            
            if system_prompt:
                request_data["system"] = system_prompt
                
            logger.info(f"Sending completion request to Ollama for model {self.model}")
            logger.debug(f"Request data: {request_data}")
            
            async with httpx.AsyncClient(timeout=self.timeout) as client:
                response = await client.post(
                    f"{self.base_url}/api/generate",
                    json=request_data
                )
                
                if response.status_code != 200:
                    error_msg = f"Ollama API returned status {response.status_code}: {response.text}"
                    logger.error(error_msg)
                    return "", {"error": error_msg}
                
                result = response.json()
                generated_text = result.get("response", "")
                logger.info(f"Received {len(generated_text)} chars from Ollama")
                
                return generated_text, result
                
        except Exception as e:
            error_msg = f"Error getting completion from Ollama: {str(e)}"
            logger.error(error_msg)
            return "", {"error": error_msg}

    async def extract_mcp_commands(self, llm_response: str) -> List[Dict[str, Any]]:
        """
        Extract MCP commands from the LLM's response text.
        
        This function parses the LLM output and extracts function calls intended 
        for the MCP protocol.
        
        Args:
            llm_response: The raw text response from the LLM
            
        Returns:
            List of parsed MCP commands as dictionaries
        """
        commands = []
        
        # Different LLMs format their function calls differently
        # We'll look for common patterns and extract the relevant JSON
        
        # Look for Python function call patterns
        if "(" in llm_response and ")" in llm_response:
            import re
            
            # Match patterns like: function_name(arg1="value", arg2=123)
            function_calls = re.findall(r'(\w+)\s*\((.*?)\)', llm_response)
            
            for func_name, args_str in function_calls:
                try:
                    # Parse the arguments
                    args_dict = {}
                    
                    # Handle empty args
                    if not args_str.strip():
                        commands.append({"function": func_name, "arguments": {}})
                        continue
                        
                    # Extract key-value pairs
                    key_value_pairs = re.findall(r'(\w+)\s*=\s*("[^"]*"|\'[^\']*\'|\[[^\]]*\]|\{[^\}]*\}|[^,]+)', args_str)
                    
                    for key, raw_value in key_value_pairs:
                        # Clean up the value
                        value = raw_value.strip()
                        
                        # Handle string values
                        if (value.startswith('"') and value.endswith('"')) or \
                           (value.startswith("'") and value.endswith("'")):
                            value = value[1:-1]
                        # Handle lists
                        elif value.startswith('[') and value.endswith(']'):
                            try:
                                value = json.loads(value.replace("'", '"'))
                            except:
                                pass
                        # Handle numeric values
                        elif value.replace('.', '', 1).isdigit():
                            value = float(value) if '.' in value else int(value)
                        # Handle booleans
                        elif value.lower() == 'true':
                            value = True
                        elif value.lower() == 'false':
                            value = False
                            
                        args_dict[key] = value
                        
                    commands.append({"function": func_name, "arguments": args_dict})
                except Exception as e:
                    logger.warning(f"Failed to parse function call {func_name}: {str(e)}")
        
        # Look for JSON patterns (used by some models)
        json_matches = []
        try:
            # Find JSON-like structures between curly braces
            import re
            potential_jsons = re.findall(r'\{[^{}]*\}', llm_response)
            
            for json_str in potential_jsons:
                try:
                    parsed = json.loads(json_str)
                    if isinstance(parsed, dict) and ('function' in parsed or 'name' in parsed):
                        # Structure the command properly
                        function_name = parsed.get('function') or parsed.get('name')
                        args = parsed.get('arguments') or parsed.get('params') or parsed.get('args') or {}
                        
                        commands.append({"function": function_name, "arguments": args})
                except:
                    pass
        except Exception as e:
            logger.warning(f"Error parsing JSON in response: {str(e)}")
        
        # If we couldn't extract any commands, log a warning
        if not commands:
            logger.warning(f"Could not extract any MCP commands from response: {llm_response[:100]}...")
            
        return commands

# Global Ollama connection instance
_ollama_connection = None

async def get_ollama_connection() -> OllamaConnection:
    """Get or create the global Ollama connection."""
    global _ollama_connection
    
    if _ollama_connection is None:
        _ollama_connection = OllamaConnection()
        
    return _ollama_connection