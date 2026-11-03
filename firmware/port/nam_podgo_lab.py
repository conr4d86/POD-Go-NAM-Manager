"""First POD Go NAM A2 Lite port components, for offline simulation only.

Uses the pinned GPL-3.0-or-later hx-stomp-nam-lite tools without editing them.
This module neither patches a firmware nor imports any USB or updater API.
The code/data addresses used by the simulator are NOT approved physical placement.
"""
from pathlib import Path
from collections import defaultdict
import ast
import hashlib
import json
import struct
import sys

ROOT = Path(__file__).resolve().parents[1]
UPSTREAM = Path(__file__).resolve().parents[2] / "third_party/hx-stomp-nam-lite"
REVISION = "2e801641df4a083c29ee13c5718c047d336ef692"
STOCK_SHA256 = "9ac67fdd55f8b8feb8c21f231eaafb5c6eb7883a8218430c9c4a23567c2f34d1"
REPLACEMENT_MODEL = "HD2_AmpLine6Clarity"
PROTECTED_MODEL = "HD2_AmpLine6Badonk"
PROTECTED_MODELS = (PROTECTED_MODEL, "HD2_PreampLine6Badonk")
POD_IR_FLAGS = 0x26758E
POD_IR_BANK = 0x26760E
POD_IR_TOP = POD_IR_BANK + 2047
WRAPPER_PM = 0xC4E80
WRAPPER_DM = 0xE4800
NATIVE_FRAME = 32
KERNEL_FRAME = 16

def sha(data):
    return hashlib.sha256(data).hexdigest()

def checked_upstream():
    manifest = json.loads((UPSTREAM / "SOURCE-REVIEW.json").read_text())
    assert manifest["revision"] == REVISION
    for row in manifest["files"]:
        assert sha((UPSTREAM / row["path"]).read_bytes()) == row["sha256"], row["path"]
    return manifest

def upstream_modules():
    checked_upstream()
    sys.path.insert(0, str(UPSTREAM / "tools/sharc"))
    import nam_lite
    import nam_multi
    import nam_pack
    import sasm
    import ssim
    # Port the actual IR bank geometry. Upstream globals are process-local;
    # the reviewed sources on disk remain unchanged.
    nam_multi.IR_FLAG, nam_multi.IR_TOP = POD_IR_FLAGS, POD_IR_TOP
    # One amp block is the first POD Go target. The upstream allocator and
    # kernel use NSLOTS; enable a one-slot layout with the two-slot near/far split.
    nam_multi.FAR_D = {**nam_multi.FAR_D, 1: 41}
    return nam_lite, nam_multi, nam_pack, sasm, ssim

def frame32_wrapper():
    # I1/I2/I3 are caller-clobbered by the inner kernel, so each half reloads
    # them from the entry values. Never advance the first call's returned I1.
    # Disable SIMD before saving index registers; its effect has one delay slot.
    return f"""
.section dm {WRAPPER_DM:#x}
.word 0, 0, 0, 0
.section pm {WRAPPER_PM:#x}
POD_NAM_FRAME32:
    DM({WRAPPER_DM:#x}) = MODE1;
    BIT CLR MODE1 0xE00000;
    NOP;
    DM({WRAPPER_DM+1:#x}) = I1;
    DM({WRAPPER_DM+2:#x}) = I2;
    DM({WRAPPER_DM+3:#x}) = I3;
    CALL CUSTOM_AMP_ENTRY;
    I1 = DM({WRAPPER_DM+1:#x});
    I2 = DM({WRAPPER_DM+2:#x});
    I3 = DM({WRAPPER_DM+3:#x});
    MODIFY(I1, 16);
    MODIFY(I2, 16);
    CALL CUSTOM_AMP_ENTRY;
    I1 = DM({WRAPPER_DM+1:#x});
    I2 = DM({WRAPPER_DM+2:#x});
    I3 = DM({WRAPPER_DM+3:#x});
    MODE1 = DM({WRAPPER_DM:#x});
    NOP;
    RTS;
"""

