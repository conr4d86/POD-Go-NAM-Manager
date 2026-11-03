"""Read-only decoder for observed v3 runtime containers and control graphs.

Offsets are in words, relative to the module or its graph arena as indicated.
Primitive kinds remain numeric: their DSP implementations are not decoded here.
"""
import hashlib
import json
import struct
from map_l6ia import ROOT, load
from inspect_model_catalog import parse_catalog


def decode(data, view_offset=0):
    w = list(struct.unpack('<%dI' % (len(data)//4), data))
    h = w[:12]
    if h[:2] != [3, 2]:
        raise ValueError('Not an observed v3/2 graph container')
    assert 12 <= h[2]
    assert h[2]+4*h[3] == h[6]
    assert h[6]+h[7] == h[10]
    assert h[10]+4*h[11] == h[4]
    assert h[4]+2*h[5] == h[8]
    assert h[8]+2*h[9] == len(w)
    arena = w[h[6]:h[6]+h[7]]

    def address(graph_word):
        return view_offset+4*(h[6]+graph_word)

    def graph(offset, object_row, path):
        gh = arena[offset:offset+24]
        assert len(gh) == 24 and gh[0] == offset+24
        assert all(gh[i] == 0 for i in [1,3,5,7,9,11])
        assert gh[4]+gh[20]*4 == gh[6]
        assert gh[6]+gh[21]*4 == gh[8]
        assert gh[8]+gh[22]*4 == gh[10]
        nodes = []
        for i in range(gh[13]):
            p = gh[0]+i*10
            row = arena[p:p+10]
            assert row[2] == i
            finish = arena[p+10] if i+1 < gh[13] else gh[2]
            assert gh[0]+gh[13]*10 <= row[0] <= finish <= gh[2]
            init = arena[row[0]:finish]
            node = {'id': i, 'primitive_kind': row[3], 'row_words': row,
                    'row_view_offset': address(p), 'init_words': init,
                    'init_view_offset': address(row[0])}
            if row[3] == 1 and len(init) == 3:
                ref = init[0]
                assert 12 <= ref < h[2] and ref+1+w[ref] <= h[2]
                node['reference'] = {'module_word': ref,
                    'path_words': w[ref+1:ref+1+w[ref]],
                    'member': init[1], 'mode': init[2],
                    'warning': 'Member/mode semantics not established; not necessarily audio ports or control IDs.'}
            nodes.append(node)

        def rows(start, end, width):
            assert (end-start) % width == 0
            return [{'words': arena[p:p+width], 'view_offset': address(p)}
                    for p in range(start,end,width)]

        edges = rows(gh[4], gh[6], 4)
        for e in edges:
            assert e['words'][0] < len(nodes) and e['words'][1] < len(nodes)
        children = []
        child_start, child_count = object_row[2:4]
        assert gh[23] == child_count
        for i in range(child_count):
            child = w[child_start+i*4:child_start+i*4+4]
            p = gh[10]+i*8
            desc = arena[p:p+8]
            if desc[0]:
                children.append(graph(desc[0], child, path+[child[0]]))
        return {'object_path': path, 'arena_word_offset': offset,
                'header_view_offset': address(offset), 'header_words': gh,
                'nodes': nodes, 'edges': edges,
                'control_bindings': rows(gh[6],gh[8],4),
                'state_bindings': rows(gh[8],gh[10],4), 'children': children}

    result = {'view_offset': view_offset, 'bytes': len(data),
              'sha256': hashlib.sha256(data).hexdigest(), 'header_words': h,
              'graph': graph(0,w[h[2]:h[2]+4],[])}
    return result


def main():
    _,view,_,records,*_ = load()
    catalog = parse_catalog(view,records[3])
    base = records[4]['payload_offset']+512
    count = struct.unpack_from('<I',view,base+8)[0]
    selected = {'P34_App','P34_AppDSPFlowOutput','P34_AppCabHarnessSingle','P34_AppDSPFlowInput'}
    modules, formats = [], {}
    validated = 0
    for i in range(count):
        length,offset = struct.unpack_from('<2I',view,base+24+i*8)
        start = base+offset*4
        data = view[start:start+length*4]
        tag = '/'.join(map(str,struct.unpack_from('<2I',data)))
        formats[tag] = formats.get(tag,0)+1
        names = [c for c in catalog['classes'] if c['raw_words'][13] == i] if i else []
        for cls in names:
            decoded = decode(data,start)
            decoded['class_name'] = cls['name']
            decoded['module_index'] = i
            bindings = decoded['graph']['control_bindings']
            assert len(bindings) == len(cls['controls'])
            for binding, control in zip(bindings,cls['controls']):
                assert binding['words'][1:3] == [control['row'],control['parameter_id']]
                binding['name'] = control['name']
            bindings = decoded['graph']['state_bindings']
            assert len(bindings) == len(cls['states'])
            for binding, state in zip(bindings,cls['states']):
                assert binding['words'][1:3] == [state['row'],state['state_index']]
                binding['name'] = state['name']
            for child in decoded['graph']['children']:
                if len(child['object_path']) == 1:
                    matches = [n for n in cls['nodes'] if n['raw_words'][4] == child['object_path'][0]]
                    if matches:
                        child['catalog_child_name'] = matches[0]['name']
            validated += 1
            if cls['name'] in selected:
                modules.append(decoded)
    root = next(m for m in modules if m['class_name'] == 'P34_App')
    def reachable(g, start):
        seen, pending = set(), [start]
        while pending:
            n = pending.pop()
            if n in seen:
                continue
            seen.add(n)
            pending.extend(e['words'][1] for e in g['edges'] if e['words'][0] == n)
        return sorted(seen)
    def trace(class_name, control_name):
        module = next(m for m in modules if m['class_name'] == class_name)
        g = module['graph']
        binding = next(b for b in g['control_bindings'] if b['name'] == control_name)
        ids = reachable(g,binding['words'][0])
        return {'class_name':class_name,'control_binding':binding,
                'reachable_node_ids':ids,'nodes':[g['nodes'][i] for i in ids],
                'edges':[e for e in g['edges'] if e['words'][0] in ids],
                'limit':'Reachability through explicit control edges only, excluding runtime behavior and member side effects.'}
    traces = [trace('P34_App','AmpOutRoute'),trace('P34_AppDSPFlowOutput','ToAmpOut'),
              trace('P34_AppCabHarnessSingle','bypass')]
    assert traces[0]['reachable_node_ids'] == [67,68,80,82]
    assert traces[1]['reachable_node_ids'] == [20,21,25]
    assert traces[2]['reachable_node_ids'] == list(range(6))
    embedded_checks = []
    for child_name, class_name in [('cab','P34_AppCabHarnessSingle'),('output','P34_AppDSPFlowOutput')]:
        embedded = next(c for c in root['graph']['children'] if c.get('catalog_child_name') == child_name)
        standalone = next(m['graph'] for m in modules if m['class_name'] == class_name)
        def signature(g):
            return ([n['primitive_kind'] for n in g['nodes']],
                    [e['words'] for e in g['edges']],
                    [b['words'] for b in g['control_bindings']],
                    [b['words'] for b in g['state_bindings']])
        assert signature(embedded) == signature(standalone)
        embedded_checks.append({'child_name':child_name, 'object_path':embedded['object_path'],
            'header_view_offset':embedded['header_view_offset'],
            'matching_fields':['primitive_kind_sequence','edges','control_bindings','state_bindings'],
            'not_compared':['reference_relocations','DSP_implementation','audio_behavior']})
    report = {'scope': 'Static serialized graph structure; no primitive opcode semantics or audio equivalence claimed.',
              'library_module_format_counts': formats, 'validated_module_and_catalog_pairs':validated,
              'embedded_graph_checks':embedded_checks, 'modules': modules}
    destination = ROOT/'evidence/runtime_graphs.json'
    destination.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    (ROOT/'evidence/runtime_route_traces.json').write_text(json.dumps({'traces':traces,
        'primitive_kinds_analyzed_separately':[43,63,67,87],
        'implementation_analysis':'evidence/control_primitive_semantics.json',
        'output_reference_link':{'root_graph_node':80,'reference_module_word':37,
            'resolved_object_id':45,'catalog_child':'output','member':10,'mode':1,
            'matching_output_parameter':'ToAmpOut'},
        'warning':'This structural decoder does not prove reference semantics. Separate instruction analysis identifies root member 12/mode 0 as native state bit 12, not Cab bypass.'},indent=2)+'\n',encoding='utf-8')
    def stats(g):
        nested = [stats(c) for c in g['children']]
        return (1+sum(s[0] for s in nested),len(g['nodes'])+sum(s[1] for s in nested))
    print('Library formats:',formats)
    for m in modules:
        print(m['class_name'], 'graphs/nodes',stats(m['graph']))
        for c in m['graph']['children']:
            if c.get('catalog_child_name') in {'cab','output'}:
                print(' embedded',c['catalog_child_name'],c['object_path'],hex(c['header_view_offset']))
    print('Validated module/catalog pairs:',validated)
    print('Validated partitions, node indices, edge endpoints and catalog bindings:',destination)


if __name__ == '__main__':
    main()
