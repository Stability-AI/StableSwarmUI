@echo off
setlocal enabledelayedexpansion

set CUDA_VISIBLE_DEVICES=%1
set COMMANDLINE_ARGS="%4"

cd %2

set PYTHONUNBUFFERED=true

set "argument=%~4"
set "argument=!argument: =^ !"

if "%5" neq "py" (
    call %3 %argument%
) ELSE (
    call python %3 %argument%
)