def build_lab_kernel(eq_base=None):
    lite, multi, pack, assembler, simulator = upstream_modules()
    # v2's external table requires the separately tested arena-end reservation.
    # No firmware edits are performed here. Explicitly restore v1 for defaults.
    assert eq_base in (None, 0x827000, 0x30a700)
    multi.DDR2 = 0x324500 if eq_base is None else eq_base
    multi.DDR2_END = 0x328000 if eq_base is None else 0x30b000 if eq_base==0x30a700 else 0x828000
    source, layout = multi.build(slots=1)
    source += frame32_wrapper()
    sections, symbols = assembler.assemble(source, "podgo_nam_lab.asm")
    # Reject overlap of code and statically claimed normal-word memory.
    used = []
    for section in sections:
        if section.kind == "pm":
            base = section.addr & ~0x1ffff
            start = base + (section.addr-base)*3//2
            end = start + (len(section.words)*3+1)//2
        else:
            start, end = section.addr, section.addr + len(section.words)
        assert all(end <= a or b <= start for a, b in used), (section.kind, hex(section.addr))
        used.append((start, end))
    return source, layout, sections, symbols

def checked_stock():
    firmware = ROOT / "originals/POD_Go_v2.50.0_0x6e984472.hxf"
    assert sha(firmware.read_bytes()) == STOCK_SHA256
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from map_l6ia import parse_records, wordswap
    from inspect_model_catalog import parse_catalog
    from podgo_module_resources import ModuleResources
    raw = (ROOT / "artifacts/pod_go_2.50_inflated.bin").read_bytes()
    view = wordswap(raw)
    _, records, _ = parse_records(raw)
    cat = parse_catalog(view, records[3])
    return raw, view, records, cat, ModuleResources(view, records[4], cat)

def port_model_plan():
    raw, view, records, catalog, library = checked_stock()
    by_name = {c["name"]: c for c in catalog["classes"]}
    selected, protected = by_name[REPLACEMENT_MODEL], by_name[PROTECTED_MODEL]
    selected_model = library.model(REPLACEMENT_MODEL)
    users = []
    for cls in catalog["classes"]:
        words = library.words(cls["raw_words"][13])
        if words[0] == 3 and words[words[2]+1] == selected_model["native_id"]:
            users.append(cls["name"])
    # Critical: Clarity's native host is shared by other Catalyst amp/preamp
    # models. Only Clarity's model module may point to our separate host.
    assert PROTECTED_MODEL not in users
    assert not any("Badonk" in name for name in users)
    return dict(replacement_model=REPLACEMENT_MODEL, replacement_class_id=selected["id"],
        replacement_module_index=selected["raw_words"][13],
        replacement_original_module_sha256=sha(library.data(selected["raw_words"][13])),
        replacement_original_native_host=hex(selected_model["native_id"]),
        original_native_host_shared_by=users, requires_separate_native_host=True,
        protected_model=PROTECTED_MODEL, protected_class_id=protected["id"],
        protected_module_index=protected["raw_words"][13],
        protected_module_sha256=sha(library.data(protected["raw_words"][13])),
        protected_catalog_descriptor_sha256=sha(struct.pack("<14I", *protected["raw_words"])),
        proposed_kernel_primitive="0x9c75", primitive_replacement_must_be_verified=True,
        firmware_modified=False, installation_ready=False)

