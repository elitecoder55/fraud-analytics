"""
FraudScorer
-----------
Ensemble model combining:
  • Isolation Forest (unsupervised) – detects statistical outliers
  • Random Forest Classifier (supervised) – trained on historical labelled fraud
  • Rule Engine – explicit business rules (velocity, amount thresholds)

In production: train on real labelled data, save with joblib, load here.
For portfolio demo: trains on synthetic data at startup.
"""

import numpy as np
from sklearn.ensemble import IsolationForest, RandomForestClassifier
from sklearn.preprocessing import StandardScaler
import joblib
import os
import logging

logger = logging.getLogger(__name__)


class FraudScorer:
    MODEL_VERSION = "1.0.0-demo"
    FEATURES = ["amount", "hour_of_day", "day_of_week",
                 "is_international", "is_high_risk_merchant",
                 "amount_zscore", "is_odd_hour"]

    # Known high-risk merchant categories (simplified)
    HIGH_RISK_MERCHANTS = {"M190", "M191", "M192", "M193", "M194", "M195"}

    def __init__(self):
        self.scaler = StandardScaler()
        self.iso_forest = IsolationForest(contamination=0.05, random_state=42, n_estimators=100)
        self.rf_clf = RandomForestClassifier(n_estimators=100, random_state=42, n_jobs=-1)
        self._train_on_synthetic_data()

    def _train_on_synthetic_data(self):
        """
        Train on synthetic transaction data.
        PRODUCTION: Replace with: self.rf_clf = joblib.load('models/rf_model.pkl')
        """
        logger.info("Training fraud models on synthetic data...")
        np.random.seed(42)
        n = 10_000

        # Generate realistic-looking transaction features
        amounts = np.concatenate([
            np.random.lognormal(4, 1, int(n * 0.95)),   # Normal transactions
            np.random.uniform(5000, 10000, int(n * 0.05))  # Fraud: high amounts
        ])
        hours = np.random.randint(0, 24, n)
        days = np.random.randint(0, 7, n)
        is_intl = np.random.binomial(1, 0.15, n)
        is_high_risk = np.random.binomial(1, 0.08, n)
        odd_hour = (hours < 5).astype(int)

        # Z-score amount per-card (simplified: global here)
        mean_amt, std_amt = amounts.mean(), amounts.std()
        amt_zscore = np.abs((amounts - mean_amt) / (std_amt + 1e-8))

        X = np.column_stack([amounts, hours, days, is_intl, is_high_risk, amt_zscore, odd_hour])

        # Labels: fraud if high amount + international + odd hour
        y = ((amounts > 4000) & (is_intl == 1)).astype(int)
        y |= ((amounts > 6000)).astype(int)
        y |= ((odd_hour == 1) & (is_high_risk == 1)).astype(int)

        X_scaled = self.scaler.fit_transform(X)
        self.iso_forest.fit(X_scaled)
        self.rf_clf.fit(X_scaled, y)
        logger.info("Models trained. Fraud rate in training data: %.2f%%", y.mean() * 100)

    def _extract_features(self, tx: dict) -> tuple[np.ndarray, list[str]]:
        flags = []
        amount = float(tx.get("amount", 0))
        hour = int(tx.get("hour_of_day", 12))
        day = int(tx.get("day_of_week", 1))
        location = tx.get("location", "").lower()
        merchant_id = tx.get("merchant_id", "")
        tx_type = tx.get("transaction_type", "PURCHASE")

        is_intl = int("us" not in location and "united states" not in location)
        is_high_risk = int(merchant_id in self.HIGH_RISK_MERCHANTS or
                           any(w in location for w in ["unknown", "offshore"]))
        is_odd_hour = int(hour < 5 or hour > 23)

        # Rule-based flag reasons
        if amount > 5000:
            flags.append("LARGE_AMOUNT")
        if is_intl:
            flags.append("INTERNATIONAL_LOCATION")
        if is_odd_hour:
            flags.append("ODD_HOUR_TRANSACTION")
        if is_high_risk:
            flags.append("HIGH_RISK_MERCHANT")
        if tx_type == "CASH_ADVANCE":
            flags.append("CASH_ADVANCE")

        # Simplified z-score (in production: per-card rolling average)
        mean_amt, std_amt = 300.0, 500.0
        amt_zscore = abs((amount - mean_amt) / (std_amt + 1e-8))
        if amt_zscore > 3:
            flags.append("AMOUNT_OUTLIER")

        features = np.array([[amount, hour, day, is_intl, is_high_risk, amt_zscore, is_odd_hour]])
        return features, flags

    def score(self, tx: dict) -> dict:
        features, flags = self._extract_features(tx)
        features_scaled = self.scaler.transform(features)

        # Isolation Forest: -1 = anomaly, 1 = normal → convert to [0,1]
        iso_score = self.iso_forest.score_samples(features_scaled)[0]
        iso_normalized = float(np.clip((iso_score * -1 + 0.5), 0, 1))

        # Random Forest probability of fraud
        rf_prob = float(self.rf_clf.predict_proba(features_scaled)[0][1])

        # Rule engine score (0–1 based on number of flags)
        rule_score = min(len(flags) * 0.2, 1.0)

        # Weighted ensemble
        final_score = 0.4 * rf_prob + 0.35 * iso_normalized + 0.25 * rule_score
        final_score = float(np.clip(final_score, 0.0, 1.0))

        return {"risk_score": round(final_score, 4), "flags": flags}
