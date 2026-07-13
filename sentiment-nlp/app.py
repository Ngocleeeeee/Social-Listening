"""NLP sentiment microservice — transformer model (multilingual, VN + EN).
Exposes POST /analyze {text} -> {label, score}. Used by Analysis.Worker via HTTP."""
import os
import torch
from fastapi import FastAPI
from pydantic import BaseModel
from transformers import pipeline

# Cap intra-op threads. Uvicorn runs the sync endpoint on a threadpool, so several requests can hit
# the model at once; letting torch fan each one across every core oversubscribes the CPU (the ~400%
# spikes) and hurts aggregate throughput. A small fixed count parallelises requests, not matmuls.
torch.set_num_threads(int(os.getenv("TORCH_THREADS", "2")))

MODEL = os.getenv("MODEL", "cardiffnlp/twitter-xlm-roberta-base-sentiment")
# truncation/max_length guard against long inputs blowing up token count (the char slice below is
# only a coarse pre-cut; the tokenizer decides real length).
clf = pipeline("sentiment-analysis", model=MODEL, truncation=True, max_length=512)

LABELS = {
    "negative": "Negative", "neutral": "Neutral", "positive": "Positive",
    "label_0": "Negative", "label_1": "Neutral", "label_2": "Positive",
}

app = FastAPI(title="BrandRadar Sentiment NLP")


class Req(BaseModel):
    text: str = ""


@app.get("/health")
def health():
    return {"status": "ok", "model": MODEL}


@app.post("/analyze")
def analyze(r: Req):
    text = (r.text or "").strip()[:512]
    if not text:
        return {"label": "Neutral", "score": 0.0}
    res = clf(text)[0]                       # {'label':..., 'score':...}
    label = LABELS.get(str(res["label"]).lower(), "Neutral")
    conf = float(res["score"])
    signed = conf if label == "Positive" else -conf if label == "Negative" else 0.0
    return {"label": label, "score": round(signed, 3)}