def build_separate_host(control, layout):
    """Create an unlinked model-library component, preserving all other models.

    This is intentionally not inserted in an HXF. Catalog/UI counts, runtime
    graph-buffer capacity, boot placement and native callbacks remain gates.
    """
    raw, view, records, catalog, library = checked_stock()
    b0 = library.common
    tree = ast.parse((UPSTREAM / "tools/patch_amp.py").read_text())
    wanted = {"sharc_blob0_check", "clone_composite_in_blob0", "nam_host_member"}
    functions = [n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name in wanted]
    # The relocation check needs a synthetic pointer origin; it checks offsets
    # by adding/subtracting it, not by claiming this is an actual POD RAM base.
    namespace = {"SHARC_BLOB0_WORDS": len(b0), "SHARC_BLOB0_MAX_WORDS": len(b0)+65536,
        "SHARC_BLOB0_BASE": 0x10000000}
    exec(compile(ast.Module(body=functions, type_ignores=[]), str(UPSTREAM/"tools/patch_amp.py"), "exec"), namespace)
    assert namespace["sharc_blob0_check"](b0) is None
    cloned, new_id = namespace["clone_composite_in_blob0"](b0, 0x600114)
    assert new_id == 0x600129
    namespace["nam_host_member"](cloned, new_id, sdram_words=layout.sdram_words())
    assert namespace["sharc_blob0_check"](cloned, len(cloned)) is None
    # Every original native descriptor still points at its exact original
    # member/port data; only the copied table and added host were introduced.
    for k in range(b0[15]):
        old = b0[b0[16]+7*k:b0[16]+7*(k+1)]
        new = cloned[cloned[16]+7*k:cloned[16]+7*(k+1)]
        assert old == new
        assert b0[old[3]:old[5]+4*old[4]] == cloned[new[3]:new[5]+4*new[4]]
    # Preserve the entire original library as inert source, append the two
    # changed blobs, then change only their directory entries and size header.
    payload = bytearray((ROOT / "artifacts/stock-entry-4.sw4.bin").read_bytes())
    directory = list(struct.unpack_from("<6I", payload))
    native_data = struct.pack("<"+str(len(cloned))+"I", *cloned)
    for index, data in ((0, native_data), (206, control)):
        offset = len(payload)//4
        payload.extend(data)
        struct.pack_into("<2I", payload, (directory[3]+2*index)*4, len(data)//4, offset)
        if index == 0:
            struct.pack_into("<I", payload, 16, offset)
    struct.pack_into("<I", payload, 20, len(payload)//4)
    preserved = []
    protected = []
    for index in range(directory[2]):
        if index in (0,206):
            continue
        count, offset = struct.unpack_from("<2I", payload, (directory[3]+2*index)*4)
        candidate = bytes(payload[offset*4:(offset+count)*4])
        original = library.data(index)
        assert candidate == original, index
        preserved.append(index)
    for name in PROTECTED_MODELS:
        cls = next(c for c in catalog["classes"] if c["name"] == name)
        protected.append(dict(name=name, module=cls["raw_words"][13],
            module_sha256=sha(library.data(cls["raw_words"][13])),
            catalog_sha256=sha(struct.pack("<14I", *cls["raw_words"]))))
    return bytes(payload), dict(new_native_host=hex(new_id), native_components_before=b0[15],
        native_components_after=cloned[15], original_native_components_preserved=b0[15],
        original_other_module_blobs_preserved=len(preserved), protected_models=protected,
        original_graph_buffer_words=len(b0), required_graph_buffer_words=len(cloned),
        graph_buffer_growth_words=len(cloned)-len(b0), runtime_graph_buffer_resized=False,
        catalog_and_UI_updated=False, installation_ready=False)

def host_control_component(builtin=False):
    """Use the reviewed upstream pure graph builder; no patcher CLI/imports.

    Extract exactly one standalone function from its pinned AST, avoiding the
    patcher's Stomp-specific addresses and optional numpy dependency.
    """
    checked_upstream()
    tree = ast.parse((UPSTREAM / "tools/patch_amp.py").read_text())
    function = next(n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name == "nam_host_blob")
    namespace = {}
    exec(compile(ast.Module(body=[function], type_ignores=[]), str(UPSTREAM / "tools/patch_amp.py"), "exec"), namespace)
    # IDs 5..11 and 15 are the generic primitive's DM control offsets. This
    # is a candidate ABI contract until the original POD callbacks are executed.
    params = [dict(slot=1, pid=7, op=0x0b, name="Model", default_word=0 if builtin else 1),
        dict(slot=2, pid=5, op=0x0a, name="Input", default_word=0x3f000000),
        *[dict(slot=3+i, pid=8+i, op=0x0a, name=n, default_word=0x3f000000)
          for i, n in enumerate(("Bass", "Mid", "Treble", "Presence"))],
        dict(slot=7, pid=6, op=0x0a, name="ChVol", default_word=0x3f000000),
        dict(slot=8, pid=15, op=0x0d, name="Polarity", default_word=0)]
    # Reserved future ID, not installed. The library currently has 297 native
    # components, so this denotes the first extra component of a later rebuild.
    component = namespace["nam_host_blob"](0x600129, params)
    data = struct.pack("<" + str(len(component)) + "I", *component)
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from decode_runtime_graphs import decode
    decoded = decode(data)
    header = component[:12]
    assert component[header[2]+1] == 0x600129
    assert len(decoded["graph"]["control_bindings"]) == 8
    assert len(decoded["graph"]["nodes"]) == 16
    return data, params, decoded

def export_test_model(directory):
    lite, multi, pack, assembler, sim = upstream_modules()
    # Never apply the Stomp's unmeasured input calibration to POD Go. For now
    # the converter deliberately omits capture-level automatic compensation.
    weights = lite.placeholder()
    words = lite.ir_samples(weights, None)
    assert len(words) == 2048 and words[5] == 0
    payload = struct.pack("<2048I", *words)
    fmt = struct.pack("<HHIIHH", 3, 1, 48000, 192000, 4, 32)
    body = b"WAVEfmt " + struct.pack("<I", len(fmt)) + fmt + b"data" + struct.pack("<I", len(payload)) + payload
    wave = b"RIFF" + struct.pack("<I", len(body)) + body
    path = directory / "SYNTHETIC_NAM_TEST_ONLY.wav"
    path.write_bytes(wave)
    return dict(path=str(path.relative_to(ROOT)), sha256=sha(wave), synthetic=True,
        samples=2048, sample_rate=48000, format="mono IEEE float32", automatic_input_calibration=False)

def build_artifacts(eq_base=None):
    target = ROOT / ("artifacts/nam-a2-lite-lab-v1" if eq_base is None else "artifacts/nam-a2-lite-lab-v2")
    target.mkdir(parents=True, exist_ok=True)
    source, layout, sections, symbols = build_lab_kernel(eq_base)
    asm = target / "POD_GO_NAM_FRAME32_SIMULATION_ONLY.asm"
    asm.write_text(source, encoding="utf-8", newline="\n")
    control, params, decoded = host_control_component()
    (target / "NAM_control_UNLINKED.bin").write_bytes(control)
    model_library, native_plan = build_separate_host(control, layout)
    (target / "POD_GO_MODELS_LIBRARY_UNLINKED.bin").write_bytes(model_library)
    plan = port_model_plan()
    native_path = ROOT / "evidence/nam-native-analysis/verification.json"
    native = json.loads(native_path.read_text()) if native_path.exists() else None
    native_controls_verified = False
    if native and native.get("stock_sha256") == STOCK_SHA256:
        native_controls_verified = (native.get("status") == "PASS_ORIGINAL_CONTROL_AND_IR_CPU_EMULATION"
            and native.get("case_count") == 17
            and all(sha((ROOT/"scripts"/name).read_bytes()) == digest
                for name, digest in native["scripts"].items())
            and all(sha((Path(native["output"])/name).read_bytes()) == digest
                for name, digest in native["evidence_sha256"].items()))
    result = dict(stage="OFFLINE_PORT_COMPONENTS_UNLINKED", upstream_revision=REVISION,
        artifact_directory=str(target), EQ_base=hex(layout.eq_tab),
        required_external_arena_end=hex(eq_base) if eq_base is not None else None,
        required_external_arena_reservation_applied_to_firmware=False,
        stock_sha256=STOCK_SHA256, target_native_frame_samples=32, kernel_frame_samples=16,
        kernel_instances=1, kernel_asm_sha256=sha(source.encode()),
        entry_symbol="POD_NAM_FRAME32", candidate_entry_pm=hex(symbols["POD_NAM_FRAME32"]),
        podgo_IR_flags=hex(POD_IR_FLAGS), podgo_IR_bank=hex(POD_IR_BANK), podgo_IR_top=hex(POD_IR_TOP),
        control_component_sha256=sha(control), parameters=params, layout=layout.report(),
        model_library_sha256=sha(model_library), native_host_plan=native_plan,
        claimed_memory=[list(x) for x in layout.claims()], model_plan=plan,
        synthetic_test_model=export_test_model(target),
        physical_memory_placement_verified=False, original_control_callbacks_executed=native_controls_verified,
        original_control_callback_evidence=str(native_path) if native_controls_verified else None,
        original_audio_dispatcher_to_NAM_executed=False,
        firmware_image_created=False, hardware_accessed=False,
        pending=["Execute the native dispatcher into the new NAM wrapper; generic control callbacks are a separate bounded CPU test.",
            "Prove code/data reservation across initialization and runtime; the current placement is simulator-only.",
            "Clone the shared native host without altering the other amp/preamp models, and size the graph buffer.",
            "Update model/catalog/UI/admission tables consistently; validate POD Go Edit IR transfer and input calibration.",
            "Complete timing, boot and hardware validation only after separate authorization."])
    (target / "manifest.json").write_text(json.dumps(result, indent=2))
    return result

if __name__ == "__main__":
    result = build_artifacts()
    print(json.dumps({k: result[k] for k in ("stage", "kernel_instances", "candidate_entry_pm", "podgo_IR_flags", "podgo_IR_top", "model_plan")}, indent=2))
