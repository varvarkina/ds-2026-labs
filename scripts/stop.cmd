@echo off
setlocal

echo Stopping Nginx container...
docker rm -f valuator-nginx 1>nul 2>nul

echo Killing processes on ports 5001 and 5002...
for %%P in (5001 5002) do (
  for /f "tokens=5" %%a in ('netstat -ano ^| findstr :%%P ^| findstr LISTENING') do (
    echo Killing PID %%a port %%P
    taskkill /PID %%a /F 1>nul 2>nul
  )
)

echo Done.
endlocal