"""
Configuration management for the backend service.
"""

import os
import sys
import logging
from dataclasses import dataclass, field
from typing import Optional
from pathlib import Path

from dotenv import load_dotenv


def is_frozen_app() -> bool:
    """判断当前后端是否以 PyInstaller 冻结后的 exe 方式运行。"""
    return bool(getattr(sys, "frozen", False))


def get_install_root() -> Path:
    """获取安装根目录；源码运行时返回仓库根目录，exe 运行时返回 exe 所在目录。"""
    if is_frozen_app():
        return Path(sys.executable).resolve().parent

    return Path(__file__).resolve().parent.parent.parent


def get_backend_dir() -> Path:
    """获取后端资源目录，安装包中用于存放 .env、data 和 ast_python。"""
    root_dir = get_install_root()
    packaged_backend_dir = root_dir / "backend"

    if packaged_backend_dir.exists():
        return packaged_backend_dir

    return Path(__file__).resolve().parent.parent


def get_frontend_dist_dir() -> Path:
    """获取前端静态文件目录，安装版由后端直接托管该目录。"""
    return get_install_root() / "frontend" / "dist"


def get_env_file_path() -> Path:
    """获取 .env 文件路径，安装版和源码版统一由后端资源目录承载。"""
    return get_backend_dir() / ".env"


# Load environment variables from the packaged/source backend resource directory.
load_dotenv(dotenv_path=get_env_file_path())


@dataclass
class VolcengineConfig:
    """Volcengine AST API configuration."""
    ws_url: str = "wss://openspeech.bytedance.com/api/v4/ast/v2/translate"
    app_key: str = ""
    access_key: str = ""
    resource_id: str = "volc.service_type.10053"


@dataclass
class AudioConfig:
    """Audio format configuration."""
    # Volcengine AST 2.0 s2s input follows the official demo metadata.
    source_format: str = "wav"
    source_rate: int = 16000
    source_bits: int = 16
    source_channel: int = 1
    
    # Target audio (TTS output)
    target_format: str = "ogg_opus"
    target_rate: int = 24000


@dataclass
class ServerConfig:
    """Server configuration."""
    host: str = "0.0.0.0"
    port: int = 3100
    debug: bool = False


@dataclass
class Config:
    """Main configuration class."""
    volcengine: VolcengineConfig = field(default_factory=VolcengineConfig)
    audio: AudioConfig = field(default_factory=AudioConfig)
    server: ServerConfig = field(default_factory=ServerConfig)
    
    # Paths
    base_dir: Path = field(default_factory=get_backend_dir)
    install_root: Path = field(default_factory=get_install_root)
    frontend_dist_dir: Path = field(default_factory=get_frontend_dist_dir)
    env_file_path: Path = field(default_factory=get_env_file_path)
    protobuf_dir: Optional[Path] = None
    
    def __post_init__(self):
        # Set protobuf directory (for AST SDK)
        if self.protobuf_dir is None:
            self.protobuf_dir = self.base_dir / "ast_python"


def load_config() -> Config:
    """Load configuration from environment variables."""
    
    volcengine = VolcengineConfig(
        app_key=os.getenv("VOLC_APP_ID", ""),
        access_key=os.getenv("VOLC_ACCESS_KEY", ""),
        resource_id=os.getenv("VOLC_RESOURCE_ID", "volc.service_type.10053"),
    )
    
    server = ServerConfig(
        host=os.getenv("HOST", "0.0.0.0"),
        port=int(os.getenv("PORT", 3100)),
        debug=os.getenv("DEBUG", "false").lower() == "true",
    )
    
    config = Config(
        volcengine=volcengine,
        server=server,
    )
    
    return config


def validate_config(config: Config) -> bool:
    """Validate configuration and check required values."""
    
    if not config.volcengine.app_key or not config.volcengine.access_key:
        logging.error("[ERROR] VOLC_APP_ID or VOLC_ACCESS_KEY is not set!")
        logging.error("Please add them to your .env file:")
        logging.error("  VOLC_APP_ID=your_app_id")
        logging.error("  VOLC_ACCESS_KEY=your_access_key")
        return False
    
    # Check if protobuf directory exists
    if config.protobuf_dir and not config.protobuf_dir.exists():
        logging.warning(f"[WARN] Protobuf directory not found: {config.protobuf_dir}")
    
    return True


# Global configuration instance
_config: Optional[Config] = None


def get_config() -> Config:
    """Get the global configuration instance."""
    global _config
    if _config is None:
        _config = load_config()
    return _config
