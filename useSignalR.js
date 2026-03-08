import { useEffect, useState, useRef } from "react";
import * as signalR from "@microsoft/signalr";

export function useSignalR(url) {
  const [isConnected, setIsConnected] = useState(false);
  const connectionRef = useRef(null);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(url)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.onreconnecting(() => setIsConnected(false));
    connection.onreconnected(() => setIsConnected(true));
    connection.onclose(() => setIsConnected(false));

    connection
      .start()
      .then(() => setIsConnected(true))
      .catch((err) => console.error("SignalR connection error:", err));

    connectionRef.current = connection;

    return () => {
      connection.stop();
    };
  }, [url]);

  return { connection: connectionRef.current, isConnected };
}
