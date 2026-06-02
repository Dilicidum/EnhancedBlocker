"""Smoke tests for the ML sidecar skeleton: /health and the three stub shapes.

Run from the repo root so ``ml.app`` is importable:

    python -m pytest ml/tests -q
"""

from __future__ import annotations

from fastapi.testclient import TestClient

from ml.app import app
from ml.app.models import EMBEDDING_DIM

client = TestClient(app)


def test_health() -> None:
    resp = client.get("/health")
    assert resp.status_code == 200
    assert resp.json() == {"status": "ok"}


def test_embed_shape() -> None:
    resp = client.post("/embed", json={"texts": ["hello", "world"]})
    assert resp.status_code == 200
    body = resp.json()
    assert body["stub"] is True
    assert body["dim"] == EMBEDDING_DIM
    assert len(body["embeddings"]) == 2
    assert all(len(vec) == EMBEDDING_DIM for vec in body["embeddings"])


def test_embed_empty() -> None:
    resp = client.post("/embed", json={"texts": []})
    assert resp.status_code == 200
    assert resp.json()["embeddings"] == []


def test_zeroshot_shape() -> None:
    labels = ["news", "entertainment", "work"]
    resp = client.post(
        "/zeroshot", json={"text": "some article", "labels": labels}
    )
    assert resp.status_code == 200
    body = resp.json()
    assert body["stub"] is True
    assert set(body["labels"]) == set(labels)
    assert len(body["scores"]) == len(labels)
    assert all(0.0 <= s <= 1.0 for s in body["scores"])


def test_score_shape() -> None:
    resp = client.post(
        "/score",
        json={
            "prototype_similarity": 0.4,
            "intent_relevance": 0.1,
            "category": "news",
            "category_score": 0.9,
            "in_focus_session": True,
        },
    )
    assert resp.status_code == 200
    body = resp.json()
    assert body["stub"] is True
    assert body["outcome"] in {"allow", "block", "pending"}
    assert 0.0 <= body["score"] <= 1.0
    assert isinstance(body["reason"], str) and body["reason"]


def test_score_validates_feature_range() -> None:
    # prototype_similarity is bounded to [-1, 1]; out-of-range is a 422.
    resp = client.post(
        "/score",
        json={"prototype_similarity": 5.0, "intent_relevance": 0.0},
    )
    assert resp.status_code == 422
