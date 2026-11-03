"""Strict A2 Lite capture reader for offline POD Go experiments."""
from pathlib import Path
import hashlib
import json
import math
import struct

ROOT = Path(__file__).resolve().parents[1]
CANONICAL = Path(__file__).with_name("a2-lite-config.json")


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate JSON key: " + key)
        result[key] = value
    return result


def finite_f32(value):
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError("A finite float32 number is required")
    try:
        result = struct.pack("<f", value)
    except (OverflowError, struct.error):
        raise ValueError("Number does not fit float32") from None
    if not math.isfinite(struct.unpack("<f", result)[0]):
        raise ValueError("Non-finite float32 number")
    return result


def validate(data):
    if not isinstance(data, dict):
        raise ValueError("NAM must be a JSON object")
    if data.get("architecture") == "SlimmableContainer":
        try:
            models = [sub["model"] for sub in data["config"]["submodels"]]
        except (KeyError, TypeError):
            raise ValueError("Malformed SlimmableContainer") from None
    else:
        models = [data]
    candidates = []
    for index, model in enumerate(models):
        try:
            if model["architecture"] == "WaveNet" and model["config"]["layers"][0]["channels"] == 3:
                candidates.append((index, model))
        except (KeyError, IndexError, TypeError):
            raise ValueError("Malformed submodel") from None
    if len(candidates) != 1:
        raise ValueError("Exactly one 3-channel WaveNet A2 Lite submodel is required")
    index, model = candidates[0]
    rates = [item.get("sample_rate") for item in (data, model) if item.get("sample_rate") is not None]
    if not rates:
        raise ValueError("Explicit 48000 Hz training rate required")
    for item in (data, model):
        rate = item.get("sample_rate")
        if isinstance(rate, bool) or rate not in (None, 48000):
            raise ValueError("This kernel requires 48000 Hz")
    expected = json.loads(CANONICAL.read_bytes())
    config = model["config"]
    if set(config) - set(expected) - {"head_scale"}:
        raise ValueError("Unknown WaveNet configuration fields")
    # Compare every layer/activation/group/FiLM/head setting, not just the
    # channel count and weight count. Those counts alone do not identify A2.
    for key, value in expected.items():
        if key not in config or json.dumps(config[key], sort_keys=True) != json.dumps(value, sort_keys=True):
            raise ValueError("Unsupported A2 Lite configuration: " + key)
    weights = model.get("weights")
    if not isinstance(weights, list) or len(weights) != 1871:
        raise ValueError("A2 Lite requires exactly 1871 weights")
    packed = [finite_f32(value) for value in weights]
    if weights[-1] <= 0:
        raise ValueError("Positive head scale required")
    # NAM Core loads the final weight as the head scale. The informational
    # config field can differ in valid trainer exports; never replace a weight
    # with that field or reject the capture for that difference alone.
    config_scale_matches = None
    if "head_scale" in config:
        config_scale_matches = finite_f32(config["head_scale"]) == packed[-1]
    return weights, dict(submodel_index=index, channels=3, weights=len(weights),
        sample_rate=model.get("sample_rate") or data.get("sample_rate"),
        head_scale=weights[-1], config_head_scale_matches_float32=config_scale_matches,
        canonical_config_sha256=hashlib.sha256(CANONICAL.read_bytes()).hexdigest(),
        weight_stream_sha256=hashlib.sha256(b"".join(packed)).hexdigest(),
        metadata=data.get("metadata") or {})


def read_capture(path):
    path = Path(path).resolve()
    with path.open("rb") as stream:
        original = stream.read(16 * 1024 * 1024 + 1)
    if len(original) > 16 * 1024 * 1024:
        raise ValueError("Capture exceeds laboratory size limit")
    def reject_number(value):
        raise ValueError("Non-finite JSON number: " + value)
    data = json.loads(original, object_pairs_hook=unique_object, parse_constant=reject_number)
    weights, details = validate(data)
    return original, weights, dict(details, source=str(path), source_sha256=hashlib.sha256(original).hexdigest())
