@echo off
setlocal

cd /d %~dp0

REM

REM While ideally we would use the support for building a custom docker image built into the Gemini
REM CLI, it currently depends on having a source checkout of the Gemini CLI source repo.
REM So for now we build the image manually.
docker build -t sep-csv-source-generator-gemini-sandbox . -f sandbox.Dockerfile
