# Triển khai BrandRadar trên Linux server

Toàn bộ hệ thống chạy bằng Docker nên trên Linux **không cần sửa code**. Hướng dẫn dưới đây cho Ubuntu/Debian (các distro khác tương tự).

## 1. Cài Docker + Docker Compose

```bash
sudo apt update
sudo apt install -y ca-certificates curl git
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker $USER      # đăng xuất/đăng nhập lại để dùng docker không cần sudo
```

Kiểm tra:
```bash
docker --version
docker compose version
```

## 2. Chỉnh sysctl cho Elasticsearch (BẮT BUỘC)

Elasticsearch cần `vm.max_map_count` đủ lớn, nếu không container sẽ crash:

```bash
sudo sysctl -w vm.max_map_count=262144
echo "vm.max_map_count=262144" | sudo tee -a /etc/sysctl.conf
```

## 3. Yêu cầu tài nguyên

- **RAM tối thiểu 6–8 GB** (ES + Kafka + Postgres + model NLP đều nặng).
- Máy nhỏ có thể bỏ bớt service không thiết yếu:
  ```bash
  docker compose up -d --build \
    postgres rabbitmq elasticsearch redis kafka \
    collector sentiment-nlp analysis dashboard-api frontend
  # (bỏ kibana, prometheus, grafana)
  ```

## 4. Lấy code & chạy

```bash
git clone <repo-url>
cd SocialListening
cp .env.example .env
# Sửa .env: đặt JWT_KEY (>= 32 ký tự) và ADMIN_PASSWORD mạnh
nano .env

docker compose up -d --build      # lần đầu ~2–5 phút (tải image + model NLP)
```

Theo dõi:
```bash
docker compose ps
docker compose logs -f analysis dashboard-api
```

## 5. Truy cập

Mặc định các cổng đã publish ra host (đổi để tránh xung đột):

| Dịch vụ | Cổng |
|---|---|
| Frontend | 3000 |
| Dashboard API / Swagger | 8090 |
| Grafana | 3001 |
| Prometheus | 9090 |
| Kibana | 5602 |

### Mở firewall (nếu truy cập từ ngoài)
```bash
sudo ufw allow 3000/tcp        # frontend
sudo ufw allow 8090/tcp        # API (nếu cần gọi trực tiếp)
```

## 6. Đặt reverse proxy + HTTPS (khuyến nghị cho production)

Thay vì mở nhiều cổng, đặt **Caddy** (tự động HTTPS) hoặc **nginx** phía trước, chỉ expose 443.

Ví dụ `Caddyfile`:
```
brandradar.example.com {
    reverse_proxy localhost:3000
}
```
```bash
sudo apt install -y caddy
sudo nano /etc/caddy/Caddyfile
sudo systemctl restart caddy
```
Frontend nginx đã proxy sẵn `/api` và `/hubs` về Dashboard API, nên chỉ cần trỏ về cổng 3000.

## 7. Tự khởi động lại khi reboot

`docker-compose.yml` đã đặt `restart: unless-stopped` cho mọi service → Docker tự bật lại container khi máy khởi động lại (miễn là Docker daemon bật cùng hệ thống, mặc định `systemctl enable docker`).

Kiểm tra Docker tự chạy khi boot:
```bash
sudo systemctl enable docker
```

## 8. Vận hành

```bash
docker compose ps                      # trạng thái
docker compose logs -f <service>       # xem log
docker compose pull && docker compose up -d --build   # cập nhật code mới (git pull trước)
docker compose restart dashboard-api   # khởi động lại 1 service
docker compose down                    # dừng (GIỮ dữ liệu)
docker compose down -v                 # dừng + XOÁ dữ liệu (chỉ khi đổi schema)
```

Cập nhật phiên bản mới từ git:
```bash
git pull
docker compose up -d --build           # giữ dữ liệu; chỉ down -v khi có đổi bảng DB
```

## 9. Sao lưu dữ liệu

Dữ liệu nằm trong Docker volume `brandradar_brpg` (Postgres) và `brandradar_bres` (Elasticsearch).

```bash
# Backup Postgres
docker compose exec postgres pg_dump -U radar brandradar > backup_$(date +%F).sql
# Restore
cat backup.sql | docker compose exec -T postgres psql -U radar -d brandradar
```

## 10. Lỗi thường gặp

| Triệu chứng | Nguyên nhân / cách xử lý |
|---|---|
| ES container liên tục restart | Chưa đặt `vm.max_map_count=262144` (mục 2) |
| Máy đơ / OOM | Thiếu RAM — bỏ bớt kibana/prometheus/grafana, hoặc giảm `ES_JAVA_OPTS` |
| `/api/rules` 500 sau khi thêm bảng | Chạy lại `analysis` để tạo bảng, hoặc `down -v` để tạo schema mới |
| Cổng bị chiếm | Sửa phần `ports:` trong `docker-compose.yml` |
