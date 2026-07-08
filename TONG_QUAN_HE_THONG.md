# BrandRadar — Tổng quan hệ thống (tài liệu phỏng vấn)

Hệ thống **social listening + crisis monitoring** theo kiến trúc **microservices hướng sự kiện (event-driven)**, viết bằng **.NET 9** (+ một microservice NLP bằng Python). Tài liệu này mô tả kiến trúc, luồng dữ liệu, kỹ thuật và design pattern đúng theo code thực tế.

---

## 1. Bức tranh lớn

Hệ thống có **hai xương sống message riêng biệt**, mỗi cái một nhiệm vụ — đây là quyết định thiết kế cốt lõi:

- **RabbitMQ** = hàng đợi *ingest* (work queue): phân phối việc xử lý từng bài, cần ack/nack + dead-letter để **không mất dữ liệu**.
- **Kafka** = *event stream* realtime: phát tán kết quả đã phân tích cho nhiều consumer độc lập (dashboard realtime, và có thể mở rộng thêm consumer khác) mà không ảnh hưởng lẫn nhau.

Tách "đảm bảo xử lý" (RabbitMQ) khỏi "phát tán realtime" (Kafka) thay vì nhồi cả hai vào một công nghệ.

### Luồng end-to-end

```
RSS / Google News / Reddit
      │  Collector.Worker (poll định kỳ)
      ▼
RabbitMQ  brandradar.exchange → ingest.queue  (→ DLQ nếu lỗi)
      │  Analysis.Worker (consume, prefetch song song, ack/nack)
      ▼
[Sentiment NLP] + [Topic] + [Brand match] + [Fingerprint] + [lang]
      │
      ├─► PostgreSQL   (EF Core — nguồn sự thật + index)
      ├─► Elasticsearch (index "mentions" — search + aggregate)
      └─► Kafka        (analyzed-mention / alerts)
                 │
                 ▼
        Dashboard.Api ──(LiveConsumer)── SignalR ──► Browser (realtime)
        Dashboard.Api ── REST ◄── đọc từ ES + Postgres + Redis + snapshot
```

---

## 2. Các service

### Collector.Worker — thu thập
`CollectorService : BackgroundService` dùng `PeriodicTimer` poll mỗi N giây. Mỗi vòng ghép **feed tĩnh** (theo chủ đề) với **feed động** do `BrandFeeds` sinh từ keyword brand trong DB (Google News query + Reddit search theo từng keyword). `RssCollector` parse được cả **RSS** (`<item>`) lẫn **Atom** (`<entry>` của Reddit), decode HTML entity, gắn User-Agent. Mỗi bài → `RawMention` publish vào RabbitMQ. Collector **không** khử trùng lặp — việc dedup để tầng sau lo (giữ collector đơn giản, idempotent hoá ở consumer).

### RabbitMQ — topology
`RabbitMqConnection.DeclareTopologyAsync` (idempotent): topic exchange `brandradar.exchange` → `ingest.queue` (durable) kèm `x-dead-letter-exchange` trỏ sang DLX → `ingest.dlq`. Message publish `Persistent = true`. Bài lỗi không mất mà rơi vào DLQ để điều tra. Một **connection dùng chung** (singleton), mở channel per-operation (đúng khuyến nghị RabbitMQ: connection share được, channel không thread-safe).

### Analysis.Worker — trái tim xử lý
`AnalysisConsumer : BackgroundService`:
- `BasicQos(prefetchCount)` → consume **song song có kiểm soát**; `autoAck: false` → chỉ ack khi xử lý xong.
- **Dedup idempotent**: check `Mentions.AnyAsync(ExternalId == raw.Id)`; trùng thì ack và bỏ qua (RSS re-fetch cùng bài mỗi vòng — đây là chỗ chặn double-count).
- Chạy: **sentiment** (qua interface `ISentimentAnalyzer`), **topic extraction**, **brand match**, phát hiện ngôn ngữ (regex dấu tiếng Việt → vi/en), **fingerprint** (gom tin trùng).
- Ghi **PostgreSQL** (`PublishedAt.ToUniversalTime()` vì `timestamptz` yêu cầu UTC) → index **Elasticsearch** → publish **Kafka** `analyzed-mention`.
- Sau đó chạy **spike detection** (mọi sentiment) và nếu Negative thì **crisis detection**.
- Lỗi → `BasicNack(requeue: false)` → dead-letter.

