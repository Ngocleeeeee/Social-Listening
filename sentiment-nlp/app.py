"""NLP sentiment microservice — transformer model (multilingual, VN + EN).
Exposes POST /analyze {text} -> {label, score}. Used by Analysis.Worker via HTTP."""
import os
from fastapi import FastAPI
from pydantic import BaseModel
from transformers import pipeline

MODEL = os.getenv("MODEL", "cardiffnlp/twitter-xlm-roberta-base-sentiment")
clf = pipeline("sentiment-analysis", model=MODEL)

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
