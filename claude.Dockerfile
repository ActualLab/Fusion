# Claude Code sandbox environment for ActualLab.Fusion
# Includes: .NET 10 SDK, Node.js 20, Claude Code CLI

FROM mcr.microsoft.com/dotnet/sdk:10.0

# Timezone setup
ARG TZ=Etc/UTC
ENV TZ="$TZ"

# Install Node.js 20
RUN curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y nodejs

# Install dev tools, CLI utilities, Python 3, image tools
RUN apt-get update && apt-get install -y \
    git procps sudo fzf zsh man-db unzip gnupg2 \
    gh jq wget curl less ca-certificates \
    python3 python3-pip python3-venv \
    imagemagick \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

# Install Python charting and data analysis libraries
RUN pip3 install --break-system-packages \
    matplotlib seaborn plotly pandas numpy pillow kaleido

# Install git-delta for nicer git diffs
RUN ARCH=$(dpkg --print-architecture) && \
    wget "https://github.com/dandavison/delta/releases/download/0.18.2/git-delta_0.18.2_${ARCH}.deb" && \
    dpkg -i "git-delta_0.18.2_${ARCH}.deb" && \
    rm "git-delta_0.18.2_${ARCH}.deb"

# Install .NET wasm-tools workload (needed for Blazor WebAssembly)
RUN dotnet workload install wasm-tools

# Create non-root user
ARG USERNAME=claude

RUN useradd -m $USERNAME && \
    echo "$USERNAME ALL=(ALL) NOPASSWD:ALL" > /etc/sudoers.d/$USERNAME && \
    chmod 0440 /etc/sudoers.d/$USERNAME

# Setup directories
RUN mkdir -p /home/$USERNAME/.claude && \
    touch /home/$USERNAME/.claude.json && \
    mkdir -p /project && \
    chown -R $USERNAME:$USERNAME /home/$USERNAME/.claude /home/$USERNAME/.claude.json /project

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

# Install Claude Code CLI
ENV CLAUDE_INSTALL_METHOD=npm
RUN npm install -g @anthropic-ai/claude-code

# Working directory is the mounted project
WORKDIR /project

# Default: launch Claude CLI
CMD ["claude", "--dangerously-skip-permissions"]
