export default function StatsBar({ stats }) {
  const flaggedPct = stats.total > 0
    ? ((stats.flagged / stats.total) * 100).toFixed(1)
    : "0.0";

  return (
    <div className="stats-bar">
      <div className="stat-card">
        <span className="stat-value">{stats.total.toLocaleString()}</span>
        <span className="stat-label">Total Transactions</span>
      </div>
      <div className="stat-card stat-danger">
        <span className="stat-value">{stats.flagged.toLocaleString()}</span>
        <span className="stat-label">Flagged ({flaggedPct}%)</span>
      </div>
      <div className="stat-card stat-success">
        <span className="stat-value">{stats.safe.toLocaleString()}</span>
        <span className="stat-label">Safe</span>
      </div>
      <div className="stat-card stat-warn">
        <span className="stat-value">{stats.avgRisk}</span>
        <span className="stat-label">Avg Risk Score</span>
      </div>
    </div>
  );
}