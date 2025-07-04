FROM us-docker.pkg.dev/gemini-code-dev/gemini-cli/sandbox:0.1.9

USER root

RUN curl -sSL https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -o /tmp/packages-microsoft-prod.deb && \
    dpkg -i /tmp/packages-microsoft-prod.deb && \
    rm /tmp/packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y dotnet-sdk-9.0

# From https://github.com/dotnet/dotnet-docker/blob/a846cb4f827603f38366cce4714ee50851412430/src/sdk/9.0/bookworm-slim/amd64/Dockerfile#L17

ENV \
    # Do not generate certificate
    DOTNET_GENERATE_ASPNET_CERTIFICATE=false \
    # Do not show first run text
    DOTNET_NOLOGO=true \
    # Enable correct mode for dotnet watch (only mode supported in a container)
    DOTNET_USE_POLLING_FILE_WATCHER=true

USER node

# Trigger first run experience by running arbitrary cmd
RUN dotnet help

CMD ["gemini"]
