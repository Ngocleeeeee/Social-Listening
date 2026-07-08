import { useState, useEffect, useRef } from 'react'
import Overview from './components/Overview.jsx'
import Mentions from './components/Mentions.jsx'
import LiveFeed from './components/LiveFeed.jsx'
import Reports from './components/Reports.jsx'
import Alerts from './components/Alerts.jsx'
import Compare from './components/Compare.jsx'
import BrandHealth from './components/BrandHealth.jsx'
import AlertRules from './components/AlertRules.jsx'
import Stories from './components/Stories.jsx'
import ManageBrands from './components/ManageBrands.jsx'
import { useLive } from './useLive'
import { login as apiLogin } from './api'

function beep() {
  try {
    const ctx = new (window.AudioContext || window.webkitAudioContext)()
    const o = ctx.createOscillator(); const g = ctx.createGain()
    o.connect(g); g.connect(ctx.destination); o.type = 'sine'; o.frequency.value = 880
    g.gain.setValueAtTime(0.001, ctx.currentTime)
    g.gain.exponentialRampToValueAtTime(0.2, ctx.currentTime + 0.02)
    g.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.5)
    o.start(); o.stop(ctx.currentTime + 0.5)
  } catch { /* ignore */ }
}

const BRANDS = ['', 'Vietnam Airlines', 'Viettel', 'VinFast', 'FPT', 'Vietjet', 'Techcombank', 'MoMo', 'Shopee']

const NAV = [
  { key: 'overview', ic: '▦', label: 'Tổng quan' },
  { key: 'live', ic: '⚡', label: 'Trực tiếp' },
  { key: 'mentions', ic: '💬', label: 'Mentions' },
  { key: 'stories', ic: '🗞', label: 'Tin nổi bật' },
  { key: 'reports', ic: '📊', label: 'Báo cáo' },
  { key: 'health', ic: '❤', label: 'Sức khỏe TH' },
  { key: 'alerts', ic: '🚨', label: 'Cảnh báo' },
  { key: 'rules', ic: '📐', label: 'Luật cảnh báo' },
  { key: 'compare', ic: '⚖', label: 'So sánh' },
  { key: 'manage', ic: '⚙', label: 'Quản lý' }
]

