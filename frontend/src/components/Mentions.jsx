import { useEffect, useState, useCallback } from 'react'
import { getMentions, getMentionsCount, exportMentionsUrl } from '../api'

const cls = (s) => ({ Positive: 'ok', Negative: 'err', Neutral: 'muted' }[s] || 'muted')

function highlight(text, term) {
  if (!term || !text) return text
  const idx = text.toLowerCase().indexOf(term.toLowerCase())
  if (idx < 0) return text
  return <>{text.slice(0, idx)}<mark>{text.slice(idx, idx + term.length)}</mark>{text.slice(idx + term.length)}</>
}

export default function Mentions({ brand }) {
  const [sentiment, setSentiment] = useState('')
  const [lang, setLang] = useState('')
  const [keyword, setKeyword] = useState('')
  const [sort, setSort] = useState('analyzed')
  const [page, setPage] = useState(1)
  const [items, setItems] = useState([])
  const [total, setTotal] = useState(0)
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState(null)
  const [detail, setDetail] = useState(null)
  const size = 20

  const load = useCallback(async () => {
    setBusy(true); setErr(null)
    try {
      const f = { brand, sentiment, lang, keyword, sort, page, size }
      const [list, cnt] = await Promise.all([getMentions(f), getMentionsCount({ brand, sentiment, lang, keyword })])
      setItems(list || []); setTotal(cnt?.total ?? 0)
    } catch (e) { setErr(e.message) } finally { setBusy(false) }
  }, [brand, sentiment, lang, keyword, sort, page])

  useEffect(() => { setPage(1) }, [brand, sentiment, lang, keyword, sort])
  useEffect(() => { load(); const id = setInterval(load, 8000); return () => clearInterval(id) }, [load])
  useEffect(() => {
    const onKey = (e) => { if (e.key === 'Escape') setDetail(null) }
    window.addEventListener('keydown', onKey); return () => window.removeEventListener('keydown', onKey)
  }, [])

  const exportCsv = () => window.open(exportMentionsUrl({ brand, sentiment, lang, keyword }), '_blank')
  const pages = Math.max(1, Math.ceil(total / size))

  return (
    <div>
      <div className="card">
        <form className="form row" onSubmit={(e) => { e.preventDefault(); setPage(1); load() }}>
          <label>Sắc thái
            <select value={sentiment} onChange={(e) => setSentiment(e.target.value)}>
              <option value="">Tất cả</option><option>Positive</option><option>Neutral</option><option>Negative</option>
            </select>
          </label>
          <label>Ngôn ngữ
            <select value={lang} onChange={(e) => setLang(e.target.value)}>
              <option value="">Tất cả</option><option value="vi">Tiếng Việt</option><option value="en">English</option>
            </select>
          </label>
          <label>Từ khoá
            <input value={keyword} onChange={(e) => setKeyword(e.target.value)} placeholder="tìm trong nội dung" />
          </label>
          <label>Sắp xếp
            <select value={sort} onChange={(e) => setSort(e.target.value)}>
              <option value="analyzed">Mới thu thập</option>
              <option value="published">Thời gian đăng bài</option>
            </select>
          </label>
          <button className="btn" disabled={busy}>{busy ? 'Đang tải…' : 'Tìm'}</button>
          <button type="button" className="btn ghost" onClick={exportCsv} disabled={!total}>⬇ CSV (tất cả)</button>
        </form>
        <div className="muted small" style={{ marginTop: 8 }}>Tổng {total.toLocaleString()} mention khớp bộ lọc</div>
      </div>

      {err && <div className="banner error">{err}</div>}
      {!items.length && !busy && <p className="empty">Chưa có mention.</p>}

      <div className="feed">
        {items.map((m) => (
          <div className={`mention ${cls(m.sentiment)}`} key={m.id} onClick={() => setDetail(m)}>
            <div className="mention-head">
              <span className={`pill ${cls(m.sentiment)}`}>{m.sentiment}</span>
              <span className="muted small">NLP {Number(m.sentimentScore ?? 0).toFixed(2)}</span>
              {m.brand && <span className="pill brand">{m.brand}</span>}
              <span className="muted small">{m.source} · {new Date(m.publishedAt).toLocaleString()}</span>
            </div>
            <div className="mention-title">{highlight(m.title, keyword)}</div>
          </div>
        ))}
      </div>

      <div className="pager">
        <button disabled={page <= 1 || busy} onClick={() => setPage((p) => Math.max(1, p - 1))}>← Trước</button>
        <span className="muted small">Trang {page} / {pages}</span>
        <button disabled={page >= pages || busy} onClick={() => setPage((p) => p + 1)}>Sau →</button>
      </div>

      {detail && (
        <div className="modal-bg" onClick={() => setDetail(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <button className="modal-close" onClick={() => setDetail(null)}>×</button>
            <div className="mention-head">
              <span className={`pill ${cls(detail.sentiment)}`}>{detail.sentiment}</span>
              <span className="muted small">NLP {Number(detail.sentimentScore ?? 0).toFixed(2)}</span>
              {detail.brand && <span className="pill brand">{detail.brand}</span>}
            </div>
            <h2 style={{ marginTop: 10 }}>{detail.title}</h2>
            <div className="muted small">{detail.source} · {new Date(detail.publishedAt).toLocaleString()}</div>
            <p style={{ lineHeight: 1.6, marginTop: 12 }}>{detail.content}</p>
            {detail.topics?.length > 0 && <div className="tags">{detail.topics.map((t) => <span className="tag" key={t}>#{t}</span>)}</div>}
            {detail.url && <p style={{ marginTop: 14 }}><a className="btn" href={detail.url} target="_blank" rel="noopener noreferrer">Đọc bài gốc ↗</a></p>}
          </div>
        </div>
      )}
    </div>
  )
}
