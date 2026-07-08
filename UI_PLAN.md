# BrandRadar — Kế hoạch nâng cấp UI & tính năng

Mục tiêu: biến dashboard thành sản phẩm social-listening chỉn chu để demo phỏng vấn iComm.

## A. Đại tu giao diện (design system)
- Layout **sidebar trái** (logo BrandRadar + điều hướng có icon) + **topbar** (trạng thái realtime, chọn thương hiệu, auto-refresh).
- Theme tối chuyên nghiệp: bảng màu nhất quán, thẻ bo góc, khoảng cách hợp lý, typography rõ ràng.
- Trạng thái: loading skeleton, empty state, **toast** khi thao tác, badge sắc thái.

## B. Biểu đồ thật (Chart.js)
- **Donut** tỉ lệ sắc thái (tích cực/trung tính/tiêu cực).
- **Line** khối lượng theo thời gian (tổng + tiêu cực).
- **Bar ngang** Share of Voice (tỉ trọng mention theo brand), Top nguồn, Top chủ đề.

## C. Tính năng mới
1. **Trang Alerts**: lịch sử cảnh báo khủng hoảng (từ `/api/alerts`) + realtime, mức độ warning/critical.
2. **Modal chi tiết mention**: bấm 1 mention xem đầy đủ nội dung, sắc thái, chủ đề, link gốc.
3. **Xuất CSV** danh sách mention đang xem.
4. **Thông báo trình duyệt** (Web Notification) khi có khủng hoảng + toast.
5. **Auto-refresh** bật/tắt + chọn nhịp.
6. **Share of Voice** & **KPI** tổng quan.
7. **Quản lý brand/keyword** (đã có) — restyle cho đồng bộ.

## D. Kỹ thuật
- Thêm `chart.js` (npm) + wrapper `ChartView`.
- Giữ nguyên backend (đã có đủ endpoint: stats/overview, top, timeseries, mentions, alerts, report/*, brands).
- Không phá vỡ realtime SignalR.

## Thứ tự thực hiện
1. Design system + layout sidebar + theme.
2. Chart.js wrapper + Overview biểu đồ.
3. Alerts page + crisis notification/toast.
4. Mentions: modal + CSV export.
5. Reports: chart hoá.
6. Manage: restyle.
7. Polish (loading/empty/toast).
