FROM python:3.11-slim
WORKDIR /app

# CPU-only PyTorch + NLP libs
RUN pip install --no-cache-dir torch --index-url https://download.pytorch.org/whl/cpu
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app.py .

# Bake the model into the image so startup is fast (no runtime download)
ENV MODEL=cardiffnlp/twitter-xlm-roberta-base-sentiment
RUN python -c "from transformers import pipeline; pipeline('sentiment-analysis', model='${MODEL}')"

EXPOSE 8000
CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "8000"]
