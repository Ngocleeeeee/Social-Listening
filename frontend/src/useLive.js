import { useEffect, useState } from 'react'
import * as signalR from '@microsoft/signalr'

/// Single SignalR connection for the app: realtime mentions + crisis alerts.
export function useLive() {
  const [mentions, setMentions] = useState([])
  const [alerts, setAlerts] = useState([])
  const [connected, setConnected] = useState(false)

  useEffect(() => {
    const conn = new signalR.HubConnectionBuilder().withUrl('/hubs/live').withAutomaticReconnect().build()
    conn.on('mention', (m) => setMentions((prev) => (prev.some((x) => x.id === m.id) ? prev : [m, ...prev].slice(0, 60))))
    conn.on('alert', (a) => setAlerts((prev) => [a, ...prev].slice(0, 20)))
    conn.onreconnected(() => setConnected(true))
    conn.onclose(() => setConnected(false))
    conn.start().then(() => setConnected(true)).catch(() => setConnected(false))
    return () => { conn.stop() }
  }, [])

  return { mentions, alerts, connected }
}
