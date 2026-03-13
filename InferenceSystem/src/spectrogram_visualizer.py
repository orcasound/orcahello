import gc
import math
import os
import warnings

import cv2
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np
import soundfile as sf

from model.audio_frontend import load_processed_waveform, featurize_waveform
from model.types import DetectorInferenceConfig

# Fixed image height matching mel_n_filters for 1:1 pixel-per-bin rendering.
_VIZ_IMAGE_HEIGHT = 960
_VIZ_IMAGE_WIDTH = 1280


def _build_viz_config(native_sr):
    """Build a visualization-optimized spectrogram config for the given native sample rate.

    Uses the full audible bandwidth at native resolution (no resampling),
    with 960 mel bins matching the fixed image height for 1:1 pixel rendering.
    """
    return DetectorInferenceConfig.from_dict({
        "audio": {
            "downmix_mono": True,
            "resample_rate": native_sr,
            "normalize": True,
        },
        "spectrogram": {
            "sample_rate": native_sr,
            "n_fft": 8192,
            "hop_length": 2048,
            "mel_n_filters": _VIZ_IMAGE_HEIGHT,
            "mel_f_min": 20.0,
            "mel_f_max": native_sr // 2,
            "mel_f_pad": 0,
            "convert_to_db": True,
            "top_db": 100,
        },
    })


def _render_spectrogram(spectrogram_np, times_np, freqs_np, output_path,
                        width_px=_VIZ_IMAGE_WIDTH, height_px=_VIZ_IMAGE_HEIGHT, dpi=100):
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
    ax.axis('off')
    ax.set_position([0., 0., 1., 1.])

    ax.pcolormesh(
        times_np, freqs_np, spectrogram_np,
        shading='auto', cmap='magma',
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
        # empty top-end mel bins simply render as black (no energy).
        warnings.filterwarnings("ignore", message="At least one mel filterbank")
        features, times, freqs = featurize_waveform(waveform, sr, config_dict["spectrogram"])

    spectrogram_np = features.squeeze(0).numpy()
    times_np = times.numpy()
    freqs_np = freqs.numpy()
    return spectrogram_np, times_np, freqs_np


def write_spectrogram(wav_file_path):
    """Generate a spectrogram PNG from a WAV file.

    Uses the native sample rate and visualization-optimized mel parameters
    (960 mel bins, 20 Hz–Nyquist, n_fft=4096) for clear human-readable output.

    Args:
        wav_file_path: path to WAV file

    Returns:
        Path to the output PNG file
    """
    directory_name = os.path.dirname(wav_file_path)
    candidate_name = os.path.basename(wav_file_path)
    candidate_name_without_extension = os.path.splitext(candidate_name)[0]
    spec_output_path = os.path.join(directory_name, candidate_name_without_extension + ".png")

    native_sr = sf.info(wav_file_path).samplerate
    config = _build_viz_config(native_sr)

    spectrogram_np, times_np, freqs_np = _compute_mel_for_clip(wav_file_path, config)
    _render_spectrogram(spectrogram_np, times_np, freqs_np, spec_output_path)

    del spectrogram_np, times_np, freqs_np
    gc.collect()

    return spec_output_path


def write_annotations_on_spectrogram(wav_file_path, wav_timestamp, data, spec_output_path):
    """Generate an annotated spectrogram highlighting positive detection segments.

    Args:
        wav_file_path: path to WAV file
        wav_timestamp: timestamp string to overlay
        data: dict with 'local_predictions' and 'local_confidences' lists
        spec_output_path: path to save annotated PNG
    """
    native_sr = sf.info(wav_file_path).samplerate
    config = _build_viz_config(native_sr)

    spectrogram_np, times_np, freqs_np = _compute_mel_for_clip(wav_file_path, config)
    _render_spectrogram(spectrogram_np, times_np, freqs_np, spec_output_path)

    image = cv2.imread(spec_output_path)
    img_width = image.shape[1]

    local_predictions = data["local_predictions"]
    local_confidences = data["local_confidences"]
    num_predictions = len(local_predictions)

    annotation_width = math.floor(img_width / num_predictions)
    font = cv2.FONT_HERSHEY_SIMPLEX

    for i in range(num_predictions):
        if local_predictions[i] == 1:
            image = cv2.rectangle(image, (i * annotation_width, 20), ((i + 1) * annotation_width, image.shape[0] - 20), (255, 255, 255), 2)
            image = cv2.putText(image, str(local_confidences[i]), (i * annotation_width + 5, image.shape[0] // 2), font, 0.5, (255, 255, 255), 1, cv2.LINE_AA, False)

    image = cv2.putText(image, str(wav_timestamp), (0, 20), font, 0.5, (255, 255, 255), 2, cv2.LINE_AA, False)
    cv2.imwrite(spec_output_path, image)

    del spectrogram_np, times_np, freqs_np, image
    gc.collect()
