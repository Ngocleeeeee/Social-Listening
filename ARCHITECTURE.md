# BrandRadar — Kiến trúc & Design Patterns

Tài liệu này liệt kê các **mẫu thiết kế microservice** đang dùng và vị trí trong code.

## Nguyên tắc tổng thể
- **Microservices** tách theo trách nhiệm: `Collector.Worker` (thu thập), `Analysis.Worker` (xử lý), `Dashboard.Api` (đọc/serve), `sentiment-nlp` (AI). Giao tiếp bất đồng bộ.
- **Database-per-need**: PostgreSQL (nguồn sự thật), Elasticsearch (search/aggregation), Redis (cache). Mỗi store dùng đúng thế mạnh.
- **Shared kernel** (`BrandRadar.Shared`): contracts, messaging, sentiment, persistence, logging dùng chung.

## Design patterns ↔ vị trí

| Pattern | Mô tả | Ở đâu |
|---|---|---|
| **Publisher/Subscriber** | Tách producer/consumer qua message broker | `IMentionPublisher`/`RabbitMqPublisher`, `IEventBus`/`KafkaEventBus` (Shared/Messaging) |
| **Competing Consumers** | Nhiều worker cùng đọc 1 queue để scale | `Analysis.Worker` (replicas) + RabbitMQ `mentions.ingest`, prefetch |
| **Repository** | Trừu tượng truy vấn dữ liệu sau interface | `IMentionQueryService` (ES), `IReportQueries` (SQL), `IBrandAdmin` (SQL) |
| **CQRS-lite** | Tách đường ghi/đọc | Ghi: EF Core (`BrandRadarDbContext`); Đọc: Dapper (SQL) + Elasticsearch |
| **Strategy** | Hoán đổi thuật toán qua interface | `ISentimentAnalyzer` → `LexiconSentimentAnalyzer` / `NlpSentimentAnalyzer` / `LlmSummarizer` |
| **Adapter** | Chuẩn hoá nguồn khác nhau về 1 hợp đồng | `RssCollector` (RSS + Atom), `BrandFeeds` (Google News/Reddit) → `RawMention` |
| **Options** | Bind cấu hình mạnh kiểu | `RabbitMqOptions`, `KafkaOptions`, `CrisisOptions`, `SentimentOptions`… |
| **Dependency Injection** | Toàn bộ service qua DI container | `AddBrandRadar*`, `Program.cs` các service |
| **Cache-Aside** | Đọc cache trước, miss thì tính rồi set | `ICache`/`RedisCache` (Dashboard/Caching) |
| **Bridge (Kafka→SignalR)** | Nối stream Kafka sang realtime web | `LiveConsumer` → `LiveHub` (Dashboard/Realtime) |
| **Resilience (Retry/Circuit-breaker/Timeout)** | Chịu lỗi khi gọi service ngoài | `AddStandardResilienceHandler` (Polly) trên HttpClient "resilient" — NLP, webhook, RSS |
| **Health/Readiness probe** | Phân biệt sống vs sẵn sàng | `/health/live` (process), `/health/ready` (kiểm tra Postgres + Redis) |
| **Idempotent consumer** | Bỏ trùng khi xử lý lại | `Analysis` khử trùng theo `ExternalId` trước khi index/publish |
| **Sliding-window detection** | Phát hiện khủng hoảng | `CrisisDetector` (đếm tiêu cực trong cửa sổ) |
| **Vertical Slice** | Tổ chức code theo tính năng | `Dashboard.Api`: Search / Reporting / Brands / Alerts / Auth / Realtime / Caching / Ai |
| **API Gateway-lite** | Một điểm vào, reverse proxy | nginx frontend proxy `/api` & `/hubs` |

## Cross-cutting (Dashboard.Api)
- **Auth**: JWT bearer, bảo vệ endpoint ghi bằng `[Authorize]`.
- **Rate limiting**: fixed-window theo IP cho controller.
- **Correlation-Id**: middleware gắn `X-Correlation-Id` vào response + log Serilog.
- **Error handling**: `UseExceptionHandler` + `ProblemDetails` (RFC 9457).
- **Compression**: `UseResponseCompression`.
- **Observability**: Serilog structured logs (enrich Application/CorrelationId), health checks.

## Độ tin cậy & mở rộng
- Worker **stateless** + **competing consumers** + **HPA** (xem `k8s/`).
- Tách **đường business (RabbitMQ)** khỏi **đường event/log (Kafka)** → scale độc lập.
- **Graceful degradation**: mất Redis/NLP/LLM vẫn chạy (fallback), ES query `IgnoreUnavailable`, publish best-effort.

## Hướng nâng cấp (production)
- **Outbox pattern** cho publish đảm bảo (ghi DB + dispatcher) thay vì publish trực tiếp.
- **Saga/Orchestration** nếu có quy trình nhiều bước.
- **API Gateway** thật (YARP/Kong) + service mesh.
- **OpenTelemetry** traces/metrics + Prometheus/Grafana.
