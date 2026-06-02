"""Pydantic request/response models — the explicit, typed wire contract.

These shapes are the contract the .NET backend codes against. They are stable
even though the implementations behind them are still stubs in M2.
"""

from __future__ import annotations

from enum import Enum

from pydantic import BaseModel, Field

# Dimensionality of the real embedding model (all-MiniLM-L6-v2 → 384-dim).
# The stub emits vectors of this width so callers can wire up storage/maths now.
EMBEDDING_DIM: int = 384


# --- /health ---------------------------------------------------------------


class HealthResponse(BaseModel):
    """Readiness probe result polled by the .NET lifecycle manager."""

    status: str = Field("ok", description="'ok' once the service can accept work.")


# --- /embed ----------------------------------------------------------------


class EmbedRequest(BaseModel):
    texts: list[str] = Field(
        ...,
        description="Texts to embed (page title/text, intent, prototype URLs, ...).",
    )


class EmbedResponse(BaseModel):
    """One vector per input text, all of width :data:`EMBEDDING_DIM`."""

    model: str = Field(..., description="Identifier of the embedding model used.")
    dim: int = Field(..., description="Vector dimensionality.")
    embeddings: list[list[float]] = Field(
        ..., description="Row-aligned with the request `texts`."
    )
    stub: bool = Field(
        True, description="True while this is a placeholder (no real model loaded)."
    )


# --- /zeroshot -------------------------------------------------------------


class ZeroShotRequest(BaseModel):
    text: str = Field(..., description="Text to classify.")
    labels: list[str] = Field(
        ..., description="Candidate labels, e.g. ['news', 'entertainment', 'work']."
    )


class ZeroShotResponse(BaseModel):
    """Per-label scores in [0, 1], sorted high→low, aligned with `labels`."""

    model: str = Field(..., description="Identifier of the NLI model used.")
    labels: list[str] = Field(..., description="Labels sorted by descending score.")
    scores: list[float] = Field(..., description="Scores aligned with `labels`.")
    stub: bool = Field(
        True, description="True while this is a placeholder (no real model loaded)."
    )


# --- /score ----------------------------------------------------------------


class Outcome(str, Enum):
    """Mirrors the .NET cascade `Outcome` enum (TECH_PLAN.md)."""

    ALLOW = "allow"
    BLOCK = "block"
    PENDING = "pending"


class ScoreRequest(BaseModel):
    """The Tier-1 decision features described in ARCHITECTURE.md.

    These are the three calibrated signals the combiner weighs:
      (a) prototype_similarity — cosine similarity to the known-bad prototype set,
      (b) intent_relevance     — cosine similarity to the declared focus intent,
      (c) category / category_score — zero-shot category and its confidence.
    """

    prototype_similarity: float = Field(
        ...,
        ge=-1.0,
        le=1.0,
        description="(a) Similarity to known-bad prototypes (nearest-centroid/k-NN).",
    )
    intent_relevance: float = Field(
        ...,
        ge=-1.0,
        le=1.0,
        description="(b) Cosine similarity of the page to the declared focus intent.",
    )
    category: str | None = Field(
        None, description="(c) Top zero-shot category label (e.g. 'news')."
    )
    category_score: float | None = Field(
        None,
        ge=0.0,
        le=1.0,
        description="(c) Confidence of the top category.",
    )
    in_focus_session: bool = Field(
        False,
        description="Whether a focus session is active (affects fail-open/closed).",
    )


class ScoreResponse(BaseModel):
    outcome: Outcome = Field(..., description="allow / block / pending.")
    score: float = Field(
        ..., ge=0.0, le=1.0, description="Confidence the page should be blocked."
    )
    reason: str = Field(..., description="Human-readable explanation of the decision.")
    stub: bool = Field(
        True, description="True while this is a placeholder (no learned combiner)."
    )
