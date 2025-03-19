import asyncio
import json
import logging
import os
import sys
from typing import Dict, Any, List, Optional, Tuple
from ollama_connection import get_ollama_connection, OllamaConnection
from config import config
import traceback
try:
    from fastmcp.ollama_prompt import get_system_prompt
except ImportError:
    # デフォルトのシステムプロンプト関数を定義
    def get_system_prompt():
        return config.ollama_system_prompt

# Configure logging
logging.basicConfig(
    level=getattr(logging, config.log_level),
    format=config.log_format
)
logger = logging.getLogger("UnityMCP_TCP")

# Global connection variables
_ollama_connection: Optional[OllamaConnection] = None

class MCPTCPServer:
    """Custom TCP server for Unity-MCP communication"""
    
    def __init__(self, host: str = 'localhost', port: int = 6500):
        self.host = host
        self.port = port
        self.server = None
        
    async def start(self):
        """Start the TCP server"""
        # Initialize ollama connection
        global _ollama_connection
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
        
        # Start server
        self.server = await asyncio.start_server(
            self.handle_client, self.host, self.port
        )
        
        addr = self.server.sockets[0].getsockname()
        logger.info(f'TCP Server running on {addr[0]}:{addr[1]}')
        
        async with self.server:
            await self.server.serve_forever()
    
    async def handle_client(self, reader: asyncio.StreamReader, writer: asyncio.StreamWriter):
        """Handle TCP client connection"""
        addr = writer.get_extra_info('peername')
        logger.info(f'Connection from {addr}')
        
        while True:
            try:
                # Read data until we get a complete message
                data = await reader.read(8192)
                if not data:
                    break
                
                message = data.decode('utf-8')
                logger.debug(f"Received: {message[:100]}...")
                
                # Special handling for ping
                if message.strip() == "ping":
                    response = json.dumps({"status": "success", "result": {"message": "pong"}})
                    writer.write(response.encode('utf-8'))
                    await writer.drain()
                    continue
                
                # Process the command
                try:
                    command = json.loads(message)
                    response = await self.process_command(command)
                except json.JSONDecodeError:
                    response = json.dumps({
                        "status": "error",
                        "error": "Invalid JSON format",
                        "receivedText": message[:50] + "..." if len(message) > 50 else message
                    })
                
                # Send response
                writer.write(response.encode('utf-8'))
                await writer.drain()
                
            except Exception as e:
                logger.error(f"Error handling client: {str(e)}")
                traceback.print_exc()
                try:
                    error_response = json.dumps({
                        "status": "error",
                        "error": str(e)
                    })
                    writer.write(error_response.encode('utf-8'))
                    await writer.drain()
                except:
                    pass
                break
        
        logger.info(f'Closing connection from {addr}')
        writer.close()
        await writer.wait_closed()
    
    async def process_command(self, command: Dict[str, Any]) -> str:
        """Process command and return JSON response string"""
        global _ollama_connection
        
        command_type = command.get("type")
        params = command.get("params", {})
        
        logger.info(f"Processing command: {command_type}")
        
        try:
            if not command_type:
                return json.dumps({
                    "status": "error",
                    "error": "Command type cannot be empty"
                })
            
            # Process user request (chat with Ollama)
            if command_type == "process_user_request":
                return await self.handle_process_user_request(params)
            
            # Get Ollama status
            elif command_type == "get_ollama_status":
                return await self.handle_get_ollama_status()
            
            # Configure Ollama
            elif command_type == "configure_ollama":
                return await self.handle_configure_ollama(params)
            
            # Default for unknown commands
            else:
                return json.dumps({
                    "status": "success",
                    "result": {
                        "message": f"Command {command_type} was received but not implemented",
                        "commandType": command_type,
                        "paramsCount": len(params) if params else 0
                    }
                })
        
        except Exception as e:
            logger.error(f"Error processing command: {str(e)}")
            traceback.print_exc()
            return json.dumps({
                "status": "error",
                "error": str(e),
                "stackTrace": traceback.format_exc()
            })
    
    async def handle_process_user_request(self, params: Dict[str, Any]) -> str:
        """Handle process_user_request command"""
        global _ollama_connection
        
        prompt = params.get("prompt", "")
        if not prompt:
            return json.dumps({
                "status": "error",
                "result": {
                    "status": "error",
                    "message": "Prompt cannot be empty",
                    "llm_response": "Please provide a prompt to process."
                }
            })
        
        # Ensure Ollama connection
        if not _ollama_connection:
            try:
                _ollama_connection = await get_ollama_connection()
                is_connected = await _ollama_connection.test_connection()
                if not is_connected:
                    return json.dumps({
                        "status": "error",
                        "result": {
                            "status": "error",
                            "message": "Could not connect to Ollama",
                            "llm_response": "Sorry, I couldn't connect to the Ollama service. Please check that Ollama is running."
                        }
                    })
            except Exception as e:
                return json.dumps({
                    "status": "error",
                    "result": {
                        "status": "error",
                        "message": f"Ollama connection error: {str(e)}",
                        "llm_response": "Sorry, there was an error connecting to Ollama."
                    }
                })
        
        try:
            # 新しいシステムプロンプト取得関数を使用
            system_prompt = get_system_prompt()
            
            # Ollamaに送信
            response_text, full_response = await _ollama_connection.get_completion(
                prompt=prompt,
                system_prompt=system_prompt,
                temperature=config.ollama_temperature
            )
            
            if not response_text:
                return json.dumps({
                    "status": "error",
                    "result": {
                        "status": "error",
                        "message": "Received empty response from Ollama",
                        "llm_response": ""
                    }
                })
            
            # Extract commands from the response
            commands = await _ollama_connection.extract_mcp_commands(response_text)
            
            # ログに抽出されたコマンドを出力
            logger.info(f"Extracted {len(commands)} commands from response")
            for cmd in commands:
                logger.info(f"Command: {json.dumps(cmd)}")
            
            return json.dumps({
                "status": "success",
                "result": {
                    "status": "success",
                    "message": f"Processed request with {len(commands)} commands",
                    "llm_response": response_text,
                    "commands_executed": 0,  # Unity側で実行します
                    "commands": commands,
                    "results": []
                }
            })
            
        except Exception as e:
            logger.error(f"Error processing request: {str(e)}")
            traceback.print_exc()
            return json.dumps({
                "status": "error",
                "result": {
                    "status": "error",
                    "message": f"Error processing request: {str(e)}",
                    "llm_response": "Sorry, there was an error processing your request."
                }
            })
    
    async def handle_get_ollama_status(self) -> str:
        """Handle get_ollama_status command"""
        global _ollama_connection
        
        try:
            if not _ollama_connection:
                _ollama_connection = await get_ollama_connection()
                
            is_connected = await _ollama_connection.test_connection()
            
            return json.dumps({
                "status": "success",
                "result": {
                    "status": "connected" if is_connected else "disconnected",
                    "model": _ollama_connection.model,
                    "host": _ollama_connection.host,
                    "port": _ollama_connection.port
                }
            })
        except Exception as e:
            logger.error(f"Error checking Ollama status: {str(e)}")
            return json.dumps({
                "status": "error",
                "result": {
                    "status": "error",
                    "message": f"Error checking Ollama status: {str(e)}"
                }
            })
    
    async def handle_configure_ollama(self, params: Dict[str, Any]) -> str:
        """Handle configure_ollama command"""
        global _ollama_connection
        
        host = params.get("host")
        port = params.get("port")
        model = params.get("model")
        temperature = params.get("temperature")
        system_prompt = params.get("system_prompt")
        
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
            
            return json.dumps({
                "status": "success",
                "result": {
                    "status": "connected" if is_connected else "error",
                    "message": "Ollama configuration updated successfully" if is_connected else "Failed to connect with new settings",
                    "config": {
                        "host": config.ollama_host,
                        "port": config.ollama_port,
                        "model": config.ollama_model,
                        "temperature": config.ollama_temperature
                    }
                }
            })
        except Exception as e:
            logger.error(f"Error connecting to Ollama with new settings: {str(e)}")
            return json.dumps({
                "status": "error",
                "result": {
                    "status": "error",
                    "message": f"Error connecting to Ollama: {str(e)}",
                    "config": {
                        "host": config.ollama_host,
                        "port": config.ollama_port,
                        "model": config.ollama_model,
                        "temperature": config.ollama_temperature
                    }
                }
            })

# Main application
async def main():
    # Create and start the TCP server
    server = MCPTCPServer()
    logger.info(f"Starting TCP server on {server.host}:{server.port}")
    await server.start()

if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        logger.info("Server stopped by user")
    except Exception as e:
        logger.error(f"Server error: {str(e)}")
        traceback.print_exc()
