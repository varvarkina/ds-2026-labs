@echo off
setlocal
cd /d "%~dp0\.."

echo Building project...
dotnet build
if errorlevel 1 exit /b 1

echo Starting Valuator instances...
start "Valuator-5001" cmd /c "dotnet run --no-build --project Valuator --urls http://0.0.0.0:5001"
start "Valuator-5002" cmd /c "dotnet run --no-build --project Valuator --urls http://0.0.0.0:5002"

echo Starting RankCalculator instances...
start "RankCalculator-1" cmd /c "dotnet run --no-build --project RankCalculator -- RankCalculator-1"
start "RankCalculator-2" cmd /c "dotnet run --no-build --project RankCalculator -- RankCalculator-2"

echo Starting Nginx...
docker rm -f valuator-nginx 1>nul 2>nul
docker run --name valuator-nginx -d -p 8080:8080 ^
  -v "%cd%\nginx\conf\nginx.conf:/etc/nginx/nginx.conf:ro" ^
  nginx:alpine

echo Done. Open http://localhost:8080/
echo RabbitMQ UI: http://localhost:15672/
pause
endlocal