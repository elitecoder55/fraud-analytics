from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from datetime import datetime
import logging

from routers import health
from models.scorer import FraudScorer

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="FraudShield ML Service",
    version="1.0.0"
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

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
    risk_score: float
    flags: list[str]
    model_version: str
    latency_ms: float


@app.post("/predict", response_model=RiskResponse)
async def predict(tx: TransactionRequest):
    start = datetime.now()
    try:
        result = scorer.score(tx.dict())
        latency = (datetime.now() - start).total_seconds() * 1000
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
    }


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=True)