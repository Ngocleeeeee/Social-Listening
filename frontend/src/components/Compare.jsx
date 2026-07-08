import { useEffect, useState } from 'react'
import { getOverview } from '../api'
import ChartView, { DARK } from './ChartView.jsx'

const BRANDS = ['Vietnam Airlines', 'Viettel', 'VinFast', 'FPT', 'Vietjet', 'Techcombank', 'MoMo', 'Shopee']
const donutOpts = { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom', labels: { color: DARK.text, boxWidth: 12, font: { size: 11 } } } } }

function Panel({ brand, setBrand }) {
  const [ov, setOv] = useState(null)
  useEffect(() => { let on = true; getOverview(brand).then((o) => on && setOv(o)).catch(() => {}); return () => { on = false } }, [brand])
  const negPct = ov && ov.total ? Math.round((ov.negative / ov.total) * 100) : 0
  const data = ov ? { labels: ['Tích cực', 'Trung tính', 'Tiêu cực'], datasets: [{ data: [ov.positive, ov.neutral, ov.negative], backgroundColor: [DARK.ok, DARK.neutral, DARK.err], borderWidth: 0 }] } : null
  return (
    <section className="card">
      <select value={brand} onChange={(e) => setBrand(e.target.value)} style={{ marginBottom: 12 }}>
        {BRANDS.map((b) => <option key={b} value={b}>{b}</option>)}
      </select>
      {!ov ? <p className="muted">Đang tải…</p> : (
        <>
          <div className="kpis" style={{ gridTemplateColumns: 'repeat(3,1fr)' }}>
            <div className="kpi"><div className="kpi-num">{ov.total}</div><div className="kpi-lbl">Tổng</div></div>
            <div className="kpi"><div className="kpi-num err">{ov.negative}</div><div className="kpi-lbl">Tiêu cực</div></div>
            <div className="kpi"><div className="kpi-num accent">{negPct}%</div><div className="kpi-lbl">Tỉ lệ xấu</div></div>
          </div>
          {ov.total ? <ChartView type="doughnut" data={data} options={donutOpts} height={220} /> : <p className="empty">Chưa có dữ liệu</p>}
        </>
      )}
    </section>
  )
}

export default function Compare() {
  const [a, setA] = useState('VinFast')
  const [b, setB] = useState('Viettel')
  return (
    <div>
      <p className="muted small">So sánh sắc thái giữa hai thương hiệu.</p>
      <div className="grid2">
        <Panel brand={a} setBrand={setA} />
        <Panel brand={b} setBrand={setB} />
      </div>
    </div>
  )
}
