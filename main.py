"""
FraudShield ML Microservice
----------------------------
FastAPI service that scores incoming transactions using:
  1. A trained Isolation Forest model (unsupervised anomaly detection)
  2. A Random Forest classifier (trained on labelled fraud data)
  3. A statistical rules engine as fallback / ensemble weight

.NET backend calls POST /predict; response time target: < 100ms
"""

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import Optional
import uvicorn
import numpy as np
import joblib
import os
import logging
from datetime import datetime

from routers import health
from models.scorer import FraudScorer

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="FraudShield ML Service",
    description="Real-time fraud risk scoring microservice",
    version="1.0.0"
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

# Load/init scorer at startup (model lives in memory for low latency)
scorer = FraudScorer()
app.include_router(health.router)


class TransactionRequest(BaseModel):
    transaction_id: str
    amount: float
    merchant_id: str
    location: str
    transaction_type: str = "PURCHASE"
    hour_of_day: int = 12
    day_of_week: int = 1
    currency: str = "USD"


class RiskResponse(BaseModel):
    risk_score: float          # 0.0 – 1.0
    flags: list[str]           # Human-readable reasons
    model_version: str
    latency_ms: float


@app.post("/predict", response_model=RiskResponse)
async def predict(tx: TransactionRequest):
    """
    Score a transaction. Called by .NET backend for every incoming transaction.
    Returns risk_score in [0, 1] and a list of triggered rule flags.
    """
    start = datetime.now()
    try:
        result = scorer.score(tx.dict())
        latency = (datetime.now() - start).total_seconds() * 1000
        logger.info(f"Scored {tx.transaction_id}: {result['risk_score']:.3f} ({latency:.1f}ms)")
        return RiskResponse(
            risk_score=result["risk_score"],
            flags=result["flags"],
            model_version=scorer.MODEL_VERSION,
            latency_ms=round(latency, 2)
        )
    except Exception as e:
        logger.error(f"Scoring error: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.get("/model/info")
async def model_info():
    return {
        "model_version": scorer.MODEL_VERSION,
        "features": scorer.FEATURES,
        "threshold_high": 0.7,
        "threshold_medium": 0.4,
    }


if __name__ == "__main__":
    uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=True)
