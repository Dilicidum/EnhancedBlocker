# EnhancedBlocker — ML Sidecar (M2)

On-demand **Python / FastAPI** service that provides the Tier-1 machine-learning
signals for the EnhancedBlocker decision cascade. It is **spawned and killed by
the .NET backend**, runs on loopback only, and exists exactly while it is needed
(focus sessions / brief on-demand bursts).

> **Status: M2 skeleton.** Every model-backed endpoint returns a typed *stub* so
> the service runs without any multi-gigabyte model download. Each stub is marked
> `TODO` in the code with the concrete package + model that replaces it. See
> [Swapping in the real models](#swapping-in-the-real-models).

See the design docs in the repo root:
[ARCHITECTURE.md](../ARCHITECTURE.md) (ML design, cascade tiers),
[BUILD_PLAN.md](../BUILD_PLAN.md) (lifecycle contract),
[TECH_PLAN.md](../TECH_PLAN.md) (model choices).

---

## Layout

```
ml/
  app/
    __init__.py      # exposes `app` so `ml.app:app` resolves
    main.py          # FastAPI app + endpoints (the stubs live here)
    models.py        # Pydantic request/response models (the wire contract)
  tests/
    test_api.py      # smoke tests: /health + stub shapes
  requirements.txt   # LIGHT deps the skeleton imports (fastapi/uvicorn/pydantic + test)
  requirements-ml.txt# HEAVY model deps (only when wiring real models)
  .gitignore         # ignores .venv/, __pycache__/, model caches
  README.md
```

---

## Quick start (skeleton, no model downloads)

Run from the **repo root** (so `ml.app` is importable):

```bash
# 1. Create the venv (gitignored)
python -m venv ml/.venv

# 2. Activate it
#   Windows PowerShell:
ml\.venv\Scripts\Activate.ps1
#   bash:
source ml/.venv/bin/activate

# 3. Install the light deps only
pip install -r ml/requirements.txt

# 4. Run the sidecar on port 5181
python -m uvicorn ml.app:app --port 5181
```

Health check: `GET http://127.0.0.1:5181/health` → `{"status":"ok"}`.

Run the tests:

```bash
python -m pytest ml/tests -q
```

---

## On-demand lifecycle contract (owned by .NET)

The .NET backend **owns the sidecar process**. The sidecar itself is stateless
and knows nothing about the lifecycle — it just serves requests while alive.

| Phase | Who | What happens |
|-------|-----|--------------|
| **Spawn** | .NET | Launches `python -m uvicorn ml.app:app --port 5181` as a child process on **focus-session start** (preferred — warms models before the first block), or lazily on the first Tier-1 need. |
| **Readiness** | .NET | Polls `GET /health` until it returns `200 {"status":"ok"}` before routing any decision to Tier 1. |
| **Serve** | sidecar | Answers `/embed`, `/zeroshot`, `/score` for the cascade. |
| **Kill** | .NET | Terminates the child process on **focus-session end**, or on an **idle timeout** (no Tier-1 request for N minutes and no active focus session). |

**Cold-start behaviour (real models take a few seconds to load):**
- **During a focus session**, while warming → **fail closed** (block + show
  "checking…" in the extension overlay).
- **Outside a focus session** → **fail open** (allow unless Tier 0 blocks); in
  fact outside sessions only Tier 0 runs and Python stays off entirely.

Port: **5181** (loopback). Transport: REST over `http://127.0.0.1:5181`.

---

## Endpoint contract

All bodies are JSON. Shapes are defined as Pydantic models in
[`app/models.py`](app/models.py) and are stable even though M2 returns stubs.
Stub responses include `"stub": true`.

### `GET /health`
Readiness probe.
```json
{ "status": "ok" }
```

### `POST /embed`
Embed one or more texts (page title/text, declared intent, prototype URLs).
```jsonc
// request
{ "texts": ["how to write a rust parser", "celebrity gossip"] }
// response (stub: zero vectors of width 384)
{ "model": "all-MiniLM-L6-v2-stub", "dim": 384,
  "embeddings": [[0.0, ...], [0.0, ...]], "stub": true }
```

### `POST /zeroshot`
Zero-shot category classification (NLI).
```jsonc
// request
{ "text": "...", "labels": ["news", "entertainment", "educational", "work"] }
// response (stub: uniform scores; labels sorted high→low)
{ "model": "valhalla/distilbart-mnli-12-3-stub",
  "labels": ["news", "entertainment", "educational", "work"],
  "scores": [0.25, 0.25, 0.25, 0.25], "stub": true }
```

### `POST /score`
Combine the three Tier-1 features (ARCHITECTURE.md) into a decision:
**(a)** prototype similarity, **(b)** intent relevance, **(c)** zero-shot category.
```jsonc
// request
{ "prototype_similarity": 0.42, "intent_relevance": 0.08,
  "category": "news", "category_score": 0.91, "in_focus_session": true }
// response (stub: neutral allow, never blocks)
{ "outcome": "allow", "score": 0.0, "reason": "stub combiner: ...", "stub": true }
```
`outcome` is `allow` / `block` / `pending`, mirroring the .NET cascade `Outcome`
enum. The `pending` value exists for the slow-path/warming case.

---

## Swapping in the real models

Install the heavy deps first:

```bash
pip install -r ml/requirements.txt -r ml/requirements-ml.txt
```

Then replace each stub in [`app/main.py`](app/main.py) (search for `TODO`):

| Endpoint | Stub → Real | Package | Model |
|----------|-------------|---------|-------|
| `/embed` | zero vectors → real embeddings | `sentence-transformers` | `all-MiniLM-L6-v2` (384-dim, CPU) |
| `/zeroshot` | uniform scores → NLI scores | `transformers` (zero-shot pipeline) | `valhalla/distilbart-mnli-12-3` (CPU) or `facebook/bart-large-mnli` |
| `/score` | neutral allow → learned decision | `scikit-learn` | nearest-centroid (few-shot) → logistic regression (M3) |

Sketches (also in the docstrings):

```python
# /embed
from sentence_transformers import SentenceTransformer
_model = SentenceTransformer("all-MiniLM-L6-v2")          # load once at startup
embeddings = _model.encode(texts, normalize_embeddings=True).tolist()

# /zeroshot
from transformers import pipeline
_clf = pipeline("zero-shot-classification",
                model="valhalla/distilbart-mnli-12-3")    # load once at startup
out = _clf(text, labels, multi_label=True)                # out["labels"], out["scores"]

# /score (day one: interpretable rule over the 3 calibrated signals;
#         M3: scikit-learn LogisticRegression persisted + reloaded)
```

**Load models once at process start** (module level or FastAPI lifespan), not per
request — model loading is the cold-start cost the .NET warm-on-focus-start hides.
When you do, update `requirements.txt` notes / drop the `-stub` suffixes so callers
can tell real responses from placeholders, and (optionally) verify CPU `torch`
wheels for Python 3.12.
