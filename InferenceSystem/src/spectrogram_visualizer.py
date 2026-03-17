import gc
import os
import warnings

import matplotlib

matplotlib.use("Agg")
import matplotlib.patheffects
import matplotlib.pyplot as plt
import numpy as np
import soundfile as sf
from model.audio_frontend import featurize_waveform, load_processed_waveform
from model.types import DetectorInferenceConfig

# Fixed image height matching mel_n_filters for 1:1 pixel-per-bin rendering.
_VIZ_IMAGE_HEIGHT = 480
_VIZ_IMAGE_WIDTH = 1280
_FREQ_LABEL_FONT_SIZE = 8
_COLORMAP = "Blues"


def _build_viz_config(native_sr):
    """Build a visualization-optimized spectrogram config for the given native sample rate.

    Uses the full audible bandwidth at native resolution (no resampling),
    with mel bins matching the fixed image height for 1:1 pixel rendering.
    """
    return DetectorInferenceConfig.from_dict(
        {
            "audio": {
                "downmix_mono": True,
                "resample_rate": native_sr,
                "normalize": True,
            },
            "spectrogram": {
                "sample_rate": native_sr,
                "n_fft": 4096,
                "hop_length": 1024,
                "mel_n_filters": _VIZ_IMAGE_HEIGHT,
                "mel_f_min": 20.0,
                "mel_f_max": native_sr // 2,
                "mel_f_pad": 0,
                "convert_to_db": True,
                "top_db": 100,
            },
        }
    )


def _freq_label(hz):
    """Format a frequency value as a compact human-readable string."""
    if hz >= 1000:
        khz = hz / 1000
        return f"{khz:.0f}k" if khz == int(khz) else f"{khz:.1f}k"
    return f"{hz:.0f}"


def _pick_freq_ticks(f_min, f_max):
    """Choose ~5-8 log-spaced tick positions between f_min and f_max."""
    candidates = [
        40,
        250,
        500,
        750,
        1000,
        1500,
        2000,
        3000,
        5000,
        7500,
        10000,
        15000,
        20000,
        30000,
        48000,
    ]
    ticks = [f for f in candidates if f_min <= f <= f_max]
    if not ticks:
        ticks = np.geomspace(max(f_min, 1), f_max, num=6).tolist()
    return ticks


def _render_spectrogram(
    spectrogram_np,
    times_np,
    freqs_np,
    output_path,
    width_px=_VIZ_IMAGE_WIDTH,
    height_px=_VIZ_IMAGE_HEIGHT,
    dpi=100,
):
    """Render a mel spectrogram array to a PNG file.

    Args:
        spectrogram_np: 2D numpy array (n_mels, n_frames), dB-scaled
        times_np: 1D array of time values (seconds)
        freqs_np: 1D array of frequency values (Hz)
        output_path: path to save PNG
        width_px: image width in pixels
        height_px: image height in pixels
        dpi: dots per inch
    """
    fig, ax = plt.subplots(1, 1, figsize=(width_px / dpi, height_px / dpi), dpi=dpi)
    ax.axis("off")
    ax.set_position([0.0, 0.0, 1.0, 1.0])

    bin_indices = np.arange(
        len(freqs_np)
    )  # freqs log-spaced, each bin given equal height
    ax.pcolormesh(
        times_np,
        bin_indices,
        spectrogram_np,
        shading="auto",
        cmap=_COLORMAP,
    )

    f_min, f_max = float(freqs_np[0]), float(freqs_np[-1])
    ticks = _pick_freq_ticks(f_min, f_max)
    x_pos = times_np[0] + (times_np[-1] - times_np[0]) * 0.005

    for freq in ticks:
        bin_idx = float(np.searchsorted(freqs_np, freq))
        ax.text(
            x_pos,
            bin_idx,
            _freq_label(freq),
            color="white",
            fontsize=_FREQ_LABEL_FONT_SIZE,
            fontweight="bold",
            va="center",
            ha="left",
            path_effects=[
                matplotlib.patheffects.Stroke(linewidth=2, foreground="black"),
                matplotlib.patheffects.Normal(),
            ],
        )

    fig.savefig(output_path, bbox_inches=None, pad_inches=0)
    plt.close(fig)


def _compute_mel_for_clip(wav_file_path, config):
    """Load audio and compute mel spectrogram using audio_frontend.

    Args:
        wav_file_path: path to WAV file
        config: DetectorInferenceConfig

    Returns:
        (spectrogram_np, times_np, freqs_np) - 2D mel spectrogram in dB,
        time axis in seconds, frequency axis in Hz
    """
    config_dict = config.as_dict()
    waveform, sr = load_processed_waveform(wav_file_path, config_dict["audio"])
    with warnings.catch_warnings():
        # High mel_n_filters relative to n_fft is intentional for 1:1 pixel rendering;
        # empty top-end mel bins simply render as a low-intensity color (no energy).
        warnings.filterwarnings("ignore", message="At least one mel filterbank")
        features, times, freqs = featurize_waveform(
            waveform, sr, config_dict["spectrogram"]
        )

    spectrogram_np = features.squeeze(0).numpy()
    times_np = times.numpy()
    freqs_np = freqs.numpy()
    return spectrogram_np, times_np, freqs_np


def write_spectrogram(wav_file_path):
    """Generate a spectrogram PNG from a WAV file.

    Uses the native sample rate and visualization-optimized mel parameters
    for clear human-readable spectrogram output.

    Args:
        wav_file_path: path to WAV file

    Returns:
        Path to the output PNG file
    """
    directory_name = os.path.dirname(wav_file_path)
    candidate_name = os.path.basename(wav_file_path)
    candidate_name_without_extension = os.path.splitext(candidate_name)[0]
    spec_output_path = os.path.join(
        directory_name, candidate_name_without_extension + ".png"
    )

    native_sr = sf.info(wav_file_path).samplerate
    config = _build_viz_config(native_sr)

    spectrogram_np, times_np, freqs_np = _compute_mel_for_clip(wav_file_path, config)
    _render_spectrogram(spectrogram_np, times_np, freqs_np, spec_output_path)

    del spectrogram_np, times_np, freqs_np
    gc.collect()

    return spec_output_path
