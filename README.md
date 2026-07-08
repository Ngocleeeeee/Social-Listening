# BrandRadar — Social Listening & Crisis Monitoring

Nền tảng **lắng nghe mạng xã hội & phát hiện khủng hoảng truyền thông**, xây dựng bằng **.NET 9 microservices** theo kiến trúc **hướng sự kiện (event-driven)**. Hệ thống thu thập tin theo thương hiệu từ nguồn thật, phân tích **sắc thái bằng NLP**, đánh chỉ mục để tìm kiếm/thống kê trên **Elasticsearch**, đẩy dữ liệu **realtime** qua Kafka + SignalR, chấm **điểm sức khỏe thương hiệu**, và **tự động cảnh báo khủng hoảng** theo luật cấu hình được.

> Dự án mô phỏng sản phẩm lõi của một công ty Media/Tech (Social Listening, Crisis Management, Big Data, AI) — bám sát yêu cầu tuyển dụng: **API + Worker Services (.NET Core), Microservices + Message Queue (RabbitMQ/Kafka), Elasticsearch, SQL thuần + tối ưu index, backend hiệu năng cao & mở rộng được**.

---

## 1. Kiến trúc tổng thể

Hệ thống dùng **hai xương sống message tách vai trò** — đây là quyết định thiết kế cốt lõi:

- **RabbitMQ** — hàng đợi *ingest* (work queue): phân phối việc xử lý từng bài, có ack/nack + dead-letter để **không mất dữ liệu**.
- **Kafka** — *event stream* realtime: phát tán kết quả đã phân tích cho nhiều consumer độc lập (dashboard realtime, có thể mở rộng thêm) mà không ảnh hưởng nhau.

```
Nguồn thật: RSS báo VN · Google News (theo brand) · Reddit
      │  Collector.Worker  (BackgroundService, poll định kỳ)
      ▼
RabbitMQ  mentions.exchange → mentions.ingest   (→ DLQ nếu lỗi)
      │  Analysis.Worker  (consume, prefetch song song, dedup, ack/nack)
      ▼
  Sentiment NLP + Topic + Brand match + Fingerprint + Language
      │
      ├─► PostgreSQL      (EF Core — nguồn sự thật + index)
      ├─► Elasticsearch   (index "mentions" — full-text + aggregations)
      └─► Kafka           (analyzed-mention / alerts)
                 │
                 ▼
        Dashboard.Api  ──(LiveConsumer)── SignalR ──► Trình duyệt (realtime)
        Dashboard.Api  ── REST ◄── Elasticsearch · PostgreSQL(Dapper) · Redis · snapshot
```

## 2. Các service & công nghệ

| Thành phần | Vai trò |
|---|---|
| `Collector.Worker` | Kéo RSS/Google News/Reddit (parse cả RSS lẫn Atom) → publish `RawMention` vào RabbitMQ. Feed brand sinh **động** từ keyword trong DB. Dedup ổn định theo `source + title`. |
| `Analysis.Worker` | Consume RabbitMQ → **NLP sentiment** + topic + brand match + fingerprint → ghi PostgreSQL + Elasticsearch + Kafka. Chứa **CrisisDetector** (cửa sổ trượt + spike) và **RuleEngine** (luật cấu hình được). |
| `sentiment-nlp` | Microservice **Python (FastAPI + transformer đa ngữ)** chấm sentiment (`cardiffnlp/twitter-xlm-roberta-base-sentiment`). |
| `Dashboard.Api` | REST đọc **Elasticsearch** (search/aggregation) + **SQL thuần/Dapper** (report) + **Redis** cache + **SignalR** realtime + **JWT** auth + **Prometheus** metrics. |
| `frontend` | **React + Vite + Chart.js**, phục vụ qua nginx (reverse-proxy `/api` & `/hubs`). 10 tab, biểu đồ, realtime, sáng/tối. |
| Hạ tầng | PostgreSQL 16 · RabbitMQ · Kafka (KRaft) · Elasticsearch 8 · Redis · Kibana · Prometheus · Grafana · (Ollama tùy chọn) |

