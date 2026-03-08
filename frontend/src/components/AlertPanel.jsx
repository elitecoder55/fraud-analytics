export default function AlertPanel({ alerts }) {
  return (
    <div className="panel">
      <div className="panel-header">
        <h2>🚨 Fraud Alerts</h2>
        <span className="badge alert-badge">{alerts.length}</span>
      </div>
      <div className="alert-list">
        {alerts.length === 0 && (
          <div className="empty-state">No alerts yet. System monitoring...</div>
        )}
        {alerts.map((alert, i) => (
          <div key={i} className="alert-item">
            <div className="alert-title">{alert.reason}</div>
            <div className="alert-meta">
              <span>TXN #{alert.transactionId?.slice(0, 8)}</span>
              <span className="alert-score">Score: {(alert.riskScore * 100).toFixed(1)}%</span>
            </div>
            <div className="alert-time">{new Date(alert.timestamp).toLocaleTimeString()}</div>
          </div>
        ))}
      </div>
    </div>
  );
}