import { useEffect, useState, useCallback } from 'react'
import { getReportBrands, getReportDaily } from '../api'
import ChartView, { DARK, baseOptions } from './ChartView.jsx'

export default function Reports() {
  const [brands, setBrands] = useState([])
  const [daily, setDaily] = useState([])
  const [days, setDays] = useState(14)
  const [err, setErr] = useState(null)

  const load = useCallback(async () => {
    try {
      const [b, d] = await Promise.all([getReportBrands(), getReportDaily(days)])
      setBrands(b || []); setDaily(d || []); setErr(null)
    } catch (e) { setErr(e.message) }
  }, [days])

  useEffect(() => { load(); const t = setInterval(load, 8000); return () => clearInterval(t) }, [load])

  if (err) return <div className="banner error">Lỗi tải báo cáo: {err}</div>

  const brandChart = {
    labels: brands.map((b) => b.brand),
    datasets: [
      { label: 'Tích cực', data: brands.map((b) => b.positive), backgroundColor: DARK.ok, stack: 's' },
      { label: 'Khác', data: brands.map((b) => b.total - b.positive - b.negative), backgroundColor: DARK.neutral, stack: 's' },
      { label: 'Tiêu cực', data: brands.map((b) => b.negative), backgroundColor: DARK.err, stack: 's' }
    ]
  }
  const dailyChart = {
    labels: daily.map((d) => d.day.slice(5)),
    datasets: [
      { label: 'Tổng', data: daily.map((d) => d.total), backgroundColor: DARK.accent, borderRadius: 4 },
      { label: 'Tiêu cực', data: daily.map((d) => d.negative), backgroundColor: DARK.err, borderRadius: 4 }
    ]
  }

  return (
    <div>
      <div className="toolbar" style={{ justifyContent: 'space-between', marginBottom: 10 }}>
        <p className="muted small" style={{ margin: 0 }}>Nguồn: <code>/api/report/*</code> — SQL GROUP BY trên PostgreSQL (Dapper).</p>
        <button className="btn ghost" onClick={() => window.print()}>🖨 In báo cáo</button>
      </div>

      <section className="card">
        <h2>Sắc thái theo thương hiệu</h2>
        {brands.length
          ? <ChartView type="bar" data={brandChart} options={baseOptions({ scales: { x: { stacked: true, ticks: { color: DARK.text }, grid: { color: DARK.grid } }, y: { stacked: true, beginAtZero: true, ticks: { color: DARK.text }, grid: { color: DARK.grid } } } })} height={Math.max(220, brands.length * 20)} />
          : <p className="empty">Chưa có dữ liệu theo brand.</p>}
      </section>

      <section className="card">
        <div className="card-head">
          <h2>Khối lượng theo ngày</h2>
          <select value={days} onChange={(e) => setDays(Number(e.target.value))}>
            <option value={7}>7 ngày</option><option value={14}>14 ngày</option><option value={30}>30 ngày</option>
          </select>
        </div>
        {daily.length ? <ChartView type="bar" data={dailyChart} options={baseOptions()} height={260} /> : <p className="empty">Chưa có dữ liệu.</p>}
      </section>
    </div>
  )
}
