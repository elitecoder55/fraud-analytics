import { useMemo } from "react";
import {
  AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, BarChart, Bar, Cell, Legend
} from "recharts";

export default function RiskChart({ transactions }) {
  const timelineData = useMemo(() => {
    const last20 = transactions.slice(0, 20).reverse();
    return last20.map((tx, i) => ({
      index: i + 1,
      risk: parseFloat((tx.riskScore * 100).toFixed(1)),
      amount: parseFloat(tx.amount.toFixed(2)),
      id: tx.transactionId?.slice(0, 6),
    }));
  }, [transactions]);

  const distributionData = useMemo(() => {
    const high = transactions.filter((t) => t.riskScore >= 0.7).length;
    const medium = transactions.filter((t) => t.riskScore >= 0.4 && t.riskScore < 0.7).length;
    const low = transactions.filter((t) => t.riskScore < 0.4).length;
    return [
      { name: "Low Risk", count: low, color: "#22c55e" },
      { name: "Medium", count: medium, color: "#f59e0b" },
      { name: "High Risk", count: high, color: "#ef4444" },
    ];
  }, [transactions]);

  return (
    <div className="panel">
      <div className="panel-header">
        <h2>Risk Score Timeline</h2>
        <span className="badge">Last 20 transactions</span>
      </div>
      <div style={{ height: 200 }}>
        <ResponsiveContainer width="100%" height="100%">
          <AreaChart data={timelineData} margin={{ top: 5, right: 10, left: -20, bottom: 0 }}>
            <defs>
              <linearGradient id="riskGradient" x1="0" y1="0" x2="0" y2="1">
                <stop offset="5%" stopColor="#ef4444" stopOpacity={0.4} />
                <stop offset="95%" stopColor="#ef4444" stopOpacity={0} />
              </linearGradient>
            </defs>
            <CartesianGrid strokeDasharray="3 3" stroke="#2d3748" />
            <XAxis dataKey="id" tick={{ fontSize: 10, fill: "#94a3b8" }} />
            <YAxis domain={[0, 100]} tick={{ fontSize: 10, fill: "#94a3b8" }} />
            <Tooltip
              contentStyle={{ background: "#1e293b", border: "1px solid #334155", borderRadius: 6 }}
              formatter={(val) => [`${val}%`, "Risk Score"]}
            />
            <Area
              type="monotone" dataKey="risk"
              stroke="#ef4444" strokeWidth={2}
              fill="url(#riskGradient)"
            />
          </AreaChart>
        </ResponsiveContainer>
      </div>

      <div className="panel-header" style={{ marginTop: 16 }}>
        <h2>Risk Distribution</h2>
      </div>
      <div style={{ height: 160 }}>
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={distributionData} margin={{ top: 5, right: 10, left: -20, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#2d3748" />
            <XAxis dataKey="name" tick={{ fontSize: 11, fill: "#94a3b8" }} />
            <YAxis tick={{ fontSize: 10, fill: "#94a3b8" }} />
            <Tooltip
              contentStyle={{ background: "#1e293b", border: "1px solid #334155", borderRadius: 6 }}
            />
            <Bar dataKey="count" radius={[4, 4, 0, 0]}>
              {distributionData.map((entry, index) => (
                <Cell key={index} fill={entry.color} />
              ))}
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}
