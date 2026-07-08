import { useEffect, useState, useCallback } from 'react'
import { listRules, createRule, toggleRule, deleteRule, listBrands } from '../api'

const TYPES = {
  negative: 'Số mention tiêu cực ≥ ngưỡng',
  volume: 'Tổng lượng nhắc ≥ ngưỡng',
  negshare: '% tiêu cực ≥ ngưỡng (%)',
}
const CHANNELS = { inapp: 'Trên dashboard', slack: 'Slack', webhook: 'Webhook' }
const empty = { name: '', brandId: '', type: 'negative', threshold: 10, windowMinutes: 60, cooldownMinutes: 30, channel: 'inapp', target: '', enabled: true }

export default function AlertRules() {
  const [rules, setRules] = useState([])
  const [brands, setBrands] = useState([])
  const [form, setForm] = useState(empty)
  const [err, setErr] = useState(null)
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try { setRules((await listRules()) || []); setErr(null) } catch (e) { setErr(e.message) }
  }, [])
  useEffect(() => { load(); listBrands().then((b) => setBrands(b || [])).catch(() => {}) }, [load])

  const brandName = (id) => brands.find((b) => b.id === id)?.name || (id ? `#${id}` : 'Mọi thương hiệu')

  const submit = async (e) => {
    e.preventDefault(); setBusy(true); setErr(null)
    try {
      await createRule({
        name: form.name.trim(),
        brandId: form.brandId === '' ? null : Number(form.brandId),
        type: form.type, threshold: Number(form.threshold),
        windowMinutes: Number(form.windowMinutes), cooldownMinutes: Number(form.cooldownMinutes),
        channel: form.channel, target: form.target.trim() || null, enabled: true,
      })
      setForm(empty); await load()
    } catch (e) { setErr(e.message.includes('401') ? 'Cần đăng nhập để tạo luật (admin/admin123)' : e.message) }
    finally { setBusy(false) }
  }

  const onToggle = async (r) => { try { await toggleRule(r.id, !r.enabled); await load() } catch (e) { setErr(e.message) } }
  const onDelete = async (r) => { if (!confirm(`Xoá luật "${r.name}"?`)) return; try { await deleteRule(r.id); await load() } catch (e) { setErr(e.message) } }

  return (
    <div>
      {err && <div className="banner error">{err}</div>}

      <div className="card">
        <h2>Tạo luật cảnh báo</h2>
        <p className="muted small">Khách hàng tự định nghĩa điều kiện + kênh gửi. Analysis.Worker đánh giá realtime theo từng mention (làm mới luật mỗi 30s).</p>
        <form className="form" onSubmit={submit}>
          <div className="row">
            <label style={{ flex: 2 }}>Tên luật
              <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="VD: VinFast khủng hoảng PR" required />
            </label>
            <label>Thương hiệu
              <select value={form.brandId} onChange={(e) => setForm({ ...form, brandId: e.target.value })}>
                <option value="">Mọi thương hiệu</option>
                {brands.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
              </select>
            </label>
          </div>
          <div className="row">
            <label style={{ flex: 2 }}>Điều kiện
              <select value={form.type} onChange={(e) => setForm({ ...form, type: e.target.value })}>
                {Object.entries(TYPES).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
              </select>
            </label>
            <label>Ngưỡng
              <input type="number" min="1" value={form.threshold} onChange={(e) => setForm({ ...form, threshold: e.target.value })} />
            </label>
            <label>Cửa sổ (phút)
              <input type="number" min="5" value={form.windowMinutes} onChange={(e) => setForm({ ...form, windowMinutes: e.target.value })} />
            </label>
            <label>Nghỉ lại (phút)
              <input type="number" min="5" value={form.cooldownMinutes} onChange={(e) => setForm({ ...form, cooldownMinutes: e.target.value })} />
            </label>
          </div>
          <div className="row">
            <label>Kênh gửi
              <select value={form.channel} onChange={(e) => setForm({ ...form, channel: e.target.value })}>
                {Object.entries(CHANNELS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
              </select>
            </label>
            {form.channel !== 'inapp' && (
              <label style={{ flex: 2 }}>Webhook URL
                <input value={form.target} onChange={(e) => setForm({ ...form, target: e.target.value })} placeholder="https://hooks.slack.com/…" />
              </label>
            )}
            <button className="btn" disabled={busy}>{busy ? 'Đang lưu…' : '+ Tạo luật'}</button>
          </div>
        </form>
      </div>

      <div className="card">
        <h2>Luật hiện có · {rules.length}</h2>
        {!rules.length && <p className="empty">Chưa có luật nào.</p>}
        {rules.map((r) => (
          <div className="alert-item" key={r.id} style={{ opacity: r.enabled ? 1 : 0.5 }}>
            <span style={{ fontSize: 18 }}>{r.enabled ? '🟢' : '⚪'}</span>
            <div style={{ flex: 1 }}>
              <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', alignItems: 'center' }}>
                <b>{r.name}</b>
                <span className="pill brand">{brandName(r.brandId)}</span>
                <span className="pill muted">{CHANNELS[r.channel] || r.channel}</span>
              </div>
              <div className="muted small" style={{ marginTop: 4 }}>
                {TYPES[r.type] || r.type} = {r.threshold}{r.type === 'negshare' ? '%' : ''} · cửa sổ {r.windowMinutes}′ · nghỉ {r.cooldownMinutes}′
                {r.lastFiredAt && ` · lần cuối ${new Date(r.lastFiredAt).toLocaleString()}`}
              </div>
            </div>
            <button className="btn ghost" onClick={() => onToggle(r)}>{r.enabled ? 'Tắt' : 'Bật'}</button>
            <button className="btn ghost" onClick={() => onDelete(r)}>Xoá</button>
          </div>
        ))}
      </div>
    </div>
  )
}
