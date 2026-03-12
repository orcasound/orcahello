FROM ubuntu:22.04

# System deps
RUN apt-get update && apt-get install -y \
        libsm6 \
        libxext6 \
        libxrender-dev \
        libglib2.0-0 \
        ffmpeg \
        curl \
        ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Install uv
RUN curl -LsSf https://astral.sh/uv/install.sh | sh
ENV PATH="/root/.local/bin:$PATH"

# Install Python deps via uv (prod group only — no pytest in prod image)
# UV_PYTHON pins to 3.11 to avoid issues with newer system pythons
WORKDIR /usr/src/install
COPY ./pyproject.toml ./pyproject.toml
COPY ./uv.lock ./uv.lock
ENV UV_PROJECT_ENVIRONMENT=/usr/src/venv
ENV UV_PYTHON=3.11
RUN uv python install 3.11 && uv sync --group prod --no-dev

# Bake HuggingFace model into image cache at build time
ENV HF_HOME=/usr/src/hf_cache
RUN /usr/src/venv/bin/python -c "from huggingface_hub import snapshot_download; snapshot_download('orcasound/orcahello-srkw-detector-v1')"

# At runtime, never hit the network for the model
ENV HF_HUB_OFFLINE=1

# App working directory
WORKDIR /usr/src/app

# model/config.yaml — loaded at runtime via model_config_path: "./model/config.yaml"
COPY ./model/config.yaml ./model/config.yaml

# Source code
COPY ./src/ ./src/

CMD ["/usr/src/venv/bin/python3", "-u", "./src/LiveInferenceOrchestratorV1.py"]
