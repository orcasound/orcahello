#!/usr/bin/env python3
"""
Extract weights from fastai learner to PyTorch checkpoint.

This script runs in the inference-venv environment (which has fastai).
It extracts the ResNet50 weights from model.pkl and saves them in a format
that loads directly into OrcaHelloSRKWDetectorV1.

Usage:
    cd InferenceSystem
    source inference-venv/bin/activate
    python scripts/extract_fastai_weights.py

Output:
    model/model_v1.pt - PyTorch state_dict checkpoint
"""

import sys
from pathlib import Path
import urllib.request
import zipfile
import tempfile

import torch

# Monkey-patch torch.load to use weights_only=False for compatibility with fastai models
# PyTorch 2.6+ changed the default to weights_only=True for security, but fastai models
# require weights_only=False to load functools.partial and other objects
_original_torch_load = torch.load


def _patched_torch_load(*args, **kwargs):
    """Wrapper for torch.load that defaults to weights_only=False for fastai compatibility"""
    if 'weights_only' not in kwargs:
        kwargs['weights_only'] = False
    return _original_torch_load(*args, **kwargs)


torch.load = _patched_torch_load

# Add src to path
SCRIPT_DIR = Path(__file__).parent
ROOT_DIR = SCRIPT_DIR.parent
sys.path.insert(0, str(ROOT_DIR / "src"))

# Model download URL
FASTAI_MODEL_URL = "https://trainedproductionmodels.blob.core.windows.net/dnnmodel/11-15-20.FastAI.R1-12.zip"


def download_fastai_model(model_dir: Path):
    """
    Download and extract the FastAI model from Azure blob storage.
    
    Args:
        model_dir: Directory to extract the model into
    """
    model_pkl_path = model_dir / "model.pkl"
    
    if model_pkl_path.exists():
        print(f"Model already exists at {model_pkl_path}")
        return
    
    print("=== Downloading FastAI Model ===")
    print(f"URL: {FASTAI_MODEL_URL}")
    model_dir.mkdir(parents=True, exist_ok=True)
    
    with tempfile.NamedTemporaryFile(suffix=".zip", delete=False) as tmp_file:
        tmp_path = Path(tmp_file.name)
        
        try:
            urllib.request.urlretrieve(FASTAI_MODEL_URL, tmp_path)
            print("Download complete. Extracting...")
            
            with zipfile.ZipFile(tmp_path, 'r') as zip_ref:
                zip_ref.extractall(model_dir)
            
            if not model_pkl_path.exists():
                raise FileNotFoundError(f"model.pkl not found after extraction at {model_pkl_path}")
            
            print(f"Model ready at: {model_pkl_path}")
            print(f"Model size: {model_pkl_path.stat().st_size / 1024 / 1024:.2f} MB")
            
        finally:
            if tmp_path.exists():
                tmp_path.unlink()


def inspect_fastai_model(learner_path: Path, learner_name: str = "model.pkl"):
    """Inspect fastai model structure and print key information."""
    from fastai.basic_train import load_learner

    print(f"Loading learner from {learner_path / learner_name}...")
    learner = load_learner(str(learner_path), learner_name)
    model = learner.model

    print("\n=== Model Architecture ===")
    print(model)

    print("\n=== State Dict Keys ===")
    state_dict = model.state_dict()
    for key in state_dict.keys():
        print(f"  {key}: {state_dict[key].shape}")

    print("\n=== Summary ===")
    print(f"Total parameters: {sum(p.numel() for p in model.parameters()):,}")
    print(f"State dict keys: {len(state_dict)}")

    if hasattr(learner.data, 'classes'):
        print(f"Classes: {learner.data.classes}")

    return learner, model, state_dict