**Stack .NET**: EF Core 9 (ghi) + Dapper (đọc raw SQL) · Elastic.Clients.Elasticsearch · Confluent.Kafka · RabbitMQ.Client · StackExchange.Redis · SignalR · Serilog · JWT Bearer · Polly (resilience) · prometheus-net.

## 3. Design pattern

- **Strategy** — `ISentimentAnalyzer` (Lexicon / NLP / LLM), và `INotificationChannel` (in-app / Slack / webhook) cho cảnh báo. Đổi thuật toán/kênh không sửa lõi.
- **Decorator / Fallback** — NLP lỗi/timeout tự rơi về lexicon, pipeline không đứng.
- **CQRS-lite** — đường ghi (EF Core, transaction) tách khỏi đường đọc (Dapper raw SQL + Elasticsearch), mỗi bên tối ưu riêng.
- **Read-model / Cache-warming** — `SnapshotRefresher` tính sẵn dashboard mỗi 5s vào bộ nhớ, API trả tức thì.
- **Publisher/Subscriber + Message Bus** — `IMentionPublisher` (Rabbit), `IEventBus` (Kafka) trừu tượng hoá hạ tầng.
- **Rules Engine** — tách *chính sách* (luật trong DB) khỏi *cơ chế* (bộ đánh giá + kênh gửi).
- **Options / Background worker / DLQ / Singleton shared connection**.

## 4. Tính năng nổi bật

- **Realtime** — feed mention + cảnh báo đẩy tức thì (Kafka → SignalR), toast + Web Notification + âm báo. Tab Trực tiếp chỉ hiện bài mới đăng trong 6h.
- **Brand Health & Competitive Intelligence** (`GET /api/health`) — **Brand Health Index (0–100)**, **Share of Voice** so kè đối thủ, đà thảo luận và **insight tự sinh**; tính bằng một native ES aggregation, cache 15s.
- **Alert Rules Engine** (`/api/rules`) — luật cảnh báo **cấu hình được** (điều kiện: số tiêu cực / lượng nhắc / % tiêu cực; cửa sổ; cooldown; kênh gửi) thay ngưỡng hard-code; `RuleEngine` đánh giá realtime, gửi đa kênh.
- **Crisis management** — phát hiện khủng hoảng (cửa sổ trượt) + phát hiện tăng đột biến → cảnh báo (lưu ES/PG), **ack** (Redis), **webhook** (Slack), **tóm tắt** trích xuất **+ LLM** (Ollama, tùy chọn).
- **Phân tích** — donut sắc thái, khối lượng theo giờ, đám mây chủ đề, xu hướng 24h, brand đang "nóng lên", **story clustering** (gom tin trùng theo fingerprint), **so sánh 2 thương hiệu**.
- **Mentions** — tìm kiếm + lọc (sắc thái/ngôn ngữ/từ khoá), **sắp xếp theo thời gian đăng hoặc thu thập**, phân trang + tổng số, highlight, modal chi tiết, mở bài gốc, **xuất CSV**.
- **Quản lý brand/keyword động** — thêm brand → tự crawl + khớp; **JWT** cho thao tác ghi.
- **Báo cáo SQL** (GROUP BY qua Dapper) + in PDF; giao diện sáng/tối.
- **Vận hành** — Redis cache, rate limiting, correlation-id, ProblemDetails, response compression, health `live`/`ready`, Swagger có Authorize, unit test + GitHub Actions CI.
- **Observability** — `/metrics` (Prometheus) → Prometheus scrape → Grafana dashboard (RPS, p50/p95/p99, 5xx, top endpoint, GC heap).

## 5. Chạy nhanh

```bash
cd SocialListening
docker compose up -d --build          # lần đầu ~1–2 phút (ES + model NLP tải về)
```

