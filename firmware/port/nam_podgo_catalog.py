"""In-place Clarity -> NAM metadata, preserving all other model descriptors."""
import ast
import struct
import zlib
import nam_podgo_lab as port

def reviewed_parameters(catalog_payload):
    port.checked_upstream();tree=ast.parse((port.UPSTREAM/"tools/patch_amp.py").read_text())
    names={"parse_entry3_full","find_model","nam_params","f2u"};constants={"NAM_PARAMS","NAM_INPUT_OP"}
    nodes=[]
    for node in tree.body:
        if isinstance(node,ast.FunctionDef) and node.name in names:nodes.append(node)
        elif isinstance(node,ast.Assign) and any(isinstance(t,ast.Name) and t.id in constants for t in node.targets):nodes.append(node)
    env={"struct":struct};exec(compile(ast.Module(body=nodes,type_ignores=[]),"reviewed_parameter_metadata","exec"),env)
    return env["nam_params"](catalog_payload,env["parse_entry3_full"](catalog_payload))

def build_catalog(builtin=False):
    raw,view,records,catalog,library=port.checked_stock();base=catalog["base_view_offset"]
    payload=bytearray(view[base:records[3]["end_exclusive"]]);original=bytes(payload)
    h=list(struct.unpack_from('<10I',payload));assert len(payload)==h[2]
    target=next(c for c in catalog['classes'] if c['name']==port.REPLACEMENT_MODEL)
    params=reviewed_parameters(bytes(payload));strings=[s['text'] for s in catalog['strings']]
    if builtin:
        assert params[0]['slot']==1 and params[0]['pid']==7 and params[0]['raw'][5]==1
        params[0]['raw'][5]=0
        params[0]['raw'][7]=0
        params[0]['default_word']=0
    ids={s:i for i,s in enumerate(strings)};added=[]
    def string(value):
        if value not in ids:ids[value]=len(strings);strings.append(value);added.append(value)
        return ids[value]
    display_id=string("NAM A2 Lite")
    for p in params:
        string(p['name']);string(p['state'])
        for key,kind,value in p['meta']:
            string(key)
            if kind==7:string(value)
    # Suffix pooling is lossless for NUL-terminated strings. IDs and text stay
    # the same; it creates space for new labels within the stock string block.
    pool=bytearray();offsets={}
    for text in sorted(set(strings),key=lambda s:(-len(s),s)):
        data=text.encode('ascii')+b'\0';offset=pool.find(data)
        if offset<0:offset=len(pool);pool.extend(data)
        offsets[text]=offset
    block=bytearray(struct.pack('<2I',len(strings),h[5]))
    for text in strings:
        data=text.encode('ascii');block.extend(struct.pack('<3I',offsets[text],zlib.crc32(data)&0xffffffff,len(data)))
    block.extend(pool);assert len(block)<=h[5],(len(block),h[5])
    saved=h[5]-len(block);block.extend(bytes(saved));payload[h[3]:h[3]+h[5]]=block
    # Keep the stock table offsets and capacities, replacing only Clarity's
    # own parameter metadata, states and control descriptors.
    d=list(target['raw_words']);old_params=target['controls'];meta_start=min(p['attribute_offset'] for p in old_params)
    meta_end=d[1];meta=bytearray();pwords=[];states=[]
    assert meta_start==423360 and meta_end-meta_start==180
    for k,p in enumerate(params):
        at=meta_start+len(meta)
        for key,kind,value in p['meta']:meta.extend(struct.pack('<3I',string(key),kind,string(value) if kind==7 else value))
        pw=list(p['raw']);pw[0]=len(p['meta']);pw[1]=at;pw[2]=string(p['name']);pwords.extend(pw)
        states.extend([0,target['states'][0]['raw_words'][1],string(p['state']),p['type'],k])
    assert len(meta)<=meta_end-meta_start
    assert len(pwords)==8*8 and len(states)==8*5
    payload[meta_start:meta_start+len(meta)]=meta
    struct.pack_into('<64I',payload,d[11],*pwords);struct.pack_into('<40I',payload,d[10],*states)
    d[6]=d[7]=8;struct.pack_into('<14I',payload,h[6]+56*target['id'],*d)
    for j in range(d[0]):
        k,kind,value=struct.unpack_from('<3I',payload,d[1]+12*j)
        if strings[k]=='displayName':assert kind==7;struct.pack_into('<I',payload,d[1]+12*j+8,display_id)
    h[4]=zlib.crc32(block)&0xffffffff;struct.pack_into('<10I',payload,0,*h)
    assert len(payload)==len(original) and len(added)==len(strings)-len(catalog['strings'])
    # Run the independent catalog reader against the complete original view.
    from inspect_model_catalog import parse_catalog
    new_view=bytearray(view);new_view[base:base+len(payload)]=payload
    linked=parse_catalog(new_view,records[3]);assert [s['text'] for s in linked['strings'][:len(catalog['strings'])]]==[s['text'] for s in catalog['strings']]
    for old,new in zip(catalog['classes'],linked['classes']):
        if old['id']!=206:assert old==new,old['name']
    model=linked['classes'][206]
    assert [(p['name'],p['parameter_id'],p['type_word']) for p in model['controls']]==[(p['name'],p['slot'],p['type']) for p in params]
    protected=[]
    for name in port.PROTECTED_MODELS:
        c=next(c for c in linked['classes'] if c['name']==name)
        protected.append(dict(name=name,catalog_descriptor_sha256=port.sha(struct.pack('<14I',*c['raw_words'])),module_sha256=port.sha(library.data(c['raw_words'][13]))))
    return bytes(payload),dict(status="LINKED_CATALOG_COMPONENT_NOT_FIRMWARE",class_id=206,
        internal_model_name=port.REPLACEMENT_MODEL,display_name="NAM A2 Lite",control_count=8,state_count=8,
        catalog_bytes=len(payload),catalog_growth_bytes=0,stock_descriptors_preserved=619,
        stock_string_texts_and_ids_preserved=len(catalog['strings']),added_labels=added,
        string_block_padding_after_pooling=saved,protected_models=protected,parameters=params,
        builtin_model_zero=builtin,firmware_export_allowed=False)

