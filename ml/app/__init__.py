"""EnhancedBlocker ML sidecar (M2).

On-demand FastAPI service spawned by the .NET backend during focus sessions.
Importing this package exposes the FastAPI instance as ``app`` so the documented
uvicorn target ``ml.app:app`` resolves to the application object.

This is the M2 *skeleton*: every model-backed endpoint returns a typed stub so
the service runs without multi-gigabyte model downloads. Each stub is marked
``TODO`` with the concrete package + model that will replace it.
"""

from .main import app

__all__ = ["app"]
