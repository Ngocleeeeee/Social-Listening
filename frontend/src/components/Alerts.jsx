import { useEffect, useState, useCallback } from 'react'
import { getAlerts, ackAlert, getSummary } from '../api'
import { timeAgo } from '../time'

export default function Alerts({ live }) {
  const [history, setHistory] = useState([])
  const [err, setErr] = useState(null)
  const [acking, setAcking] = useState(null)
  const [summary, setSummary] = useState(null)

  const load = useCallback(async () => {
    try { setHistory((await getAlerts()) || []); setErr(null) } catch (e) { setErr(e.message) }
  }, [])
  useEffect(() => { load(); const t = setInterval(load, 10000); return () => clearInterval(t) }, [load])

  const ack = async (id) => {
    setAcking(id)
    try { await ackAlert(id); await load() } finally { setAcking(null) }
  }

  // merge realtime with history, dedup by id (history has acknowledged flag)
  const map = new Map()
  ;[...(live?.alerts || []), ...history].forEach((a) => {
    if (!a || !a.id) return
    const prev = map.get(a.id)
    map.set(a.id, { ...a, acknowledged: a.acknowledged || prev?.acknowledged })
  })
  const all = [...map.values()].sort((a, b) => new Date(b['@timestamp'] || b.createdAt) - new Date(a['@timestamp'] || a.createdAt))
  const open = all.filter((a) => !a.acknowledged)
  const latestBrand = all[0]?.brand || ''

  const loadSummary = async () => {
    try { setSummary(await getSummary(latestBrand)) } catch (e) { setErr(e.message) }
  }

  return (
    <div>
      {err && <div className="banner error">{err}</div>}

      <section className="card">
        <div className="card-head">
          <h2>Tóm tắt khủng hoảng {latestBrand && <span className="pill brand">{latestBrand}</span>}</h2>
          <button className="btn ghost" onClick={loadSummary}>Tạo tóm tắt</button>
        </div>
        {!summary && <p className="muted small">Bấm "Tạo tóm tắt" để tổng hợp tin tiêu cực gần đây{latestBrand ? ` về ${latestBrand}` : ''} (headlines + từ khoá nổi bật).</p>}
        {summary && (
          <div>
            <div className="muted small" style={{ marginBottom: 8 }}>{summary.negativeCount} mention tiêu cực trong 24h</div>
            {summary.narrative && (
              <div className="banner" style={{ background: 'var(--panel2)', borderLeft: '3px solid var(--accent)', marginBottom: 10 }}>
                🤖 {summary.narrative}
              </div>
            )}
            {summary.keywords?.length > 0 && <div className="tags" style={{ marginBottom: 10 }}>{summary.keywords.map((k) => <span className="tag" key={k}>#{k}</span>)}</div>}
            <ul style={{ margin: 0, paddingLeft: 18, lineHeight: 1.7 }}>
              {summary.headlines?.map((h, i) => <li key={i}>{h}</li>)}
            </ul>
            {!summary.headlines?.length && <p className="muted small">Không có tin tiêu cực nổi bật.</p>}
          </div>
        )}
      </section>
      <section className="card">
        <div className="card-head">
          <h2>Cảnh báo khủng hoảng · {open.length} chưa xử lý / {all.length} tổng</h2>
          <span className={`conn ${live?.connected ? 'on' : ''}`}><span className="dot" />realtime</span>
        </div>
        {!all.length && <p className="empty">Chưa có cảnh báo. Kích hoạt khi một thương hiệu có nhiều mention tiêu cực trong thời gian ngắn.</p>}
        {all.map((a) => (
          <div className={`alert-item ${a.level === 'critical' ? 'critical' : ''}`} key={a.id} style={{ opacity: a.acknowledged ? 0.55 : 1 }}>
            <span style={{ fontSize: 18 }}>{a.acknowledged ? '✅' : a.level === 'critical' ? '🔴' : a.level === 'spike' ? '⚡' : '🟠'}</span>
            <div style={{ flex: 1 }}>
              <div>
                <span className={`pill ${a.level === 'critical' ? 'critical' : a.level === 'spike' ? 'brand' : 'warn'}`}>{a.level}</span>
                {a.brand && <span className="pill brand">{a.brand}</span>}
                {a.acknowledged && <span className="pill ok">đã xử lý</span>}
              </div>
              <div style={{ marginTop: 4 }}>{a.reason}</div>
              <div className="muted small">{timeAgo(a['@timestamp'] || a.createdAt)} · {a.negativeCount} mention tiêu cực</div>
            </div>
            {!a.acknowledged && (
              <button className="btn ghost" disabled={acking === a.id} onClick={() => ack(a.id)}>
                {acking === a.id ? '…' : 'Đánh dấu đã xử lý'}
              </button>
            )}
          </div>
        ))}
      </section>
    </div>
  )
}