export default function App() {
  const [tab, setTab] = useState('overview')
  const [brand, setBrand] = useState('')
  const [toasts, setToasts] = useState([])
  const [theme, setTheme] = useState(() => localStorage.getItem('theme') || 'dark')
  const [user, setUser] = useState(() => localStorage.getItem('user') || '')
  const [showLogin, setShowLogin] = useState(false)
  const [creds, setCreds] = useState({ u: '', p: '' })
  const [loginErr, setLoginErr] = useState(null)
  const live = useLive()
  const seen = useRef(new Set())

  const doLogin = async (e) => {
    e.preventDefault()
    setLoginErr(null)
    try {
      const r = await apiLogin(creds.u, creds.p)
      localStorage.setItem('token', r.token); localStorage.setItem('user', r.user)
      setUser(r.user); setShowLogin(false); setCreds({ u: '', p: '' })
    } catch { setLoginErr('Sai tài khoản hoặc mật khẩu') }
  }
  const logout = () => { localStorage.removeItem('token'); localStorage.removeItem('user'); setUser('') }

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme)
    localStorage.setItem('theme', theme)
  }, [theme])

  // New crisis alert → toast + browser notification
  useEffect(() => {
    const a = live.alerts[0]
    if (!a || seen.current.has(a.id)) return
    seen.current.add(a.id)
    if (seen.current.size === 1 && !live.alerts.length) return
    setToasts((t) => [{ id: a.id, text: `[${a.level}] ${a.reason}` }, ...t].slice(0, 4))
    setTimeout(() => setToasts((t) => t.filter((x) => x.id !== a.id)), 8000)
    beep()
    if ('Notification' in window && Notification.permission === 'granted')
      new Notification('BrandRadar — Cảnh báo khủng hoảng', { body: a.reason })
  }, [live.alerts])

  useEffect(() => {
    if ('Notification' in window && Notification.permission === 'default') Notification.requestPermission()
  }, [])

  const showBrandBar = tab === 'overview' || tab === 'mentions' || tab === 'reports' || tab === 'stories'
  const title = NAV.find((n) => n.key === tab)?.label

  return (
    <div className="layout">
      <aside className="sidebar">
        <div className="brandmark"><span className="dot" />Brand<span>Radar</span></div>
        {NAV.map((n) => (
          <button key={n.key} className={`navbtn ${tab === n.key ? 'active' : ''}`} onClick={() => setTab(n.key)}>
            <span className="ic">{n.ic}</span>{n.label}
            {n.key === 'alerts' && live.alerts.length > 0 && <span className="pill err" style={{ marginLeft: 'auto' }}>{live.alerts.length}</span>}
          </button>
        ))}
        <div className="nav-spacer" />
        <div className="side-foot">Social listening &amp; crisis monitoring<br />RSS · RabbitMQ · NLP · Kafka · ES</div>
      </aside>

      <main className="main">
        <div className="topbar">
          <h1 className="page-title">{title}</h1>
          <div className="toolbar">
            <span className={`conn ${live.connected ? 'on' : ''}`}><span className="dot" />{live.connected ? 'Realtime' : 'Đang kết nối…'}</span>
            {showBrandBar && (
              <select value={brand} onChange={(e) => setBrand(e.target.value)}>
                {BRANDS.map((b) => <option key={b} value={b}>{b || 'Tất cả thương hiệu'}</option>)}
              </select>
            )}
            <button className="btn ghost" title="Đổi giao diện sáng/tối" onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')}>
              {theme === 'dark' ? '☀' : '🌙'}
            </button>
            {user
              ? <span className="conn">👤 {user} <button className="btn ghost" onClick={logout}>Đăng xuất</button></span>
              : <button className="btn ghost" onClick={() => setShowLogin(true)}>Đăng nhập</button>}
          </div>
        </div>

        {tab === 'overview' && <Overview brand={brand} />}
        {tab === 'live' && <LiveFeed live={live} />}
        {tab === 'mentions' && <Mentions brand={brand} />}
        {tab === 'stories' && <Stories brand={brand} />}
        {tab === 'reports' && <Reports />}
        {tab === 'health' && <BrandHealth />}
        {tab === 'alerts' && <Alerts live={live} />}
        {tab === 'rules' && <AlertRules />}
        {tab === 'compare' && <Compare />}
        {tab === 'manage' && <ManageBrands />}
      </main>

      <div className="toasts">
        {toasts.map((t) => <div className="toast" key={t.id}>🚨 {t.text}</div>)}
      </div>

      {showLogin && (
        <div className="modal-bg" onClick={() => setShowLogin(false)}>
          <form className="modal" style={{ maxWidth: 360 }} onClick={(e) => e.stopPropagation()} onSubmit={doLogin}>
            <button type="button" className="modal-close" onClick={() => setShowLogin(false)}>×</button>
            <h2>Đăng nhập</h2>
            <div className="form">
              <label>Tài khoản<input value={creds.u} onChange={(e) => setCreds({ ...creds, u: e.target.value })} placeholder="admin" /></label>
              <label>Mật khẩu<input type="password" value={creds.p} onChange={(e) => setCreds({ ...creds, p: e.target.value })} placeholder="admin123" /></label>
              <button className="btn" type="submit">Đăng nhập</button>
              {loginErr && <div className="banner error">{loginErr}</div>}
              <p className="muted small">Demo: admin / admin123. Cần đăng nhập để quản lý thương hiệu &amp; xử lý cảnh báo.</p>
            </div>
          </form>
        </div>
      )}
    </div>
  )
}
