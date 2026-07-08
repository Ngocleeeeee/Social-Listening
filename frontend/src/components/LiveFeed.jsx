const cls = (s) => ({ Positive: 'ok', Negative: 'err', Neutral: 'muted' }[s] || 'muted')

export default function LiveFeed({ live }) {
  const { mentions, alerts, connected } = live
  return (
    <div>
      <div className="grid3">
        <section className="card">
          <div className="card-head">
            <h2>Mention mới về</h2>
            <span className={`conn ${connected ? 'on' : ''}`}><span className="dot" />{connected ? 'realtime' : 'kết nối…'}</span>
          </div>
          {!mentions.length && <p className="empty">Đang chờ mention mới… (dữ liệu thật về theo nhịp báo đăng)</p>}
          <div className="feed">
            {mentions.map((m, i) => (
              <div className={`mention ${cls(m.sentiment)}`} key={m.id + '_' + i}>
                <div className="mention-head">
                  <span className={`pill ${cls(m.sentiment)}`}>{m.sentiment}</span>
                  {m.brand && <span className="pill brand">{m.brand}</span>}
                  <span className="muted small">{m.source}</span>
                </div>
                <div className="mention-title">
                  {m.url ? <a href={m.url} target="_blank" rel="noopener noreferrer">{m.title}</a> : m.title}
                </div>
              </div>
            ))}
          </div>
        </section>

        <section className="card">
          <h2>Cảnh báo realtime</h2>
          {!alerts.length && <p className="empty">Chưa có cảnh báo.</p>}
          {alerts.map((a) => (
            <div className={`alert-item ${a.level === 'critical' ? 'critical' : ''}`} key={a.id}>
              <span style={{ fontSize: 16 }}>{a.level === 'critical' ? '🔴' : a.level === 'spike' ? '⚡' : '🟠'}</span>
              <div>
                <div>{a.reason}</div>
                <div className="muted small">{new Date(a['@timestamp'] || a.createdAt).toLocaleTimeString()}</div>
              </div>
            </div>
          ))}
        </section>
      </div>
    </div>
  )
}
