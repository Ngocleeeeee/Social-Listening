import { useEffect, useState, useCallback } from 'react'
import { getStories } from '../api'
import { timeAgo } from '../time'

const cls = (s) => ({ Positive: 'ok', Negative: 'err', Neutral: 'muted' }[s] || 'muted')

export default function Stories({ brand }) {
  const [items, setItems] = useState([])
  const [err, setErr] = useState(null)
  const [open, setOpen] = useState(null)

  const load = useCallback(async () => {
    try { setItems((await getStories(brand)) || []); setErr(null) } catch (e) { setErr(e.message) }
  }, [brand])
  useEffect(() => { load(); const t = setInterval(load, 8000); return () => clearInterval(t) }, [load])

  if (err) return <div className="banner error">{err}</div>

  return (
    <div>
      <p className="muted small">Gom các bài cùng một sự kiện từ nhiều nguồn (khử trùng độ phủ) — sắp theo số nguồn đưa tin.</p>
      {!items.length && <p className="empty">Chưa có cụm tin. Đợi dữ liệu về hoặc bỏ lọc thương hiệu.</p>}
      <div className="feed">
        {items.map((s) => (
          <div className={`mention ${cls(s.sentiment)}`} key={s.fingerprint}>
            <div className="mention-head">
              <span className={`pill ${cls(s.sentiment)}`}>{s.sentiment}</span>
              <span className="pill brand">{s.sourceCount} nguồn</span>
              <span className="muted small">{timeAgo(s.latest)}</span>
            </div>
            <div className="mention-title">
              {s.url ? <a href={s.url} target="_blank" rel="noopener noreferrer">{s.title}</a> : s.title}
            </div>
            <button className="src-link" onClick={() => setOpen(open === s.fingerprint ? null : s.fingerprint)}>
              {open === s.fingerprint ? 'Ẩn nguồn ▲' : `Xem ${s.sourceCount} nguồn ▼`}
            </button>
            {open === s.fingerprint && (
              <div className="tags" style={{ marginTop: 6 }}>
                {s.sources.map((src) => <span className="tag" key={src}>{src}</span>)}
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  )
}
