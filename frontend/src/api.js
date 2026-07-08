async function toJson(res) {
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
  const t = await res.text()
  return t ? JSON.parse(t) : null
}
const qs = (o) => {
  const p = new URLSearchParams()
  Object.entries(o).forEach(([k, v]) => { if (v !== undefined && v !== null && v !== '') p.append(k, v) })
  const s = p.toString()
  return s ? `?${s}` : ''
}
const authHeaders = () => { const t = localStorage.getItem('token'); return t ? { Authorization: `Bearer ${t}` } : {} }
export const login = (username, password) => fetch('/api/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ username, password }) }).then(toJson)

export const getOverview = (brand) => fetch(`/api/stats/overview${qs({ brand })}`).then(toJson)
export const getTop = (field, brand) => fetch(`/api/stats/top${qs({ field, brand })}`).then(toJson)
export const getTimeseries = (brand) => fetch(`/api/stats/timeseries${qs({ brand })}`).then(toJson)
export const getMentions = (f) => fetch(`/api/mentions${qs(f)}`).then(toJson)

export const getReportBrands = () => fetch('/api/report/brands').then(toJson)
export const getReportDaily = (days = 14) => fetch(`/api/report/daily?days=${days}`).then(toJson)

export const listBrands = () => fetch('/api/brands').then(toJson)
export const createBrand = (name, keywords) =>
  fetch('/api/brands', { method: 'POST', headers: { 'Content-Type': 'application/json', ...authHeaders() }, body: JSON.stringify({ name, keywords }) }).then(toJson)
export const deleteBrand = (id) => fetch(`/api/brands/${id}`, { method: 'DELETE', headers: authHeaders() })

export const addKeyword = (id, keyword) =>
  fetch(`/api/brands/${id}/keywords`, { method: 'POST', headers: { 'Content-Type': 'application/json', ...authHeaders() }, body: JSON.stringify({ keyword }) })
export const removeKeyword = (id, keyword) =>
  fetch(`/api/brands/${id}/keywords?keyword=${encodeURIComponent(keyword)}`, { method: 'DELETE', headers: authHeaders() })

export const getAlerts = () => fetch('/api/alerts').then(toJson)

export const getTrend = (brand) => fetch(`/api/stats/trend${qs({ brand })}`).then(toJson)
export const ackAlert = (id) => fetch(`/api/alerts/${id}/ack`, { method: 'POST', headers: authHeaders() })

export const getMentionsCount = (f) => fetch(`/api/mentions/count${qs(f)}`).then(toJson)
export const exportMentionsUrl = (f) => `/api/mentions/export${qs(f)}`
export const getSummary = (brand) => fetch(`/api/alerts/summary${qs({ brand })}`).then(toJson)
export const getTrending = () => fetch('/api/report/trending').then(toJson)

export const getStories = (brand) => fetch(`/api/stories${qs({ brand })}`).then(toJson)

export const getDashboard = (brand) => fetch(`/api/stats/dashboard${qs({ brand })}`).then(toJson)

export const getBrandHealth = () => fetch('/api/health').then(toJson)

export const listRules = () => fetch('/api/rules').then(toJson)
export const createRule = (r) =>
  fetch('/api/rules', { method: 'POST', headers: { 'Content-Type': 'application/json', ...authHeaders() }, body: JSON.stringify(r) }).then(toJson)
export const toggleRule = (id, enabled) =>
  fetch(`/api/rules/${id}/toggle?enabled=${enabled}`, { method: 'POST', headers: authHeaders() })
export const deleteRule = (id) => fetch(`/api/rules/${id}`, { method: 'DELETE', headers: authHeaders() })
