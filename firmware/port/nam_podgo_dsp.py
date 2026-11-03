"""POD Go-specific DSP boot component builder, no updater or USB APIs."""
import struct
import nam_podgo_lab as port

EQ_BASE=0x30a700
ARENA_START=0x30b000
ARENA_END=0x828000

def blocks(ldr):
    pos=1536;result=[]
    while pos+12<=len(ldr):
        tag,n,a=struct.unpack_from('<3I',ldr,pos);off=pos+12
        if tag==0:return result,pos
        size=(0 if tag in (1,2,7,8) else (n+n%2)*2 if tag==3 else n*4 if tag in (4,9,10) else (n+n%2)*6 if tag==5 else n*8 if tag==6 else -1)
        assert size>=0 and off+size<=len(ldr)
        result.append(dict(tag=tag,count=n,address=a,header=pos,payload=off,size=size));pos=off+size
    raise ValueError('Missing FINAL_INIT')

def write_visa(ldr,entries,address,before,after):
    assert len(before)==len(after) and len(before)%2==0
    b=next(b for b in entries if b['tag']==3 and b['address']<=address and address+len(before)//2<=b['address']+b['count'])
    for i in range(len(before)//2):
        k=address-b['address']+i;offset=b['payload']+4*(k//2)+(2 if k%2==0 else 0)
        expected=int.from_bytes(before[2*i:2*i+2],'big');assert struct.unpack_from('<H',ldr,offset)[0]==expected
        struct.pack_into('<H',ldr,offset,int.from_bytes(after[2*i:2*i+2],'big'))

def write_dm(ldr,entries,address,before,after):
    b=next(b for b in entries if b['tag'] in (4,9,10) and b['address']<=address and address+len(before)<=b['address']+b['count'])
    offset=b['payload']+4*(address-b['address']);assert list(struct.unpack_from('<%dI'%len(before),ldr,offset))==before
    struct.pack_into('<%dI'%len(after),ldr,offset,*after)

def build_dsp(kernel=None):
    stock=(port.ROOT/'artifacts/pod_go_dsp.ldr').read_bytes()
    assert port.sha(stock)=='4c55806b2e0c4034f6c628e95a9e6edbbbe2f0b07fb39d4cd59f51ed3bc5b605'
    output=bytearray(stock);entries,final=blocks(output);edits=[]
    # Preserve the upper arena end and every stock FX reservation. The extra
    # Amp space comes from the proposed gap below the original arena start.
    for address,before,after,label in [
        (0x16cf32,'0f2000310000','0f200030b000','Native external base/current'),
        (0x16cf42,'0f0c00518000','0f0c0051d000','Native external zero length'),
        (0x16cf45,'0f0400310000','0f040030b000','Native external zero start')]:
        write_visa(output,entries,address,bytes.fromhex(before),bytes.fromhex(after))
        edits.append(dict(SW=hex(address),before=before,after=after,purpose=label))
    original=[0x9c75,0x610d9a,0,0,0x610e63,0x8e6da,0x50,0]
    replacement=[0x9c75,0x60d8f2,0,0x60d956,0x60d83f,port.WRAPPER_PM,0x91,0x0d]
    write_dm(output,entries,0x200588,original,replacement)
    source,layout,sections,symbols=kernel if kernel is not None else port.build_lab_kernel(eq_base=EQ_BASE)
    injection=bytearray();summary=[]
    for section in sections:
        words=list(section.words)
        if section.kind=='pm':
            if len(words)%2:words.append(0)
            body=b''.join(w.to_bytes(6,'little') for w in words);tag=5
        else:
            assert section.kind=='dm';body=struct.pack('<%dI'%len(words),*words);tag=4
        injection.extend(struct.pack('<3I',tag,len(words),section.addr)+body)
        summary.append(dict(tag=tag,address=hex(section.addr),words=len(words)))
    output=bytes(output[:final])+injection+bytes(output[final:])
    parsed,new_final=blocks(output)
    assert output[new_final:]==stock[final:] and len(output)<=0x80000
    # This builder creates an intermediate loader, not a claim of full boot
    # or runtime compatibility. Physical gaps need their separate proof.
    report=dict(status='CANDIDATE_DSP_COMPONENT_NOT_FIRMWARE',stock_loader_sha256=port.sha(stock),
        candidate_loader_sha256=port.sha(output),bytes=len(output),original_FINAL_INIT_preserved=True,
        kernel_assembly_sha256=port.sha(source.encode()),registry_DM='0x200588',registry_original=original,
        registry_candidate=replacement,boot_edits=edits,injected_blocks=summary,
        required_arena_start=hex(ARENA_START),required_arena_end=hex(ARENA_END),
        required_EQ_base=hex(layout.eq_tab),reserved_native_external_words=hex(ARENA_END-ARENA_START),
        full_runtime_memory_proved=False,firmware_export_allowed=False,hardware_accessed=False)
    return output,source,layout,report
