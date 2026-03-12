"""
Pytest Configuration and Shared Fixtures for InferenceSystem tests
"""

import sys
from pathlib import Path

import pytest
import soundfile as sf
import torch
import torchaudio
import yaml
import json

# Add src to path for imports
SRC_DIR = Path(__file__).parent.parent / "src"
sys.path.insert(0, str(SRC_DIR))


# =============================================================================
# Monkey-patch torchaudio to avoid torchcodec dependency
# (Same patch as in fastai_inference.py)
# =============================================================================

_original_torchaudio_load = torchaudio.load


def _patched_torchaudio_load(filepath, *args, **kwargs):
    """Wrapper for torchaudio.load that uses soundfile directly"""
    data, samplerate = sf.read(str(filepath), dtype="float32")
    waveform = torch.from_numpy(data.T if data.ndim > 1 else data.reshape(1, -1))
    return waveform, samplerate


torchaudio.load = _patched_torchaudio_load

_original_torchaudio_save = torchaudio.save


def _patched_torchaudio_save(filepath, src, sample_rate, *args, **kwargs):
    """Wrapper for torchaudio.save that uses soundfile directly"""
    audio_data = src.numpy().T if src.ndim > 1 else src.numpy().reshape(-1, 1)
    sf.write(str(filepath), audio_data, sample_rate)


torchaudio.save = _patched_torchaudio_save

# Test data directory
TEST_DATA_DIR = Path(__file__).parent.parent / "tests" / "test_data"

# Reference outputs directory (for parity testing)
REFERENCE_DIR = Path(__file__).parent / "reference_outputs"

# V1 config file
V1_CONFIG_PATH = Path(__file__).parent / "test_config.yaml"


@pytest.fixture
def test_data_dir():
    """Return path to test data directory"""
    assert TEST_DATA_DIR.exists(), f"Test data directory not found: {TEST_DATA_DIR}"
    return TEST_DATA_DIR


@pytest.fixture
def reference_dir():
    """Return path to reference outputs directory"""
    REFERENCE_DIR.mkdir(exist_ok=True)
    return REFERENCE_DIR


@pytest.fixture
def sample_1min_wav(test_data_dir):
    """
    Return path to a 1-minute test WAV file.

    Prefers a WAV that has a matching _audio_reference.pt in reference_outputs/
    so that parity tests can run without regenerating references.
    Falls back to the first available WAV if no match is found.
    """
    wav_files = sorted(list(test_data_dir.glob("*.wav")))
    assert len(wav_files) > 0, "No WAV files found in test_data directory"
    for wav in wav_files:
        ref = REFERENCE_DIR / f"{wav.stem}_audio_reference.pt"
        if ref.exists():
            return str(wav)
    return str(wav_files[0])

@pytest.fixture
def max_segments():
    """Return maximum number of segments to test from the 1-minute WAV file"""
    return 3


@pytest.fixture
def segments_start_s():
    """Return start time in seconds for segments"""
    return 32


@pytest.fixture
def fastai_available():
    """Check if fastai environment is available"""
    try:
        from audio.data import AudioConfig, AudioList, SpectrogramConfig
        from fastai.basic_train import load_learner

        return True
    except ImportError:
        return False


@pytest.fixture
def v1_config():
    """Load v1 inference config from YAML"""
    assert V1_CONFIG_PATH.exists(), f"V1 config not found: {V1_CONFIG_PATH}"
    with open(V1_CONFIG_PATH) as f:
        return yaml.safe_load(f)


@pytest.fixture
def audio_references(reference_dir, sample_1min_wav):
    """Load pre-generated fastai reference outputs, skip if missing."""
    wav_name = Path(sample_1min_wav).stem
    reference_file = reference_dir / f"{wav_name}_audio_reference.pt"
    if not reference_file.exists():
        pytest.skip(
            f"Reference file not found: {reference_file}. "
            "Run test_generate_reference_outputs first."
        )
    return torch.load(reference_file, weights_only=False)


@pytest.fixture
def segment_prediction_references(reference_dir, sample_1min_wav):
    """Load pre-generated fastai segment prediction references, skip if missing."""
    wav_name = Path(sample_1min_wav).stem
    reference_file = reference_dir / f"{wav_name}_segment_preds_reference.json"
    if not reference_file.exists():
        pytest.skip(
            f"Reference file not found: {reference_file}. "
            "Run test_generate_segment_predictions_reference first."
        )
    with open(reference_file) as f:
        return json.load(f)


@pytest.fixture
def file_prediction_references(reference_dir, sample_1min_wav):
    """Load pre-generated fastai file prediction references (JSON), skip if missing."""
    wav_name = Path(sample_1min_wav).stem
    reference_file = reference_dir / f"{wav_name}_file_preds_reference.json"
    if not reference_file.exists():
        pytest.skip(
            f"Reference file not found: {reference_file}. "
            "Run test_generate_file_predictions_reference first."
        )
    with open(reference_file) as f:
        return json.load(f)


def pytest_addoption(parser):
    """Add custom command-line options"""
    parser.addoption(
        "--save-debug",
        action="store_true",
        default=False,
        help="Save debug output (spectrograms, stats) to tests/tmp/",
    )


def pytest_configure(config):
    """Pytest configuration hook"""
    config.addinivalue_line("markers", "parity: marks tests that compare fastai vs model")
    config.addinivalue_line("markers", "slow: marks tests that take a long time")


@pytest.fixture
def debug_dir(request):
    """
    Return debug output directory if --save-debug flag is set, otherwise None.

    Usage:
        pytest --save-debug  # enables debug output to tests/tmp/
        pytest               # no debug output
    """
    if request.config.getoption("--save-debug"):
        debug_dir = Path(__file__).parent / "tmp"
        debug_dir.mkdir(parents=True, exist_ok=True)
        return debug_dir
    return None


@pytest.fixture
def model_dir():
    """Return path to model directory"""
    model_dir = Path(__file__).parent.parent / "model"
    return model_dir


@pytest.fixture
def numerical_tolerance():
    """Return numerical tolerance for inference parity tests"""
    return {"atol": 1e-3, "rtol": 1e-3}


@pytest.fixture
def model_v1(v1_config):
    """Create OrcaHelloSRKWDetectorV1 model instance with default config"""
    from model.inference import OrcaHelloSRKWDetectorV1

    model = OrcaHelloSRKWDetectorV1.from_pretrained("orcasound/orcahello-srkw-detector-v1", config=v1_config)
    model.eval()
    return model