At-least-once (RabbitMQ) + dedup theo `ExternalId` unique = hiệu quả **exactly-once về mặt dữ liệu**.

### sentiment-nlp — microservice NLP (Python)
FastAPI chạy model transformer `cardiffnlp/twitter-xlm-roberta-base-sentiment` (đa ngôn ngữ, hợp VN + EN). Expose `POST /analyze {text} → {label, score}`. Score có **dấu** (positive = +conf, negative = −conf, neutral = 0) để tính điểm cảm xúc trung bình có ý nghĩa. Tách sang Python vì hệ sinh thái ML mạnh hơn .NET.

### Dashboard.Api — mặt đọc
- `LiveConsumer : BackgroundService` subscribe Kafka `analyzed-mention` + `alerts`, relay thẳng qua **SignalR** (`hub.Clients.All.SendAsync`). GroupId random + `AutoOffsetReset.Latest` → mỗi lần khởi động chỉ nhận event mới, không replay lịch sử (đúng cho realtime feed).
- REST controllers đọc từ ES (search/aggregate), Postgres (báo cáo Dapper), Redis (cache), và **snapshot read-model**.

### Frontend — React + Vite
8 tab; `useLive.js` mở SignalR nhận `mention`/`alert`, toast + beep khi khủng hoảng, theme sáng/tối, login modal lấy JWT.

---

## 3. Hạ tầng — mỗi thứ để làm gì

| Thành phần | Vai trò | Chi tiết đáng nói |
|---|---|---|
| **PostgreSQL** | System of record (ghi qua EF Core) | Index có chủ đích: `ExternalId` unique (dedup), `Sentiment`, `Source`, `Fingerprint`, composite `(BrandId, PublishedAt)` — đúng cột crisis/spike query lọc |
| **Elasticsearch** | Search + analytics | **Index template** map `keyword`/`text`/`date` đúng kiểu (bắt buộc để aggregate/sort chính xác); tạo lúc Analysis khởi động, idempotent |
| **Redis** | Cache-aside + trạng thái ack alert | `abortConnect=false` + graceful null → thiếu Redis app vẫn chạy |
| **Kafka** (KRaft, không ZooKeeper) | Event backbone realtime | Fan-out cho nhiều consumer độc lập |
| **Prometheus + Grafana** | Observability | `/metrics` → scrape → dashboard RED (Rate/Errors/Duration) |

---

## 4. Design pattern dùng ở đâu

- **Strategy** — `ISentimentAnalyzer` 3 hiện thực: `Lexicon` / `Nlp` / `Llm(Ollama)`, chọn qua config `Sentiment:Provider`. Đổi thuật toán không sửa consumer.
- **Decorator / Fallback** — `NlpSentimentAnalyzer` wrap `LexiconSentimentAnalyzer`: NLP lỗi/timeout thì rơi về lexicon → pipeline không bao giờ đứng.
- **CQRS-lite** — tách đường ghi (EF Core, chuẩn hoá, transaction) khỏi đường đọc (Dapper raw SQL + Elasticsearch). Mỗi bên tối ưu riêng.
- **Repository / Query Object** — `IReportQueries` (Dapper), `IMentionQueryService` (ES) đóng gói truy vấn.
- **Read-model / Materialized view + Cache-warming** — `SnapshotRefresher : BackgroundService` cứ 5s tính sẵn dashboard toàn brand vào `ISnapshotStore`; API trả tức thì từ RAM thay vì aggregate ES mỗi request.
- **Publisher / Subscriber + Message Bus** — `IMentionPublisher` (Rabbit), `IEventBus` (Kafka) trừu tượng hoá hạ tầng message.
- **Options pattern** — mọi config bind qua `IOptions<T>` (`RabbitMqOptions`, `KafkaOptions`, `CrisisOptions`, `SentimentOptions`, `ElasticOptions`).
- **Background worker / Hosted service** — Collector, Analysis, LiveConsumer, SnapshotRefresher.
- **Sidecar / microservice tách biệt** — NLP tách hẳn sang Python.
- **Dead Letter Queue** — xử lý lỗi không mất message.
- **Singleton shared connection** — `RabbitMqConnection`.

