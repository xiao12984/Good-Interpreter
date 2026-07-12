"""
PyInstaller backend entry point.

This thin wrapper keeps the frozen executable entry stable while the real
application code remains in app.main.
"""

import sys


def configure_stdio() -> None:
    """Force UTF-8 stdout/stderr so launcher logs do not become mojibake."""
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="replace")


configure_stdio()

from app.main import main

# PyInstaller cannot see the generated AST protobuf modules because they are
# loaded dynamically from backend/ast_python at runtime. Keep these imports
# explicit so the frozen backend contains the google.protobuf runtime package.
from google.protobuf import descriptor as _protobuf_descriptor
from google.protobuf import descriptor_pool as _protobuf_descriptor_pool
from google.protobuf import runtime_version as _protobuf_runtime_version
from google.protobuf import symbol_database as _protobuf_symbol_database
from google.protobuf.internal import builder as _protobuf_builder


if __name__ == "__main__":
    main()
