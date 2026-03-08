@echo off
setlocal
cd /d "%~dp0\.."

echo Starting Valuator instances...
start "Valuator-5001" cmd /k "dotnet run --project Valuator --urls http://0.0.0.0:5001"
start "Valuator-5002" cmd /k "dotnet run --project Valuator --urls http://0.0.0.0:5002"

echo Starting Nginx (Docker)...
docker rm -f valuator-nginx 1>nul 2>nul
docker run --name valuator-nginx -d -p 8080:8080 ^
  -v "%cd%\nginx\conf\nginx.conf:/etc/nginx/nginx.conf:ro" ^
  nginx:alpine

echo Done. Open http://localhost:8080/
echo To view logs: docker logs -f valuator-nginx
endlocal