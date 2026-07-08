# BrandRadar — Social Listening & Crisis Monitoring

Mini bản của sản phẩm lõi iComm (Social Listening & Monitoring + Crisis Management), dựng để đi phỏng vấn.
Codebase **mới hoàn toàn**, tách khỏi DistributedSync.

## 1. Mục tiêu

Theo dõi các bài/tin/bình luận nhắc đến một thương hiệu, phân tích **sắc thái** (tích cực / trung tính / tiêu cực),
thống kê **big data** theo thời gian & nguồn, và **cảnh báo khủng hoảng truyền thông realtime** khi tiêu cực tăng đột biến.

Ràng buộc: chạm đủ JD (API + Worker + Message Queue + Elasticsearch + SQL thuần), AI/ML là **optional** (sentiment mặc định bằng từ điển, LLM chỉ là nâng cấp).

## 2. Kiến trúc

```
Nguồn (RSS báo VN + bộ giả lập)
        │
        ▼
Collector.Worker ──(RabbitMQ: mentions.ingest)──▶ Analysis.Worker
                                                    │  làm sạch → sentiment + tách chủ đề → match brand
                                                    ├─▶ PostgreSQL (nguồn sự thật, SQL thuần khi đọc)
                                                    ├─▶ Elasticsearch (search + aggregation "big data")
                                                    └─▶ Kafka (analyzed-mention, alerts)
                                                                 │
                                                                 ▼
                                                    Dashboard.Api  ──(SignalR)──▶ Frontend (React)
                                                    (REST query ES)              feed realtime + biểu đồ + cảnh báo
```

- **RabbitMQ** = phân phối công việc ingest (nhiều Analysis.Worker cùng chạy → competing consumers, scale ngang).
- **Kafka** = event bus realtime (analyzed-mention, alerts) đẩy sang SignalR; dễ mở rộng/replay.
- **Không dùng Kafka Connect** (Analysis.Worker ghi thẳng ES) → nhẹ hơn DistributedSync.

## 3. Các service (microservices)

| Service | Loại | Trách nhiệm |
|---|---|---|
| `Collector.Worker` | Worker | Định kỳ kéo RSS thật + sinh mention giả → publish `RawMention` vào RabbitMQ |
| `Analysis.Worker` | Worker | Consume RawMention → normalize, phát hiện ngôn ngữ, **sentiment (từ điển)**, tách keyword/chủ đề, match brand → ghi Postgres + index ES + publish Kafka |
| `Alerting` | (trong Analysis hoặc consumer riêng) | Cửa sổ trượt đếm tiêu cực theo brand → tạo `Alert` khi vượt ngưỡng → Kafka `alerts` |
| `Dashboard.Api` | API | REST truy vấn ES (search, aggregation) + quản lý brand (SQL) + **SignalR hub** đẩy mention/alert realtime (consume Kafka) |
| `frontend` | React + nginx | Dashboard: overview, live feed, search, alerts |

## 4. Dữ liệu

### PostgreSQL (ghi bằng EF Core, đọc bằng Dapper — SQL thuần)
- `brands(id, name, created_at)` và `brand_keywords(id, brand_id, keyword)`
- `mentions(id, brand_id, source, author, title, content, url, lang, sentiment, sentiment_score, topics, published_at, collected_at)`
- `alerts(id, brand_id, level, reason, window_start, window_end, negative_count, created_at)`
- Index: `mentions(brand_id, published_at desc)`, `mentions(sentiment)`, `mentions(source)`.

### Elasticsearch (search + big data aggregation)
- Index `mentions`: brand, source, sentiment, content, topics, publishedAt…
- Truy vấn: full-text search, **date_histogram** (volume theo thời gian), **terms agg** (top nguồn, top keyword, tỉ lệ sentiment).

## 5. Sentiment & chủ đề (AI optional)

- Mặc định: **từ điển tích cực/tiêu cực (VN + EN)** → điểm = (pos − neg)/tổng từ cảm xúc → nhãn Positive/Neutral/Negative. Giải thích được, không cần key, chạy nhanh.
- Optional: cắm **LLM** (Ollama local / OpenAI) qua một interface `ISentimentAnalyzer` — đổi bằng config, không sửa luồng.
- Tách chủ đề: rút keyword nổi bật (tần suất, bỏ stopword).

## 6. Phát hiện khủng hoảng

- Cửa sổ trượt (vd 15 phút) theo brand: nếu **số mention tiêu cực > ngưỡng** và **tỉ lệ tiêu cực > X%** → tạo Alert (level: warning/critical) → đẩy SignalR → banner đỏ trên dashboard.
- Ngưỡng cấu hình bằng Options Pattern.

## 7. API (Dashboard.Api)

- `GET /api/mentions` — search + filter (brand, sentiment, keyword, from/to) + **phân trang** (ES).
- `GET /api/stats/overview?brand=` — tổng, tỉ lệ theo sentiment, số alert.
- `GET /api/stats/timeseries?brand=&interval=` — volume + sentiment theo thời gian (date_histogram).
- `GET /api/stats/top?field=source|keyword` — terms aggregation.
- `GET /api/alerts` — cảnh báo gần đây.
- `GET/POST /api/brands` — quản lý brand theo dõi (SQL thuần).
- SignalR `/hubs/live` — sự kiện `mention`, `alert`.

## 8. Màn hình Frontend

1. **Overview** — KPI (tổng mention, % tiêu cực, alert đang mở), donut sentiment, biểu đồ volume theo giờ, top nguồn/keyword.
2. **Live feed** — mention chạy realtime, tô màu theo sentiment; **banner cảnh báo khủng hoảng**.
3. **Search** — lọc & tra cứu mention.
4. **Alerts** — lịch sử cảnh báo.

## 9. Tech stack ↔ JD

| JD | Trong project |
|---|---|
| API Services & Worker Services (.NET Core) | Dashboard.Api · Collector.Worker, Analysis.Worker |
| Microservices + Message Queue (RabbitMQ, Kafka) | RabbitMQ (ingest) + Kafka (realtime/alerts) |
| API truy vấn Elasticsearch | search + date_histogram + terms aggregation |
| SQL thuần, tối ưu, thiết kế schema | Dapper (đọc), EF Core (ghi), index thiết kế theo truy vấn |
| Hiệu năng cao, mở rộng | competing consumers, prefetch, caching, phân trang, async |
| (iComm) AI, Big Data | sentiment ML/LLM, aggregation trên Elasticsearch |

## 10. Lộ trình (làm dần)

- **Phase 0**: solution + docker-compose (Postgres, RabbitMQ, Kafka, Elasticsearch, Kibana).
- **Phase 1**: Collector (giả lập + RSS) → RabbitMQ; Analysis.Worker → Postgres + ES; sentiment từ điển.
- **Phase 2**: Dashboard.Api (ES search + aggregation) + Frontend Overview & Search.
- **Phase 3**: Kafka + SignalR realtime feed + phát hiện & cảnh báo khủng hoảng.
- **Phase 4**: Dapper/SQL thuần + index + caching + health.
- **Phase 5 (optional)**: LLM sentiment, quản lý brand, Kibana dashboard.

## 11. Vì sao khớp iComm

Đây gần như là bản thu nhỏ **chính sản phẩm "Social Listening & Monitoring" + "Crisis Management"** của iComm,
dùng đúng bộ công nghệ họ nhấn mạnh (AI, Big Data) và trọn vẹn Jax JD. Đến phỏng vấn nói được "em đã dựng thử một
phiên bản của đúng thứ công ty đang làm" — lợi thế lớn.
