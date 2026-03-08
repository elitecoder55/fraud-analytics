from fastapi import APIRouter
from datetime import datetime

router = APIRouter()

@router.get("/health")
async def health():
    return {"status": "healthy", "timestamp": datetime.utcnow().isoformat()}

@router.get("/")
async def root():
    return {"service": "FraudShield ML Microservice", "version": "1.0.0"}