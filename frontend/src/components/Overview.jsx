import { useEffect, useState, useCallback, useRef } from 'react'
import { getDashboard } from '../api'
import ChartView, { DARK, baseOptions } from './ChartView.jsx'

function Delta({ cur, prev }) {
  if (prev === 0 && cur === 0) return <span className="delta flat">–</span>
  const pct = prev === 0 ? 100 : Math.round(((cur - prev) / prev) * 100)
  const up = cur >= prev
  return <span className={`delta ${up ? 'up' : 'down'}`}>{up ? '▲' : '▼'} {Math.abs(pct)}% <span className="muted small">so với 24h trước</span></span>
}

export default function Overview({ brand }) {
  const [ov, setOv] = useState(null)
  const [sources, setSources] = useState([])
  const [topics, setTopics] = useState([])
  const [series, setSeries] = useState([])
  const [byBrand, setByBrand] = useState([])
  const [trend, setTrend] = useState(null)
  const [trending, setTrending] = useState([])
  const [auto, setAuto] = useState(true)
  const [updated, setUpdated] = useState(null)
  const [err, setErr] = useState(null)
  const timer = useRef(null)

  const load = useCallback(async () => {
    try {
      const d = await getDashboard(brand)   // 1 request → read-model snapshot
      setOv(d.overview)
      setSources(d.sources || [])
      setTopics(d.topics || [])
      setSeries(d.series || [])
      setByBrand(d.trending || [])          // Share of Voice from trending buckets
      setTrend(d.trend)
      setTrending(d.trending || [])
      setUpdated(new Date()); setErr(null)
    } catch (e) { setErr(e.message) }
  }, [brand])

  useEffect(() => {
    load()
    clearInterval(timer.current)
    if (auto) timer.current = setInterval(load, 5000)
    return () => clearInterval(timer.current)
  }, [load, auto])

  if (err) return <div className="banner error">Lỗi tải dữ liệu: {err}</div>
  if (!ov) return <p className="muted">Đang tải…</p>

  const negPct = ov.total ? Math.round((ov.negative / ov.total) * 100) : 0
  const lastHour = series.length ? series[series.length - 1].total : 0
  const maxTopic = Math.max(1, ...topics.map((t) => t.count))

  const donut = { labels: ['Tích cực', 'Trung tính', 'Tiêu cực'], datasets: [{ data: [ov.positive, ov.neutral, ov.negative], backgroundColor: [DARK.ok, DARK.neutral, DARK.err], borderWidth: 0 }] }
  const line = {
    labels: series.map((p) => p.time.slice(5)),
    datasets: [
      { label: 'Tổng', data: series.map((p) => p.total), borderColor: DARK.accent, backgroundColor: 'rgba(56,189,248,.15)', fill: true, tension: .35, pointRadius: 0 },
      { label: 'Tiêu cực', data: series.map((p) => p.negative), borderColor: DARK.err, tension: .35, pointRadius: 0 }
    ]
  }
  const sov = { labels: byBrand.map((b) => b.brand), datasets: [{ label: 'Mentions (24h)', data: byBrand.map((b) => b.current), backgroundColor: DARK.accent, borderRadius: 4 }] }
  const hbar = baseOptions({ indexAxis: 'y', plugins: { legend: { display: false } } })

  return (
    <div>
      {negPct >= 40 && ov.total >= 5 && (
        <div className="banner crisis">⚠ Tỉ lệ tiêu cực cao: {negPct}%{brand ? ` cho ${brand}` : ''} — nguy cơ khủng hoảng.</div>
      )}

      <div className="kpis">
        <div className="kpi"><div className="kpi-num">{ov.total}</div><div className="kpi-lbl">Tổng mention</div>{trend && <Delta cur={trend.total} prev={trend.prevTotal} />}</div>
        <div className="kpi"><div className="kpi-num ok">{ov.positive}</div><div className="kpi-lbl">Tích cực</div></div>
        <div className="kpi"><div className="kpi-num err">{ov.negative}</div><div className="kpi-lbl">Tiêu cực ({negPct}%)</div>{trend && <Delta cur={trend.negative} prev={trend.prevNegative} />}</div>
        <div className="kpi"><div className="kpi-num accent">{lastHour}</div><div className="kpi-lbl">Mention/giờ gần nhất</div></div>
      </div>

      <div className="toolbar" style={{ marginBottom: 12, justifyContent: 'flex-end' }}>
        <label className="muted small" style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
          <input type="checkbox" checked={auto} onChange={(e) => setAuto(e.target.checked)} style={{ width: 'auto' }} /> Tự làm mới
        </label>
        <button className="btn ghost" onClick={load}>↻ Làm mới</button>
        {updated && <span className="muted small">Cập nhật {updated.toLocaleTimeString()}</span>}
      </div>

      <div className="grid3">
        <section className="card">
          <h2>Khối lượng theo giờ</h2>
          {series.length ? <ChartView type="line" data={line} options={baseOptions()} height={260} /> : <p className="empty">Chưa có dữ liệu</p>}
        </section>
        <section className="card">
          <h2>Tỉ lệ sắc thái</h2>
          {ov.total ? <ChartView type="doughnut" data={donut} options={{ responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom', labels: { color: DARK.text, boxWidth: 12, font: { size: 11 } } } } }} height={260} /> : <p className="empty">Chưa có dữ liệu</p>}
        </section>
      </div>

      <div className="grid2">
        <section className="card">
          <h2>Share of Voice (theo thương hiệu)</h2>
          {byBrand.length ? <ChartView type="bar" data={sov} options={hbar} height={Math.max(180, byBrand.length * 34)} /> : <p className="empty">Chưa có brand nào được nhắc</p>}
        </section>
        <section className="card">
          <h2>Đám mây chủ đề</h2>
          {topics.length ? (
            <div className="cloud">
              {topics.map((t) => (
                <span className="cloud-tag" key={t.key} style={{ fontSize: `${12 + (t.count / maxTopic) * 20}px`, opacity: 0.6 + (t.count / maxTopic) * 0.4 }}>{t.key}</span>
              ))}
            </div>
          ) : <p className="empty">Chưa có dữ liệu</p>}
        </section>
      </div>

      <section className="card">
        <h2>Thương hiệu đang nóng lên (24h vs 24h trước)</h2>
        {trending.length ? (
          <div className="brand-grid">
            {trending.map((b) => {
              const d = b.current - b.previous
              return (
                <div className="brand-item" key={b.brand}>
                  <div><div className="brand-name">{b.brand}</div><div className="muted small">{b.current} mention / 24h</div></div>
                  <span className={`delta ${d >= 0 ? 'up' : 'down'}`} style={{ marginTop: 0 }}>{d >= 0 ? '▲' : '▼'} {Math.abs(d)}</span>
                </div>
              )
            })}
          </div>
        ) : <p className="empty">Chưa đủ dữ liệu để so sánh xu hướng.</p>}
      </section>

      <p className="muted small">Nguồn: Elasticsearch + SQL qua Dashboard.Api (cache Redis).</p>
    </div>
  )
}
