import gc
import math
import os

import cv2
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np

from model.audio_frontend import load_processed_waveform, featurize_waveform
from model.types import DetectorInferenceConfig


def _render_spectrogram(spectrogram_np, times_np, freqs_np, output_path, figsize=(12.8, 4.8), dpi=100):
    """Render a mel spectrogram array to a PNG file.

    Args:
        spectrogram_np: 2D numpy array (n_mels, n_frames), dB-scaled
        times_np: 1D array of time values (seconds)
        freqs_np: 1D array of frequency values (Hz)
        output_path: path to save PNG
        figsize: figure size in inches (width, height)
        dpi: dots per inch
    """
    fig, ax = plt.subplots(1, 1, figsize=figsize, dpi=dpi)
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
    features, times, freqs = featurize_waveform(waveform, sr, config_dict["spectrogram"])

    spectrogram_np = features.squeeze(0).numpy()
    times_np = times.numpy()
    freqs_np = freqs.numpy()
    return spectrogram_np, times_np, freqs_np


def write_spectrogram(wav_file_path, config):
    """Generate a spectrogram PNG from a WAV file using the model's audio frontend.

    Produces a single, consistently-colored mel spectrogram matching the model's
    audio processing parameters.

    Args:
        wav_file_path: path to WAV file
        config: DetectorInferenceConfig with audio/spectrogram settings

    Returns:
        Path to the output PNG file
    """
    directory_name = os.path.dirname(wav_file_path)
    candidate_name = os.path.basename(wav_file_path)
    candidate_name_without_extension = os.path.splitext(candidate_name)[0]
    spec_output_path = os.path.join(directory_name, candidate_name_without_extension + ".png")

    spectrogram_np, times_np, freqs_np = _compute_mel_for_clip(wav_file_path, config)
    _render_spectrogram(spectrogram_np, times_np, freqs_np, spec_output_path)

    del spectrogram_np, times_np, freqs_np
    gc.collect()

    return spec_output_path


def write_annotations_on_spectrogram(wav_file_path, wav_timestamp, data, spec_output_path, config):
    """Generate an annotated spectrogram highlighting positive detection segments.

    Args:
        wav_file_path: path to WAV file
        wav_timestamp: timestamp string to overlay
        data: dict with 'local_predictions' and 'local_confidences' lists
        spec_output_path: path to save annotated PNG
        config: DetectorInferenceConfig with audio/spectrogram settings
    """
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
