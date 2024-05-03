FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim

# Install python
RUN apt update
RUN apt install -y git wget build-essential python3.11 python3.11-venv python3.11-dev

# Install dependencies for controlnet preprocessors
RUN apt install -y libglib2.0-0 libgl1

# Copy swarm's files into the docker container
COPY . .

# Send the port forward
EXPOSE 7801

# Set the run file to the launch script
ENTRYPOINT ["bash", "launchtools/docker.sh"]
