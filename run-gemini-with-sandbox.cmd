@echo off
setlocal
cd /d %~dp0

REM Use .gemini\build.cmd to build this image.
REM Currently we use this Docker sandbox because Gemini CLI does not handle Windows command execution
REM very well.
gemini -s --sandbox-image sep-csv-source-generator-gemini-sandbox
