FROM mcr.microsoft.com/dotnet/sdk:7.0-bookworm-slim

# Install python
RUN apt update
RUN apt install -y git wget build-essential python3.11 python3.11-venv

# Copy swarm's files into the docker container
COPY . .

# Send the port forward
EXPOSE 7801

# Set the run file to the launch script
ENTRYPOINT ["bash", "launchtools/docker.sh"]
