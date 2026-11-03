"""Observed L6IA record/ARM segment map. Static read-only, no device access."""
import hashlib
import json
from pathlib import Path
import struct
import sys
import zlib

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools/python'))


def wordswap(raw):
    if len(raw) % 4:
        raise ValueError('Expected whole 32-bit words')
    out = bytearray(len(raw))
    for i in range(4):
        out[i::4] = raw[3-i::4]
    return bytes(out)


def parse_records(raw):
    if raw[:4] != b'L6IA':
        raise ValueError('Expected L6IA')
    header = struct.unpack_from('>6I', raw)
    records = []
    offset = 24
    for index in range(header[5]):
        if offset + 36 > len(raw):
            raise ValueError('Truncated record')
        values = struct.unpack_from('>9I', raw, offset)
        size = values[3] * values[4]
        start = offset + 36
        end = start + size
        if values[0] != index or not size or end > len(raw):
            raise ValueError('Invalid record index/length')
        records.append({'index': index, 'header_offset': offset,
                        'version_word': values[1], 'type': values[2],
                        'word_bytes': values[3], 'word_count': values[4],
                        'opaque_words': list(values[5:]),
                        'payload_offset': start, 'payload_bytes': size,
                        'end_exclusive': end})
        offset = end
    return header, records, offset


def parse_arm_segments(view, record):
    wrapper = record['payload_offset']
    if struct.unpack_from('<I', view, wrapper)[0] != 0x56112603:
        raise ValueError('Unexpected wrapper magic')
    start = wrapper + 512
    magic, size, checksum, version, count, entry, stack = struct.unpack_from('<7I', view, start)
    if magic != 0x230b0c5e or size + 512 != record['payload_bytes']:
        raise ValueError('Unexpected ARM image header')
    offset = start + 52
    segments = []
    for index in range(count):
        length, address, cumulative_end = struct.unpack_from('<3I', view, offset)
        data = offset + 12
        end = data + length
        if end - start != cumulative_end or end > record['end_exclusive']:
            raise ValueError('Inconsistent segment end')
        segments.append({'index': index, 'descriptor_offset': offset,
                         'view_offset': data, 'bytes': length,
                         'load_address': address, 'load_end_exclusive': address + length,
                         'sha256': hashlib.sha256(view[data:end]).hexdigest()})
        offset = end
    pad = view[offset:record['end_exclusive']]
    if len(pad) > 3 or any(pad):
        raise ValueError('Unexpected trailing ARM data')
    return {'record': record['index'], 'image_offset': start, 'entry_thumb': entry,
            'initial_stack_field': stack, 'opaque_checksum': checksum,
            'version_word': version, 'segments': segments, 'padding_bytes': len(pad)}


class Memory:
    def __init__(self, view, segments):
        self.view, self.segments = view, segments

    def offset(self, address, size=1):
        for segment in self.segments:
            if segment['load_address'] <= address and address + size <= segment['load_end_exclusive']:
                return segment['view_offset'] + address - segment['load_address']
        raise ValueError(f'Unmapped address {address:#x}')

    def read(self, address, size):
        offset = self.offset(address, size)
        return self.view[offset:offset+size]

    def u32(self, address):
        return struct.unpack('<I', self.read(address, 4))[0]

    def cstring(self, address, limit=160):
        try:
            result = bytearray()
            for i in range(limit):
                c = self.read(address+i, 1)[0]
                if c == 0:
                    return result.decode('ascii') if len(result) >= 3 else None
                if c not in (9, 10, 13) and not 32 <= c < 127:
                    return None
                result.append(c)
        except ValueError:
            pass
        return None


def load():
    raw = (ROOT / 'artifacts/pod_go_2.50_inflated.bin').read_bytes()
    view = wordswap(raw)
    header, records, end = parse_records(raw)
    arm = parse_arm_segments(view, records[0])
    return raw, view, header, records, end, arm, Memory(view, arm['segments'])


def main():
    raw, view, header, records, end, arm, memory = load()
    for record in records:
        begin = record['payload_offset']
        magic, version, checksum, body_size = struct.unpack_from('<4I', view, begin)
        body = view[begin+512:record['end_exclusive']]
        if magic != 0x56112603 or body_size != len(body):
            raise ValueError('Invalid observed record wrapper')
        actual = zlib.crc32(body) & 0xffffffff
        if checksum != actual:
            raise ValueError('Record CRC32 mismatch')
        record['wrapper'] = {'magic': magic, 'version_word': version,
                             'body_bytes': body_size, 'crc32': checksum,
                             'crc32_matches': True}
    report = {'input_sha256': hashlib.sha256(raw).hexdigest(),
              'header_words_be': list(header), 'records': records,
              'archive_end_exclusive': end, 'trailing_bytes': len(raw)-end,
              'main_arm_image': arm,
              'scope': 'Observed records and copy segments; addresses inferred from image descriptors, not live hardware'}
    (ROOT / 'evidence/l6ia_map.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    from capstone import Cs, CS_ARCH_ARM, CS_MODE_THUMB, CS_MODE_MCLASS
    md = Cs(CS_ARCH_ARM, CS_MODE_THUMB | CS_MODE_MCLASS)
    md.skipdata = True
    out = ROOT / 'artifacts/main_m4_linear_disassembly.txt'
    import re
    with out.open('w', encoding='utf-8') as stream:
        for seg in arm['segments']:
            stream.write(f"\n; SEGMENT {seg['index']} VA={seg['load_address']:#x} view={seg['view_offset']:#x}\n")
            for ins in md.disasm(memory.read(seg['load_address'], seg['bytes']), seg['load_address']):
                annotation = ''
                match = re.search(r'\[pc, #(-?0x[0-9a-f]+|\d+)\]', ins.op_str)
                if match and ins.mnemonic.startswith('ldr'):
                    pool = ((ins.address+4) & ~3) + int(match[1], 0)
                    try:
                        value = memory.u32(pool)
                        text = memory.cstring(value)
                        annotation = f' ; pool={pool:#x} value={value:#x}'
                        if text:
                            annotation += ' string='+repr(text)
                    except ValueError:
                        pass
                stream.write(f'{ins.address:08x} {ins.bytes.hex():8} {ins.mnemonic:10} {ins.op_str}{annotation}\n')
    print(json.dumps({'records': len(records), 'archive_end': hex(end),
                      'segments': len(arm['segments']), 'entry': hex(arm['entry_thumb']),
                      'disassembly': str(out)}))


if __name__ == '__main__':
    main()
