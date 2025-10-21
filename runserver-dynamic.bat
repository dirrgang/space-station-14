@echo off
dotnet run --project Content.Server -- ^
    --cvar config.preset_development=false ^
    --cvar config.presets=dynamic
pause