---

## 5. Kỹ thuật & quyết định thiết kế đáng kể

**Native ES aggregations + fallback** (`ComputeDashboardAsync`): dashboard tính bằng **một** query ES với `terms` (sentiment/source/topics/brand), `date_histogram` (theo giờ), `filter` (cur24 vs prev24 cho trend) — tính *trong* ES trên toàn dataset, không kéo docs về app. ES/agg lỗi → tự rơi về `ComputeDashboardInMemoryAsync`. Tối ưu nhưng vẫn resilient.

**Story clustering chống trùng tin** (`Fingerprint.Of`): bỏ dấu tiếng Việt (Unicode FormD), chuẩn hoá, lấy 12 từ đầu, SHA256 → 16 hex. Cùng tin nhiều báo đăng lại → cùng fingerprint → tab "Tin nổi bật" gom thành 1 story kèm số nguồn đưa tin (đo độ lan truyền).

**Crisis detection (sliding window)** + **spike detection** (`CrisisDetector`): đếm mention Negative của brand trong `WindowMinutes`; vượt `NegativeThreshold` và không có alert trong `CooldownMinutes` (chống spam) → tạo Alert (`warning`/`critical`) → ghi Postgres + ES + Kafka + webhook Slack. Spike so cửa sổ hiện tại vs trước theo `SpikeMultiplier`. Cả hai đọc Postgres nhờ composite index `(BrandId, PublishedAt)`.

**Idempotency & consistency**: at-least-once + dedup `ExternalId` = không double-count. Ghi Postgres trước, ES sau, Kafka cuối — Kafka fail thì dữ liệu vẫn nằm trong 2 store bền vững.

**Resilience**: `AddStandardResilienceHandler` (Polly) cho HttpClient "resilient" (gọi NLP, webhook) — retry/timeout/circuit-breaker. NLP timeout 20s + fallback lexicon.

**Cross-cutting API**: JWT bearer (bảo vệ ghi), rate limiting (fixed-window 120 req/60s theo IP), correlation-id middleware (gắn mọi log Serilog), ProblemDetails, response compression, health `live`/`ready` (tag "ready" = Postgres + Redis), Swagger có Authorize.

**Observability**: `/metrics` Prometheus (`UseHttpMetrics` + `MapMetrics`) → Prometheus scrape → Grafana dashboard RED: RPS, p50/p95/p99, 5xx, top endpoint, GC heap.

**Deploy**: Docker Compose (dev, port host đổi tránh xung đột) + Kubernetes đầy đủ (Deployment, Service, ConfigMap/Secret, HPA, Ingress, kustomize).

---

## 6. Vì sao khớp JD iComm

| Yêu cầu JD | Đáp ứng trong BrandRadar |
|---|---|
| Raw SQL + query optimization + schema design | Dapper `GROUP BY ... FILTER` + index có chủ đích |
| Elasticsearch / Solr | ES native aggregations + index template |
| Microservices + Message Queue RabbitMQ/Kafka | Hai message bus tách vai trò rõ ràng |
| API Services + Worker Services C# .NET Core | Đúng cấu trúc project (4 service + Shared) |
| High-performance / scalable backend | Read-model snapshot, cache, prefetch song song, HPA |

---

## 7. Cấu trúc project

```
src/
  BrandRadar.Shared/   Contracts, Messaging (Rabbit+Kafka), Persistence (EF), Sentiment (Strategy), Text/Fingerprint
  Collector.Worker/    RSS/Atom + Google News + Reddit → RabbitMQ
  Analysis.Worker/     Consume → sentiment/brand/fingerprint → PG + ES + Kafka; Crisis/Spike detector
  Dashboard.Api/       REST + SignalR + snapshot read-model + auth + observability
  BrandRadar.Tests/    xUnit
sentiment-nlp/         FastAPI + transformer (Python)
frontend/              React + Vite
k8s/                   Manifest K8s đầy đủ
observability/         Prometheus + Grafana provisioning
```

