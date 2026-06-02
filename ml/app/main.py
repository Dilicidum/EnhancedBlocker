"""FastAPI application for the EnhancedBlocker ML sidecar (M2 skeleton).

Run with:

    python -m uvicorn ml.app:app --port 5181

Every model-backed endpoint returns a deterministic, typed *stub* so the
service is runnable without downloading sentence-transformers / transformers
weights. Search for ``TODO`` to find each swap point.
"""

from __future__ import annotations

from fastapi import FastAPI

from .models import (
    EMBEDDING_DIM,
    EmbedRequest,
    EmbedResponse,
    HealthResponse,
    Outcome,
    ScoreRequest,
    ScoreResponse,
    ZeroShotRequest,
    ZeroShotResponse,
)

# Model identifiers the real implementations will load (ARCHITECTURE.md / TECH_PLAN.md).
# Suffixed "-stub" so callers can tell a placeholder response from a real one.
_EMBED_MODEL = "all-MiniLM-L6-v2-stub"
_ZEROSHOT_MODEL = "valhalla/distilbart-mnli-12-3-stub"

app = FastAPI(
    title="EnhancedBlocker ML sidecar",
    version="0.1.0-skeleton",
    summary="On-demand Tier-1 ML for the EnhancedBlocker decision cascade (M2 stub).",
)


@app.get("/health", response_model=HealthResponse)
def health() -> HealthResponse:
    """Readiness probe. The .NET backend polls this until the sidecar is up."""
    return HealthResponse(status="ok")


@app.post("/embed", response_model=EmbedResponse)
def embed(request: EmbedRequest) -> EmbedResponse:
    """Return one embedding vector per input text.

    TODO: real sentence-transformers model.
        from sentence_transformers import SentenceTransformer
        model = SentenceTransformer("all-MiniLM-L6-v2")  # 384-dim, CPU-friendly
        embeddings = model.encode(request.texts, normalize_embeddings=True).tolist()
    The stub emits fixed-dimension zero vectors so the contract (shape + dim) is
    exercisable without loading a model.
    """
    embeddings = [[0.0] * EMBEDDING_DIM for _ in request.texts]
    return EmbedResponse(
        model=_EMBED_MODEL,
        dim=EMBEDDING_DIM,
        embeddings=embeddings,
        stub=True,
    )


@app.post("/zeroshot", response_model=ZeroShotResponse)
def zeroshot(request: ZeroShotRequest) -> ZeroShotResponse:
    """Score each candidate label against the text.

    TODO: real NLI zero-shot model.
        from transformers import pipeline
        clf = pipeline("zero-shot-classification",
                       model="valhalla/distilbart-mnli-12-3")  # smaller, CPU-friendly
        out = clf(request.text, request.labels, multi_label=True)
        # out["labels"] / out["scores"] are already sorted high→low.
    The stub assigns a flat uniform score across labels so the response shape
    (labels sorted by score, aligned scores) is exercisable.
    """
    n = len(request.labels)
    uniform = round(1.0 / n, 6) if n else 0.0
    # Sort for a stable, descending-by-score contract (uniform → keep input order).
    labels = list(request.labels)
    scores = [uniform] * n
    return ZeroShotResponse(
        model=_ZEROSHOT_MODEL,
        labels=labels,
        scores=scores,
        stub=True,
    )


@app.post("/score", response_model=ScoreResponse)
def score(request: ScoreRequest) -> ScoreResponse:
    """Combine the Tier-1 features into a block/allow decision.

    TODO: real classifier.
        Day one: a nearest-centroid / interpretable scoring rule over the three
        calibrated signals (prototype similarity, intent relevance, category).
        M3: graduate to scikit-learn LogisticRegression over the same features,
        persisted and reloaded once enough labels accrue.
    The stub returns a neutral, low-confidence decision and never blocks, so it
    is safe to wire into the cascade before a real model exists.
    """
    return ScoreResponse(
        outcome=Outcome.ALLOW,
        score=0.0,
        reason=(
            "stub combiner: no learned model yet — returning neutral allow "
            "(TODO: nearest-centroid → logistic regression)"
        ),
        stub=True,
    )
