FROM mcr.microsoft.com/dotnet/sdk:7.0-bookworm-slim
#python:slim-bookworm

WORKDIR /tmp

RUN apt update
RUN apt install -y \
  git \
  wget \
  build-essential \
  python3.11 \
  python3.11-distutils \
  python3.11-lib2to3 \
  python3.11-venv \
  python3-pip \
  python3-dev \
  python3.11-dev \
  ;

WORKDIR /code
RUN git clone https://github.com/Stability-AI/StableSwarmUI
WORKDIR /code/StableSwarmUI
RUN ls -lah && pwd
RUN ./launch-linux.sh
EXPOSE 7801
