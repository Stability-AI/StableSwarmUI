FROM mcr.microsoft.com/dotnet/sdk:7.0-bookworm-slim

# install prereqs
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
  vim \
  ;

# env vars from launch-linux.sh
ENV ASPNETCORE_ENVIRONMENT="Production"
ENV ASPNETCORE_URLS="http://*:7801"

# clone repo and modify for running inside container
WORKDIR /code
RUN git clone https://github.com/Stability-AI/StableSwarmUI
RUN sed -i 's/webinstall/none/g' /code/StableSwarmUI/src/Core/Settings.cs
RUN sed -i '/<PropertyGroup>/a \
    <ContainerBaseImage>moneymarathon/container-images:dotnet</ContainerBaseImage>' \
    /code/StableSwarmUI/src/StableSwarmUI.csproj

# copy and build
WORKDIR /app
RUN cp /code/StableSwarmUI/src/StableSwarmUI.csproj .
RUN dotnet restore --use-current-runtime
RUN mkdir src
RUN cp -r /code/StableSwarmUI/src/* /app/src/
RUN dotnet publish -c Release --property:PublishDir=/app --use-current-runtime --self-contained false --no-restore

EXPOSE 7801
ENTRYPOINT [ "dotnet" ]
