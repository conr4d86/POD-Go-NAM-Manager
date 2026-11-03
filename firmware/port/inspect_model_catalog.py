"""Decode observed string/class/state/control tables, not DSP instructions."""
import json
import struct
from map_l6ia import load, ROOT


def parse_catalog(view, record, *, allow_nonadjacent_class_table=False):
    base = record['payload_offset'] + 512
    header = struct.unpack_from('<10I', view, base)
    if header[0] != 0x4023245e or header[1] != 4:
        raise ValueError('Unsupported observed catalog version')
    count, string_block_bytes = struct.unpack_from('<2I', view, base + header[3])
    table = base + header[3] + 8
    string_base = table + count * 12
    strings = []
    for i in range(count):
        off, digest, length = struct.unpack_from('<3I', view, table + i * 12)
        begin = string_base + off
        end = begin + length
        if end >= base + header[6] or view[end] != 0:
            raise ValueError('String outside table or not NUL terminated')
        strings.append({'id': i, 'text': view[begin:end].decode('ascii'),
                        'hash_word': digest, 'view_offset': begin})
    if (header[3] + string_block_bytes != header[6] and not allow_nonadjacent_class_table):
        raise ValueError('String block does not meet class table')
    if header[6] < header[3]+string_block_bytes or header[6]+header[7]*56 > header[2]:
        raise ValueError('Invalid class table bounds')
    names = [row['text'] for row in strings]
    classes = []
    def rows(offset, count, words):
        if offset + count * words * 4 > header[2]:
            raise ValueError('Descriptor table extends beyond catalog')
        return [list(struct.unpack_from('<'+str(words)+'I', view, base+offset+i*words*4))
                for i in range(count)]
    for i, fields in enumerate(rows(header[6], header[7], 14)):
        if fields[2] >= len(names) or fields[3] != i:
            raise ValueError('Unexpected class id/name')
        states = []
        for j, w in enumerate(rows(fields[10], fields[6], 5)):
            states.append({'row': j, 'name': names[w[2]], 'name_id': w[2],
                           'type_word': w[3], 'state_index': w[4], 'raw_words': w})
        controls = []
        for j, w in enumerate(rows(fields[11], fields[7], 8)):
            controls.append({'row': j, 'name': names[w[2]], 'name_id': w[2],
                             'type_word': w[3], 'parameter_id': w[4],
                             'range_and_default_words': w[5:8],
                             'attribute_count': w[0], 'attribute_offset': w[1],
                             'descriptor_view_offset': base+fields[11]+j*32,
                             'raw_words': w})
        nodes = []
        for j, w in enumerate(rows(fields[9], fields[5], 8)):
            nodes.append({'row': j, 'name': names[w[2]], 'name_id': w[2],
                          'candidate_class_index': w[7],
                          'allowed_class_indices': list(struct.unpack_from('<'+str(w[5])+'I', view, base+w[6])),
                          'raw_words': w})
        classes.append({'id': i, 'name': names[fields[2]], 'kind_word': fields[0],
                        'descriptor_view_offset': base+header[6]+i*56,
                        'raw_words': fields, 'states': states, 'controls': controls, 'nodes': nodes})
    for cl in classes:
        for node in cl['nodes']:
            node['candidate_class_name'] = classes[node['candidate_class_index']]['name']
            node['allowed_class_names'] = [classes[i]['name'] for i in node['allowed_class_indices']]
    return {'record': record['index'], 'base_view_offset': base,
            'header_words': list(header), 'string_base_view_offset': string_base,
            'strings': strings, 'classes': classes,
            'scope': 'Observed metadata layout. Attributes are key/type/string-id triples, not executable bytecode.'}


def main():
    raw, view, header, records, end, arm, memory = load()
    catalog = parse_catalog(view, records[3])
    (ROOT/'artifacts/model_catalog_full.json').write_text(json.dumps(catalog, indent=2)+'\n', encoding='utf-8')
    selected = [c for c in catalog['classes'] if c['name'] in {
        'P34_App', 'P34_AppDSPFlowInput', 'P34_AppDSPFlowOutput',
        'P34_AppCabHarnessSingle', 'P34_AppFxHarnessStereo'}]
    report = {k: catalog[k] for k in ['record', 'base_view_offset', 'header_words', 'string_base_view_offset', 'scope']}
    report.update({'string_count': len(catalog['strings']), 'class_count': len(catalog['classes']), 'classes': selected})
    (ROOT/'evidence/amp_out_catalog.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    for cl in selected:
        print(cl['id'], cl['name'], 'states', len(cl['states']), 'controls', len(cl['controls']), 'nodes', len(cl['nodes']))
        for row in cl['controls']:
            if 'Amp' in row['name'] or cl['name']=='P34_AppCabHarnessSingle':
                print('  control', row['name'], row['parameter_id'], hex(row['descriptor_view_offset']), row['range_and_default_words'])


if __name__ == '__main__':
    main()
