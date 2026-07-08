# BrandRadar trên Kubernetes

Triển khai toàn hệ BrandRadar lên Kubernetes — thể hiện khả năng **mở rộng theo chiều ngang** (competing
consumers + HPA) đúng yêu cầu "hệ thống hiệu năng cao, mở rộng tốt".

## Thành phần
- `00-namespace` · `01-config` (ConfigMap + Secret)
- `02-infra` — postgres, rabbitmq, kafka, elasticsearch, redis (demo-grade, 1 replica)
- `03-apps` — collector, **analysis (2 replicas)**, sentiment-nlp, **dashboard-api (2 replicas)**, frontend
- `04-autoscaling` — HPA: analysis 2→8, dashboard 2→6 theo CPU 70%
- `05-ingress` — `/` → frontend, `/api` & `/hubs` → dashboard-api

## Chạy (Docker Desktop Kubernetes / Minikube / Kind)

1) Build image (một lần) rồi để cluster dùng ảnh local:
```bash
cd SocialListening
docker compose build          # tạo brandradar-* images
# Minikube: minikube image load brandradar-dashboard-api:latest (lặp cho từng ảnh)
# Kind:     kind load docker-image brandradar-dashboard-api:latest (lặp cho từng ảnh)
# Docker Desktop K8s: dùng chung Docker engine nên không cần load
```

2) Áp dụng manifests:
```bash
kubectl apply -k k8s/
kubectl -n brandradar get pods -w
```

3) Truy cập:
```bash
# qua ingress (thêm 127.0.0.1 brandradar.local vào hosts) — cần ingress-nginx
# hoặc port-forward nhanh:
kubectl -n brandradar port-forward svc/frontend 3000:80
kubectl -n brandradar port-forward svc/dashboard-api 8090:8080
```

4) Autoscaling (cần metrics-server):
```bash
kubectl -n brandradar get hpa
# tạo tải → xem analysis/dashboard tự tăng pod
```

## Điểm nhấn khi phỏng vấn
- **Worker `analysis` scale ngang** (competing consumers trên RabbitMQ) + **HPA theo CPU** → xử lý tải tăng đột biến.
- **API stateless** → nhiều replica sau Service/Ingress; **readiness/liveness probe** cho rolling update an toàn.
- Cấu hình tách khỏi image (**ConfigMap/Secret**), bí mật không hardcode.
- Infra để ở dạng demo; production sẽ dùng **managed services / operators** (RDS, MSK, ECK, Bitnami charts).
