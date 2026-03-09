import { useState, useEffect } from "react";
import TransactionFeed from "./components/TransactionFeed";
import RiskChart from "./components/RiskChart";
import AlertPanel from "./components/AlertPanel";
import StatsBar from "./components/StatsBar";
import { useSignalR } from "./hooks/useSignalR";
import "./index.css";

export default function App() {
  const [transactions, setTransactions] = useState([]);
  const [alerts, setAlerts] = useState([]);
  const [stats, setStats] = useState({ total: 0, flagged: 0, safe: 0, avgRisk: 0 });
  const { connection, isConnected } = useSignalR("https://fraudshield-api-2vjn.onrender.com/hubs/transactions");

  useEffect(() => {
    if (!connection) return;

    connection.on("ReceiveTransaction", (tx) => {
      setTransactions((prev) => [tx, ...prev].slice(0, 100));
      setStats((prev) => {
        const flagged = prev.flagged + (tx.riskScore >= 0.7 ? 1 : 0);
        const total = prev.total + 1;
        return {
          total,
          flagged,
          safe: total - flagged,
          avgRisk: ((prev.avgRisk * prev.total + tx.riskScore) / total).toFixed(3),
        };
      });
    });

    connection.on("ReceiveAlert", (alert) => {
      setAlerts((prev) => [alert, ...prev].slice(0, 50));
    });

    return () => {
      connection.off("ReceiveTransaction");
      connection.off("ReceiveAlert");
    };
  }, [connection]);

  return (
    <div className="app-container">
      <header className="app-header">
        <div className="header-left">
          <span className="logo">🛡️ FraudShield</span>
          <span className="subtitle">Real-Time Analytics Platform</span>
        </div>
        <div className="header-right">
          <span className={`connection-badge ${isConnected ? "connected" : "disconnected"}`}>
            {isConnected ? "● LIVE" : "○ OFFLINE"}
          </span>
        </div>
      </header>

      <StatsBar stats={stats} />

      <main className="main-grid">
        <section className="section-feed">
          <TransactionFeed transactions={transactions} />
        </section>
        <section className="section-chart">
          <RiskChart transactions={transactions} />
        </section>
        <section className="section-alerts">
          <AlertPanel alerts={alerts} />
        </section>
      </main>
    </div>
  );
}