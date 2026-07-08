import { useEffect, useState, useCallback } from 'react'
import { getBrandHealth } from '../api'

const gradeColor = (g) => ({ A: '#16a34a', B: '#0ea5e9', C: '#f59e0b', D: '#ef4444' }[g] || '#64748b')
const netColor = (n) => (n > 0.15 ? 'ok' : n < -0.15 ? 'err' : 'muted')

export default function BrandHealth() {
  const [rows, setRows] = useState([])
  const [err, setErr] = useState(null)
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    setBusy(true); setErr(null)
    try { setRows((await getBrandHealth()) || []) } catch (e) { setErr(e.message) } finally { setBusy(false) }
  }, [])
  useEffect(() => { load(); const t = setInterval(load, 15000); return () => clearInterval(t) }, [load])

  const maxSov = Math.max(1, ...rows.map((r) => r.shareOfVoice))
  const leader = rows[0]

  return (
    <div>
      {err && <div className="banner error">{err}</div>}

      <div className="card">
        <div className="card-head">
          <h2>Sức khỏe thương hiệu &amp; thị phần thảo luận (24h)</h2>
          <button className="btn ghost" onClick={load} disabled={busy}>{busy ? '…' : '↻ Làm mới'}</button>
        </div>
        <p className="muted small">
          Brand Health Index (0–100) tổng hợp cảm xúc thuần, áp lực tiêu cực và đà thảo luận. Share of Voice =
          thị phần lượng nhắc so với các thương hiệu đang theo dõi. Insight sinh tự động.
        </p>
        {leader && (
          <div className="banner" style={{ background: 'var(--panel2)', borderLeft: `3px solid ${gradeColor(leader.grade)}`, marginTop: 10 }}>
            🏆 Dẫn đầu: <b>{leader.brand}</b> — BHI {leader.score} ({leader.grade}), thị phần {leader.shareOfVoice}%
          </div>
        )}
      </div>

      {!rows.length && !busy && <p className="empty">Chưa đủ dữ liệu 24h. Chờ collector thu thập thêm.</p>}

      <div className="feed">
        {rows.map((r, i) => (
          <div className="card" key={r.brand} style={{ marginBottom: 10 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
              <div style={{ fontSize: 18, width: 24, textAlign: 'center', color: 'var(--muted)' }}>{i + 1}</div>

              <div style={{
                minWidth: 56, height: 56, borderRadius: 12, display: 'grid', placeItems: 'center',
                background: gradeColor(r.grade), color: '#fff', fontWeight: 700, lineHeight: 1
              }}>
                <div style={{ fontSize: 20 }}>{r.score}</div>
                <div style={{ fontSize: 11, opacity: 0.9 }}>{r.grade}</div>
              </div>

              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                  <b>{r.brand}</b>
                  <span className={`pill ${netColor(r.netSentiment)}`}>net {r.netSentiment > 0 ? '+' : ''}{r.netSentiment}</span>
                  <span className="pill muted">{r.mentions} nhắc</span>
                  <span className={`pill ${r.volumeChange >= 0 ? 'ok' : 'err'}`}>
                    {r.volumeChange >= 0 ? '↑' : '↓'} {Math.abs(r.volumeChange)}%
                  </span>
                </div>

                <div style={{ marginTop: 8 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12 }} className="muted small">
                    <span>Share of Voice</span><span>{r.shareOfVoice}%</span>
                  </div>
                  <div style={{ height: 8, background: 'var(--panel2)', borderRadius: 6, overflow: 'hidden', marginTop: 3 }}>
                    <div style={{ width: `${(r.shareOfVoice / maxSov) * 100}%`, height: '100%', background: gradeColor(r.grade) }} />
                  </div>
                </div>

                <div style={{ marginTop: 8, display: 'flex', gap: 6, fontSize: 12 }}>
                  <span className="pill ok">▲ {r.positive}</span>
                  <span className="pill muted">● {r.neutral}</span>
                  <span className="pill err">▼ {r.negative}</span>
                </div>

                <div className="muted small" style={{ marginTop: 8 }}>{r.insight}</div>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