| Giao diện | URL |
|---|---|
| Frontend | http://localhost:3000 |
| Dashboard API (Swagger) | http://localhost:8090/swagger |
| RabbitMQ | http://localhost:15673 (guest/guest) |
| Kibana | http://localhost:5602 |
| Elasticsearch | http://localhost:9201 |
| Prometheus | http://localhost:9090 |
| Grafana (dashboard "BrandRadar") | http://localhost:3001 |
| Metrics (raw) | http://localhost:8090/metrics |

Đăng nhập thao tác ghi (quản lý brand, luật cảnh báo, ack): **admin / admin123** (đổi qua `.env`).

**Khi nào cần `down -v`:** chỉ khi **đổi schema DB** (thêm bảng/cột). Sửa code thường thì `docker compose up -d --build <service>` để giữ dữ liệu — tránh nạp lại toàn bộ backlog.

### Bật LLM tóm tắt khủng hoảng (tùy chọn)
```bash
docker compose --profile llm up -d ollama
docker compose exec ollama ollama pull llama3.2:1b
```
Không bật → hệ thống dùng tóm tắt trích xuất (vẫn hoạt động).

### Cấu hình bí mật
Copy `.env.example` → `.env`, đổi `JWT_KEY`, `ADMIN_PASSWORD`.

### Kiểm thử
```bash
dotnet test          # unit test sentiment
```
CI tự chạy build + test + build frontend qua `.github/workflows/ci.yml`.

## 6. API chính (Dashboard.Api)

| Nhóm | Endpoint |
|---|---|
| Mentions | `GET /api/mentions` (filter · sort · paging) · `/api/mentions/count` · `/api/mentions/export` (CSV) |
| Thống kê | `GET /api/stats/overview\|top\|timeseries\|trend\|dashboard` |
| Sức khỏe TH | `GET /api/health` (BHI + Share of Voice + insight) |
| Báo cáo (SQL) | `GET /api/report/brands\|daily\|trending` |
| Tin nổi bật | `GET /api/stories` (story clustering) |
| Cảnh báo | `GET /api/alerts` · `GET /api/alerts/summary` · `POST /api/alerts/{id}/ack` 🔒 |
| Luật cảnh báo | `GET /api/rules` · `POST /api/rules` 🔒 · `PUT /api/rules/{id}` 🔒 · `POST /api/rules/{id}/toggle` 🔒 · `DELETE /api/rules/{id}` 🔒 |
| Brand | `GET/POST/DELETE /api/brands` 🔒 · `POST/DELETE /api/brands/{id}/keywords` 🔒 |
| Auth | `POST /api/auth/login` → JWT |
| Vận hành | `GET /health/live` · `/health/ready` · `/metrics` · SignalR `/hubs/live` |

(🔒 = cần JWT)

## 7. Triển khai Kubernetes

Bộ manifest đầy đủ ở **`k8s/`**: namespace, ConfigMap/Secret, infra, apps, **HPA autoscaling**, ingress. Triển khai: `kubectl apply -k k8s/`. Chi tiết ở `k8s/README.md`.

## 8. Tài liệu khác

- `TONG_QUAN_HE_THONG.md` — mô tả chi tiết kiến trúc, luồng, pattern (bản dài).
- `DEMO_VA_PHONG_VAN.md` — kịch bản demo + hỏi đáp phỏng vấn.
- `PLAN.md` · `UI_PLAN.md` — thiết kế ban đầu · `k8s/README.md` — triển khai K8s.

## 9. Ghi chú

- Cổng host đổi (5433/5673/9201/5602/8090/3000/9090/3001) để không đụng stack khác.
- Facebook/TikTok cần API trả phí/duyệt nên chưa cắm; collector **pluggable** — thêm nguồn = viết 1 adapter.
- Schema tạo bằng `EnsureCreated` (hợp demo); production nên chuyển sang EF **migrations**.
