# Claude Code sandbox environment for ActualLab projects
# Supports: ActualLab.Fusion, ActualLab.Fusion.Samples, ActualChat
# Includes: .NET 10 SDK, .NET 9 SDK, Node.js 20, Claude Code CLI

FROM mcr.microsoft.com/dotnet/sdk:10.0

# Timezone setup
ARG TZ=Etc/UTC
ENV TZ="$TZ"

# Install Node.js 20
RUN curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y nodejs

# Install dev tools, CLI utilities, Python 3, image tools
RUN apt-get update && apt-get install -y \
    git git-lfs procps sudo fzf zsh man-db unzip gnupg2 \
    gh jq wget curl less ca-certificates \
    python3 python3-pip python3-venv \
    imagemagick \
    ripgrep fd-find vim nano \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

# Install PowerShell (the Microsoft Debian repo is amd64-only for `powershell`)
# TODO: do we need explicit powershell installation? it's already preinstalled
RUN if [ "$(dpkg --print-architecture)" = "amd64" ]; then \
        wget -q https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb && \
        dpkg -i packages-microsoft-prod.deb && \
        rm packages-microsoft-prod.deb && \
        apt-get update && \
        apt-get install -y powershell && \
        apt-get clean && rm -rf /var/lib/apt/lists/*; \
    else \
        echo "Skipping PowerShell install on $ARCH"; \
    fi

# Install Google Cloud CLI
RUN curl -fsSL https://packages.cloud.google.com/apt/doc/apt-key.gpg | gpg --dearmor -o /usr/share/keyrings/cloud.google.gpg && \
    echo "deb [signed-by=/usr/share/keyrings/cloud.google.gpg] https://packages.cloud.google.com/apt cloud-sdk main" | tee /etc/apt/sources.list.d/google-cloud-sdk.list && \
    apt-get update && \
    apt-get install -y google-cloud-cli && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

# Install Python charting and data analysis libraries
RUN pip3 install --break-system-packages \
    matplotlib seaborn plotly pandas numpy pillow kaleido

# Install git-delta for nicer git diffs
RUN ARCH=$(dpkg --print-architecture) && \
    wget "https://github.com/dandavison/delta/releases/download/0.18.2/git-delta_0.18.2_${ARCH}.deb" && \
    dpkg -i "git-delta_0.18.2_${ARCH}.deb" && \
    rm "git-delta_0.18.2_${ARCH}.deb"

# Install .NET 9 SDK (for ActualChat and other .NET 9 projects)
RUN curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 9.0 --install-dir /usr/share/dotnet

# Install .NET wasm-tools workload (needed for Blazor WebAssembly)
RUN dotnet workload install wasm-tools

# Install Playwright npm package globally and browser dependencies
RUN npm install -g playwright && \
    playwright install-deps

# Create non-root user
ARG USERNAME=claude

RUN useradd -m $USERNAME && \
    echo "$USERNAME ALL=(ALL) NOPASSWD:ALL" > /etc/sudoers.d/$USERNAME && \
    chmod 0440 /etc/sudoers.d/$USERNAME

# Setup directories for all projects (mounted at /proj/<project-name>)
RUN mkdir -p /home/$USERNAME/.claude && \
    touch /home/$USERNAME/.claude.json && \
    mkdir -p /proj && \
    chown -R $USERNAME:$USERNAME /home/$USERNAME/.claude /home/$USERNAME/.claude.json /proj

# Configure git to trust all project directories under /proj (mounted projects)
# This avoids "dubious ownership" errors when projects are mounted from Windows
RUN git config --global --add safe.directory /proj/ActualChat && \
    git config --global --add safe.directory /proj/ActualLab.Fusion && \
    git config --global --add safe.directory /proj/ActualLab.Fusion.Samples

# NPM global setup for user
RUN mkdir -p /usr/local/share/npm-global && \
    chown -R $USERNAME:$USERNAME /usr/local/share/npm-global

# Command history persistence
RUN mkdir -p /commandhistory && \
    touch /commandhistory/.bash_history && \
    touch /commandhistory/.zsh_history && \
    chown -R $USERNAME:$USERNAME /commandhistory

ENV HISTFILE=/commandhistory/.bash_history
ENV PROMPT_COMMAND='history -a'

# Setup Zsh
RUN sh -c "$(wget -O- https://github.com/deluan/zsh-in-docker/releases/download/v1.2.0/zsh-in-docker.sh)" -- \
    -u $USERNAME \
    -p git -p fzf \
    -x

ENV SHELL=/bin/zsh

# Switch to user for npm global install
USER $USERNAME
ENV NPM_CONFIG_PREFIX=/usr/local/share/npm-global
ENV PATH=$PATH:/usr/local/share/npm-global/bin

# Pre-download Playwright Chromium browser (~280MB, speeds up first use)
RUN playwright install chromium

# Install Claude Code CLI (pinned version, auto-update disabled)
ENV DISABLE_AUTOUPDATER=1
RUN npm install -g @anthropic-ai/claude-code@2.1.25

# Default working directory (overridden by -w flag in docker run)
WORKDIR /proj

# Default: launch Claude CLI
CMD ["claude", "--dangerously-skip-permissions"]
