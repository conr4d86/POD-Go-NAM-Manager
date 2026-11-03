"""Relink Common without growing its fixed buffer; no device operations.

Only immutable one-word port-index vectors are shared. Relocation-mutated
headers, member records and connection records are never shared.
"""
from collections import defaultdict
from bisect import bisect_left
import ast
import struct
import nam_podgo_lab as port

def topology(w):
    refs=[];structural=set();io=[]
    def structure(a,n):structural.update(range(a,a+n))
    def pointer(position,count,kind):
        target=w[position];assert 0<=target<=target+count<=len(w),(position,target,count)
        refs.append((position,target,count,kind))
    structure(0,22)
    for pos,count,kind in [(2,w[1],"header"),(16,7*w[15],"composite_table"),(11,20*w[10],"root_rows"),(13,4*w[12],"regions")]:pointer(pos,count,kind)
    structure(w[16],7*w[15]);structure(w[13],4*w[12])
    def members(a,n):
        structure(a,20*n)
        for i in range(n):
            r=a+20*i
            for cnt,off,kind in [(w[r+7],8,"input_indices"),(w[r+9],10,"output_indices"),(2*w[r+11],12,"parameters")]:
                pointer(r+off,cnt,kind)
                if kind!="parameters" and cnt==1:io.append(w[r+off])
            k=w[r+1]>>1;pointer(r+2,3*k,"connections");structure(w[r+2],3*k)
            for j in range(k):
                c=w[r+2]+3*j;pointer(c+1,w[c]&~1,"connection_values")
    members(w[11],w[10])
    for i in range(w[15]):
        h=w[16]+7*i;pointer(h+3,20*w[h+2],"members");pointer(h+5,4*w[h+4],"regions")
        members(w[h+3],w[h+2]);structure(w[h+5],4*w[h+4])
    return refs,structural,io

def semantic_component(w,h):
    # Normalize every relocation field to the data it addresses. Port indices,
    # init parameters and all member scalar fields remain exact integers.
    rows=[]
    for i in range(h[2]):
        r=h[3]+20*i;row=list(w[r:r+20]);connections=[]
        for j in range(row[1]>>1):
            c=row[2]+3*j;n=w[c]&~1
            connections.append([w[c],w[w[c+1]:w[c+1]+n],w[c+2]])
        row[2]=connections
        for count,off in [(row[7],8),(row[9],10),(2*row[11],12)]:row[off]=w[row[off]:row[off]+count]
        rows.append(row)
    return [h[0],h[1],h[2],rows,h[4],w[h[5]:h[5]+4*h[4]],h[6]]

def semantic_all(w):
    return [semantic_component(w,w[8:15])]+[semantic_component(w,w[w[16]+7*i:w[16]+7*i+7]) for i in range(w[15])]

def compact(w,target_words):
    refs,structural,io=topology(w);used=set(structural)
    for _,target,n,_ in refs:used.update(range(target,target+n))
    drop=set(range(len(w)))-used
    # The abandoned old composite table is unreachable after cloning. No
    # relocation field may be dropped; zero-length end pointers are harmless.
    assert not any(pos in drop for pos,_,_,_ in refs)
    candidates=defaultdict(list);unsafe=set(structural)
    for _,t,n,_ in refs:
        if n>1:unsafe.update(range(t,t+n))
    for q in sorted(set(io)):
        if q in unsafe:continue
        candidates[w[q]].append(q)
    alias={};shared=[];need=max(128,len(w)-len(drop)-target_words)
    for value,places in sorted(candidates.items()):
        if len(places)<2:continue
        canonical=places[0]
        for q in places[1:]:
            if len(shared)>=need:break
            alias[q]=canonical;drop.add(q);shared.append(dict(old_word=q,canonical_word=canonical,value=value))
        if len(shared)>=need:break
    assert len(w)-len(drop)<=target_words,"Not enough immutable vectors to fit fixed Common buffer"
    kept=[i for i in range(len(w)) if i not in drop];mapping={old:i for i,old in enumerate(kept)}
    def relocate(old,n):
        if old in alias:return mapping[alias[old]]
        if old in mapping:return mapping[old]
        assert n==0,(old,n)
        # A zero-length vector/region can point to the next surviving word.
        return bisect_left(kept,old)
    result=[w[i] for i in kept]
    for position,target,n,_ in refs:result[mapping[position]]=relocate(target,n)
    assert semantic_all(result)==semantic_all(w)
    length=len(result);result.extend([0]*(target_words-length))
    return result,dict(unreachable_words_removed=len(w)-len(used),immutable_IO_words_shared=len(shared),
        linked_words=length,fixed_buffer_words=target_words,padding_words=target_words-length,
        original_components_semantically_equal=True,shared_vectors=shared)

def build_common(layout):
    _,_,_,_,library=port.checked_stock();original=library.common
    tree=ast.parse((port.UPSTREAM/"tools/patch_amp.py").read_text())
    wanted={"sharc_blob0_check","clone_composite_in_blob0","nam_host_member"}
    functions=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in wanted]
    env=dict(SHARC_BLOB0_WORDS=len(original),SHARC_BLOB0_MAX_WORDS=len(original)+65536,SHARC_BLOB0_BASE=0x204400)
    exec(compile(ast.Module(body=functions,type_ignores=[]),"reviewed_common_linker","exec"),env)
    w,new_id=env["clone_composite_in_blob0"](original,0x600114)
    env["nam_host_member"](w,new_id,layout.sdram_words())
    # Keep the full stock FX arena. Extend its lower bound into the proposed
    # free DDR2 gap, rather than taking memory away from stock root reserves.
    required=(layout.sdram_words()+1023)&~1023
    assert required==19456
    h=w[w[16]+7*7:w[16]+7*8];member=h[3]+3*20
    assert w[member+6]==0x60000b and w[member+15]==2048
    w[member+15]=required
    root=w[11]+11*20;assert w[root+6]==0x600007 and w[root+15]==2048
    w[root+15]=required
    assert w[5]==0x518000;w[5]=0x51d000
    before_compact=semantic_all(w)
    linked,report=compact(w,len(original))
    assert env["sharc_blob0_check"](linked,len(original)) is None
    assert semantic_all(linked)==before_compact
    original_semantic=semantic_all(original)
    # Root Amp reservation and one existing harness reservation are the only
    # original graph changes; all 296 other stock composites are identical.
    new_semantic=semantic_all(linked)
    changed=[i for i,(a,b) in enumerate(zip(original_semantic,new_semantic)) if a!=b]
    assert changed==[0,8],changed
    report.update(new_native_host=hex(new_id),stock_components_preserved_semantically=296,
        stock_harness_changed="0x600007 SDRAM reservation only",native_external_start=hex(0x30b000),
        native_external_end=hex(0x828000),native_external_words=hex(0x51d000),
        stock_FX_reservations_unchanged=True,root_amp_external_words=required,
        enlarged_arena_requires_boot_edits=True,physical_placement_verified=False,
        common_buffer_grown=False,firmware_export_allowed=False)
    return struct.pack('<%dI'%len(linked),*linked),report
