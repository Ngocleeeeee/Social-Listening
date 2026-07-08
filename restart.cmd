@echo off
cd /d "%~dp0"
echo === Dung va xoa du lieu cu ===
docker compose down -v
echo === Build va chay lai ===
docker compose up -d --build
echo === Trang thai ===
docker compose ps
echo.
echo Xong. Doi 1-2 phut cho du lieu vao, roi mo http://localhost:3000
pause
