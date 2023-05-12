@echo off

set CUDA_VISIBLE_DEVICES=%1
set COMMANDLINE_ARGS="%4"

cd %2

call %3