def build_arm_metadata(candidate_cost=2345):
    _,view,records,catalog,_=port.checked_stock()
    from map_l6ia import parse_arm_segments,Memory
    arm=parse_arm_segments(view,records[0]);memory=Memory(view,arm['segments'])
    base=records[0]['payload_offset']+512;payload=bytearray(view[base:records[0]['end_exclusive']]);edits=[]
    def edit(va,before,after,label):
        off=memory.offset(va,len(before))-base
        assert payload[off:off+len(before)]==before,(hex(va),label)
        assert len(before)==len(after);payload[off:off+len(before)]=after
        edits.append(dict(VA=hex(va),before=before.hex(),after=after.hex(),purpose=label))
    edit(0x280020c4,struct.pack('<I',1800),struct.pack('<I',candidate_cost),'NAM admission cost; upstream estimate, not POD timing measurement')
    edit(0x2800c728,b'Line6 Clarity\0\0\0',b'NAM A2 Lite'.ljust(16,b'\0'),'Model editor title')
    edit(0x2800c73c,struct.pack('<3I',9,9,9),struct.pack('<3I',8,8,8),'Three UI parameter counts agree with catalog and pmap')
    edit(0x28004752,b'Line 6 Clarity'.ljust(26,b'\0'),b'NAM A2 Lite'.ljust(26,b'\0'),'Browser label; same model ID and cab companion')
    struct.pack_into('<I',payload,8,zlib.crc32(payload[52:])&0xffffffff)
    # Both model rows and the Badonk module selection remain unchanged.
    for va,name in [(0x280020a4,'Amp Badonk'),(0x28003244,'Preamp Badonk')]:
        off=memory.offset(va,12)-base;assert payload[off:off+12]==memory.read(va,12),name
    return bytes(payload),dict(status="LINKED_ARM_METADATA_NOT_FIRMWARE",image_bytes=len(payload),
        candidate_admission_cost=candidate_cost,DSP_cost_measured_on_POD=False,edits=edits,
        Badonk_admission_rows_unchanged=True,instruction_code_changed=False,firmware_export_allowed=False)
