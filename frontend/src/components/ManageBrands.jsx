import { useEffect, useState, useCallback } from 'react'
import { listBrands, createBrand, deleteBrand, addKeyword, removeKeyword } from '../api'

function KeywordEditor({ brand, onChanged }) {
  const [kw, setKw] = useState('')
  const add = async (e) => {
    e.preventDefault()
    const v = kw.trim(); if (!v) return
    await addKeyword(brand.id, v); setKw(''); onChanged()
  }
  return (
    <div className="kw-edit">
      <div className="tags">
        {brand.keywords.map((k) => (
          <span className="tag" key={k}>
            {k}<button className="kw-x" title="Xoá từ khoá" onClick={() => removeKeyword(brand.id, k).then(onChanged)}>×</button>
          </span>
        ))}
      </div>
      <form className="kw-form" onSubmit={add}>
        <input value={kw} onChange={(e) => setKw(e.target.value)} placeholder="+ từ khoá" />
        <button>Thêm</button>
      </form>
    </div>
  )
}

export default function ManageBrands() {
  const [brands, setBrands] = useState([])
  const [name, setName] = useState('')
  const [keywords, setKeywords] = useState('')
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState(null)
  const [msg, setMsg] = useState(null)

  const load = useCallback(async () => {
    try { setBrands((await listBrands()) || []); setErr(null) } catch (e) { setErr(e.message) }
  }, [])
  useEffect(() => { load() }, [load])

  const add = async (e) => {
    e.preventDefault()
    if (!name.trim()) return
    setBusy(true); setErr(null); setMsg(null)
    try {
      const kws = keywords.split(',').map((k) => k.trim()).filter(Boolean)
      await createBrand(name.trim(), kws)
      setMsg(`Đã thêm "${name}". Collector sẽ tự thu thập theo brand này trong ~1 phút.`)
      setName(''); setKeywords(''); await load()
    } catch (e) { setErr(e.message) } finally { setBusy(false) }
  }

  const remove = async (id, bn) => {
    if (!confirm(`Xoá thương hiệu "${bn}"?`)) return
    try { await deleteBrand(id); await load() } catch (e) { setErr(e.message) }
  }

  return (
    <div>
      <section className="card">
        <h2>Thêm thương hiệu theo dõi</h2>
        <form className="form" onSubmit={add}>
          <label>Tên thương hiệu
            <input value={name} onChange={(e) => setName(e.target.value)} placeholder="VD: Bamboo Airways" />
          </label>
          <label>Từ khoá ban đầu (cách nhau dấu phẩy — để trống sẽ dùng tên)
            <input value={keywords} onChange={(e) => setKeywords(e.target.value)} placeholder="Bamboo Airways, Bamboo" />
          </label>
          <button className="btn" disabled={busy}>{busy ? 'Đang thêm…' : 'Thêm thương hiệu'}</button>
          {err && <div className="banner error">{err}</div>}
          {msg && <div className="banner ok">{msg}</div>}
        </form>
        <p className="muted small">Muốn theo dõi thêm cách gọi khác của cùng thương hiệu? Thêm/xoá từng từ khoá ngay trong danh sách dưới đây.</p>
      </section>

      <section className="card">
        <h2>Đang theo dõi ({brands.length})</h2>
        {!brands.length && <p className="muted">Chưa có thương hiệu nào.</p>}
        <div className="brand-grid">
          {brands.map((b) => (
            <div className="brand-item" key={b.id}>
              <div style={{ flex: 1 }}>
                <div className="brand-name">{b.name}</div>
                <KeywordEditor brand={b} onChanged={load} />
              </div>
              <button className="del" onClick={() => remove(b.id, b.name)} title="Xoá brand">✕</button>
            </div>
          ))}
        </div>
      </section>
    </div>
  )
}
