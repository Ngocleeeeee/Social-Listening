import { useEffect, useRef } from 'react'
import { Chart, registerables } from 'chart.js'
Chart.register(...registerables)

export default function ChartView({ type, data, options, height = 240 }) {
  const canvasRef = useRef(null)
  const chartRef = useRef(null)

  useEffect(() => {
    if (!canvasRef.current) return
    chartRef.current = new Chart(canvasRef.current, { type, data, options })
    return () => chartRef.current?.destroy()
  }, [type, JSON.stringify(data), JSON.stringify(options)])

  return <div style={{ height, position: 'relative' }}><canvas ref={canvasRef} /></div>
}

// Shared Chart.js defaults tuned for the dark theme.
export const DARK = {
  text: '#cbd5e1', grid: 'rgba(148,163,184,0.12)',
  ok: '#22c55e', neutral: '#64748b', err: '#ef4444', accent: '#38bdf8', purple: '#a78bfa', amber: '#f59e0b'
}
export const baseOptions = (extra = {}) => ({
  responsive: true, maintainAspectRatio: false,
  plugins: { legend: { labels: { color: DARK.text, boxWidth: 12, font: { size: 11 } } } },
  scales: {
    x: { ticks: { color: DARK.text, font: { size: 10 } }, grid: { color: DARK.grid } },
    y: { ticks: { color: DARK.text, font: { size: 10 } }, grid: { color: DARK.grid }, beginAtZero: true }
  },
  ...extra
})
