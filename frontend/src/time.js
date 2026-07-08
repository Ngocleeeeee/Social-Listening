export function timeAgo(input) {
  const d = new Date(input)
  if (isNaN(d)) return ''
  const s = Math.floor((Date.now() - d.getTime()) / 1000)
  if (s < 60) return 'vừa xong'
  const m = Math.floor(s / 60); if (m < 60) return `${m} phút trước`
  const h = Math.floor(m / 60); if (h < 24) return `${h} giờ trước`
  const dd = Math.floor(h / 24); if (dd < 30) return `${dd} ngày trước`
  return d.toLocaleDateString()
}
