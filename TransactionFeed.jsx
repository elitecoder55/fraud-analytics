import { useRef, useEffect } from "react";

const RISK_LABELS = { high: "HIGH RISK", medium: "MEDIUM", low: "LOW" };

function getRiskLevel(score) {
  if (score >= 0.7) return "high";
  if (score >= 0.4) return "medium";
  return "low";
}

function formatAmount(amount) {
  return new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" }).format(amount);
}

export default function TransactionFeed({ transactions }) {
  const listRef = useRef(null);

  useEffect(() => {
    if (listRef.current) listRef.current.scrollTop = 0;
  }, [transactions.length]);

  return (
    <div className="panel">
      <div className="panel-header">
        <h2>Live Transaction Feed</h2>
        <span className="badge">{transactions.length} loaded</span>
      </div>
      <div className="transaction-list" ref={listRef}>
        {transactions.length === 0 && (
          <div className="empty-state">Waiting for transactions...</div>
        )}
        {transactions.map((tx) => {
          const level = getRiskLevel(tx.riskScore);
          return (
            <div key={tx.transactionId} className={`tx-row tx-${level}`}>
              <div className="tx-main">
                <span className="tx-id">#{tx.transactionId?.slice(0, 8)}</span>
                <span className="tx-merchant">{tx.merchantName}</span>
                <span className="tx-amount">{formatAmount(tx.amount)}</span>
              </div>
              <div className="tx-meta">
                <span className="tx-location">{tx.location}</span>
                <span className={`tx-risk risk-${level}`}>
                  {RISK_LABELS[level]} — {(tx.riskScore * 100).toFixed(1)}%
                </span>
                <span className="tx-time">
                  {new Date(tx.timestamp).toLocaleTimeString()}
                </span>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
