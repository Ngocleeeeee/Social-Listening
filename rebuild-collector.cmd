@echo off
cd /d "%~dp0"
docker compose up -d --build collector
docker compose ps
pause