def convert_fastai_to_pytorch(fastai_state_dict: dict) -> dict:
    """
    Convert fastai state_dict keys to match OrcaHelloSRKWDetectorV1.

    FastAI model structure:
    - Sequential[0] = backbone (Conv2d, BatchNorm, ReLU, MaxPool, layer1-4)
    - Sequential[1] = head (pooling, bn, dropout, linear layers)

    FastAI backbone uses numeric indices within a Sequential:
    - 0.0.* -> conv1
    - 0.1.* -> bn1
    - 0.2 -> relu (no params)
    - 0.3 -> maxpool (no params)
    - 0.4.* -> layer1
    - 0.5.* -> layer2
    - 0.6.* -> layer3
    - 0.7.* -> layer4

    FastAI head structure (within Sequential[1]):
    - 1.0 -> AdaptiveConcatPool2d (no params)
    - 1.1 -> Flatten (no params)
    - 1.2 -> BatchNorm1d(4096)    -> model.fc.1.*
    - 1.3 -> Dropout (no params)
    - 1.4 -> Linear(4096, 512)    -> model.fc.3.*
    - 1.5 -> ReLU (no params)
    - 1.6 -> BatchNorm1d(512)     -> model.fc.5.*
    - 1.7 -> Dropout (no params)
    - 1.8 -> Linear(512, 2)       -> model.fc.7.*

    OrcaHelloSRKWDetectorV1 has:
    - model.conv1, model.bn1, model.layer1-4 (backbone)
    - model.fc = Sequential[Flatten, BN(4096), Dropout, Linear, ReLU, BN(512), Dropout, Linear]
    """
    new_state_dict = {}

    # Mapping from fastai numeric indices to torchvision layer names
    backbone_map = {
        "0.0.": "model.conv1.",  # First conv layer
        "0.1.": "model.bn1.",    # First batch norm
        "0.4.": "model.layer1.", # layer1
        "0.5.": "model.layer2.", # layer2
        "0.6.": "model.layer3.", # layer3
        "0.7.": "model.layer4.", # layer4
    }

    # Mapping for head layers
    # FastAI indices -> OrcaHelloSRKWDetectorV1 fc Sequential indices
    # fastai 1.2.* (BN 4096) -> model.fc.1.*
    # fastai 1.4.* (Linear 4096->512) -> model.fc.3.*
    # fastai 1.6.* (BN 512) -> model.fc.5.*
    # fastai 1.8.* (Linear 512->2) -> model.fc.7.*
    head_map = {
        "1.2.": "model.fc.1.",  # BatchNorm1d(4096)
        "1.4.": "model.fc.3.",  # Linear(4096, 512)
        "1.6.": "model.fc.5.",  # BatchNorm1d(512)
        "1.8.": "model.fc.7.",  # Linear(512, 2)
    }

    for key, value in fastai_state_dict.items():
        new_key = None

        if key.startswith("0."):
            # Backbone layers
            for fastai_prefix, pytorch_prefix in backbone_map.items():
                if key.startswith(fastai_prefix):
                    new_key = key.replace(fastai_prefix, pytorch_prefix, 1)
                    break

            if new_key is None:
                # Check if it's a layer with params we need to skip (relu, maxpool)
                if key.startswith("0.2.") or key.startswith("0.3."):
                    continue  # No params expected
                else:
                    print(f"  Warning: Unhandled backbone key: {key}")
                    continue

        elif key.startswith("1."):
            # Head layers
            for fastai_prefix, pytorch_prefix in head_map.items():
                if key.startswith(fastai_prefix):
                    new_key = key.replace(fastai_prefix, pytorch_prefix, 1)
                    break

            if new_key is None:
                # Skip non-parameterized layers (pooling, flatten, dropout, relu)
                if any(key.startswith(f"1.{i}.") for i in [0, 1, 3, 5, 7]):
                    continue
                else:
                    print(f"  Warning: Unhandled head key: {key}")
                    continue
        else:
            print(f"  Warning: Unexpected key format: {key}")
            continue

        if new_key is not None:
            new_state_dict[new_key] = value

    return new_state_dict


def extract_weights(
    learner_path: Path,
    output_path: Path,
    learner_name: str = "model.pkl"
):
    """
    Extract weights from fastai learner and save as PyTorch checkpoint.

    Args:
        learner_path: Directory containing model.pkl
        output_path: Path for output .pt file
        learner_name: Name of learner file
    """
    import torch

    # Inspect the model first
    learner, model, fastai_state_dict = inspect_fastai_model(learner_path, learner_name)

    print("\n=== Converting Keys ===")
    pytorch_state_dict = convert_fastai_to_pytorch(fastai_state_dict)

    print(f"\nConverted {len(fastai_state_dict)} fastai keys to {len(pytorch_state_dict)} pytorch keys")
    print("\nPyTorch state dict keys:")
    for key in sorted(pytorch_state_dict.keys()):
        print(f"  {key}: {pytorch_state_dict[key].shape}")

    # Verify the conversion by loading into OrcaHelloSRKWDetectorV1
    print("\n=== Verifying Load ===")
    # Import using direct path since we're in scripts/
    sys.path.insert(0, str(ROOT_DIR / "src"))
    from model.inference import OrcaHelloSRKWDetectorV1
    from model.types import DetectorInferenceConfig

    config = DetectorInferenceConfig().as_dict()
    test_model = OrcaHelloSRKWDetectorV1(config)
    expected_keys = set(test_model.state_dict().keys())
    converted_keys = set(pytorch_state_dict.keys())

    missing_keys = expected_keys - converted_keys
    extra_keys = converted_keys - expected_keys

    if missing_keys:
        print(f"Missing keys: {missing_keys}")
    if extra_keys:
        print(f"Extra keys: {extra_keys}")

    if missing_keys or extra_keys:
        print("\nKey mismatch detected. Listing expected keys:")
        for key in sorted(expected_keys):
            shape = test_model.state_dict()[key].shape
            print(f"  {key}: {shape}")
        raise ValueError("State dict key mismatch - conversion needs adjustment")

    # Load and verify shapes match
    test_model.load_state_dict(pytorch_state_dict)
    print("State dict loaded successfully!")

    # Save checkpoint
    print("\n=== Saving Checkpoint ===")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    torch.save(pytorch_state_dict, output_path)
    print(f"Saved to: {output_path}")
    print(f"File size: {output_path.stat().st_size / 1024 / 1024:.2f} MB")


def main():
    learner_path = ROOT_DIR / "model"
    output_path = ROOT_DIR / "model" / "model_v1.pt"

    # Download model if it doesn't exist
    download_fastai_model(learner_path)

    # Verify model.pkl exists
    if not (learner_path / "model.pkl").exists():
        print(f"Error: model.pkl not found at {learner_path}")
        sys.exit(1)

    extract_weights(learner_path, output_path)
    print("\nDone!")


if __name__ == "__main__":
    main()
