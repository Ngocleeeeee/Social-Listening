# BrandRadar — Kịch bản demo & Q&A phỏng vấn (iComm)

Tài liệu cầm tay khi phỏng vấn: cách trình diễn, kiến trúc, và câu trả lời cho các câu hỏi hay gặp.

## 1. Pitch 30 giây

> "Em dựng thử một phiên bản thu nhỏ của chính sản phẩm lõi iComm — **Social Listening & Crisis Monitoring**. Hệ thống thu thập tin tức/mạng xã hội theo thương hiệu, dùng **NLP** phân tích sắc thái, lưu vào **Elasticsearch** để tìm kiếm/thống kê dữ liệu lớn, đẩy **realtime** qua Kafka + SignalR, và **tự cảnh báo khủng hoảng truyền thông**. Viết bằng **.NET 9 microservices**, đúng stack mô tả trong JD: API + Worker + RabbitMQ + Kafka + Elasticsearch + Redis + SQL thuần."

## 2. Kịch bản demo (theo tab)

1. **Tổng quan** — chỉ KPI (tổng/tích cực/trung tính/tiêu cực), biểu đồ khối lượng theo giờ, donut sắc thái, Share of Voice theo thương hiệu, chủ đề nổi bật. Nói: "toàn bộ số liệu tổng hợp từ Elasticsearch + SQL, cache Redis 5s."
2. **Trực tiếp** — chờ mention mới chảy về realtime (SignalR). Nói: "event đẩy từ Kafka xuống trình duyệt, không phải polling."
3. **Mentions** — lọc theo sắc thái/từ khoá, phân trang, bấm 1 mention mở **modal chi tiết** + link bài gốc; nút **CSV**.
4. **Báo cáo** — biểu đồ sắc thái theo brand (bar chồng) + khối lượng theo ngày; nguồn **SQL GROUP BY** (Dapper) trên PostgreSQL.
5. **Cảnh báo** — lịch sử + realtime; giải thích cơ chế cửa sổ trượt. Nếu muốn chắc chắn có cảnh báo lúc demo: bật simulator (Collector) tạm thời.
6. **Quản lý** — thêm một thương hiệu mới (vd "Bamboo Airways") → nói: "collector tự sinh nguồn Google News/Reddit cho brand này, analyzer tự khớp trong ~1 phút, không cần deploy lại."

## 3. Kiến trúc (một câu)

`Collector (RSS/Google News/Reddit) → RabbitMQ → Analysis (NLP sentiment) → PostgreSQL + Elasticsearch + Kafka → Dashboard.Api (ES + SQL + Redis + SignalR) → React`

- **RabbitMQ**: hàng đợi công việc ingest (competing consumers, retry, DLQ).
- **Kafka**: event stream realtime (analyzed-mention, alerts) → SignalR.
- **Elasticsearch**: tìm kiếm full-text + aggregation trên dữ liệu lớn.
- **PostgreSQL + Dapper**: nguồn sự thật + báo cáo SQL thuần tối ưu.
- **Redis**: cache-aside cho endpoint đọc nhiều.
- **NLP microservice (Python/transformer)**: sentiment đa ngữ, gọi qua HTTP.

## 4. Câu hỏi phỏng vấn hay gặp & trả lời

**Q: Vì sao dùng cả RabbitMQ lẫn Kafka?**
A: Hai vai trò khác nhau. RabbitMQ cho **công việc** (mỗi mention xử lý đúng một lần, cần retry/DLQ, competing consumers để scale). Kafka cho **luồng sự kiện** (nhiều consumer đọc lại được, hợp realtime + có thể replay/mở rộng partition). Tách đường ghi (business) khỏi đường event/log.

**Q: Elasticsearch để làm gì, khác PostgreSQL chỗ nào?**
A: Postgres là nguồn sự thật quan hệ, tối ưu ghi/giao dịch và báo cáo GROUP BY. Elasticsearch tối ưu **full-text search + aggregation** trên hàng triệu/tỷ bản ghi với độ trễ vài chục ms — đúng nhu cầu tìm mention/timeline/thống kê. Em dùng **read path kép**: ES cho tìm kiếm, SQL cho báo cáo chính xác.

**Q: SQL thuần chỗ nào, tối ưu ra sao?**
A: Read side dùng **Dapper** (SQL thuần) tách khỏi EF (ghi). Kỹ thuật: chỉ SELECT cột cần, tham số hoá (chống injection + tái dùng plan), phân trang, `GROUP BY`/`FILTER`, **index thiết kế theo truy vấn** (trên `BrandId+PublishedAt`, `Sentiment`, `Source`), connection pooling. Bảng lớn thì chuyển sang keyset pagination và kiểm tra bằng `EXPLAIN ANALYZE`.

**Q: Hệ thống scale thế nào?**
A: Worker **stateless** → chạy nhiều instance cùng đọc một queue (RabbitMQ chia tải, prefetch để backpressure). Kafka partition theo key để tăng song song. ES/Redis scale ngang. API stateless sau load balancer. NLP là microservice riêng, scale độc lập.

**Q: Realtime làm sao?**
A: Analysis phát event lên Kafka; Dashboard.Api có background consumer bắc cầu Kafka → **SignalR** đẩy xuống trình duyệt. Không polling, độ trễ ~ vài trăm ms.

**Q: Phân tích cảm xúc bằng gì?**
A: Mặc định một **microservice NLP** dùng transformer đa ngữ (XLM-R sentiment). Kiến trúc pluggable qua interface `ISentimentAnalyzer` — có bản từ điển (chạy nhẹ, fallback) và có thể cắm LLM. .NET gọi NLP qua HTTP → kiến trúc **polyglot microservices**.

**Q: Phát hiện khủng hoảng?**
A: **Cửa sổ trượt** theo brand: nếu số mention tiêu cực vượt ngưỡng trong khoảng thời gian (và chưa có cảnh báo gần đây) → tạo Alert, lưu Postgres, index ES, phát Kafka → cảnh báo realtime + toast + Web Notification.

**Q: Chịu lỗi thế nào?**
A: RabbitMQ **DLQ** cho message hỏng; consumer khử trùng (idempotent theo ExternalId); ES query có `IgnoreUnavailable`; NLP/Redis **fallback graceful** (mất Redis vẫn chạy, mất NLP thì dùng từ điển); publisher Kafka best-effort.

**Q: Dữ liệu thật hay giả?**
A: Thật — RSS báo VN (VnExpress, Tuổi Trẻ, Thanh Niên, VietnamNet) + **Google News theo brand** + **Reddit** (Atom). Facebook/TikTok cần API trả phí/duyệt nên chưa cắm; collector **pluggable**, thêm nguồn chỉ là viết adapter publish vào cùng RabbitMQ.

**Q: Hạn chế & hướng phát triển?**
A: Sentiment tiếng Việt có thể nâng bằng PhoBERT/viSoBERT; aspect-based sentiment; LLM tóm tắt khủng hoảng; ES aggregation gốc thay cho gom in-memory; thêm auth, tests, CI/CD, Kubernetes.

## 5. Điểm nhấn nên nói
- Đúng **sản phẩm lõi iComm** (social listening + crisis).
- Đúng **stack JD**: .NET API/Worker + RabbitMQ + Kafka + Elasticsearch + Redis + SQL thuần + AI.
- Thiết kế **pluggable** (nguồn, sentiment) và **chịu lỗi graceful**.
