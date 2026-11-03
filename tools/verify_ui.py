"""Verify v8 Model-value Thumb code offline using the user's generated HXF."""
import sys,json,struct,zlib
from pathlib import Path
from build_firmware import ROOT,chunks
sys.path.insert(0,str(ROOT/'firmware/port'))
from map_l6ia import wordswap,parse_records,parse_arm_segments,Memory
from nam_titles import SITE,PID,assemble,make_cave
import unicorn
from unicorn.arm_const import *
raw=zlib.decompress(next(data for tag,data in chunks(Path(sys.argv[1]).read_bytes()) if tag==b'data'))
view=wordswap(raw);_,rows,_=parse_records(raw);arm=parse_arm_segments(view,rows[0]);memory=Memory(view,arm['segments'])
seg=arm['segments'][6];base=seg['load_address']+seg['bytes']-448;cave=make_cave(base)
assert len(cave)==448 and memory.read(base,448)==cave
assert memory.read(SITE,4)==assemble('b.w '+hex(base),SITE)
assert memory.read(0x28004750,2)==struct.pack('<H',PID)
e0=view[arm['image_offset']:rows[0]['end_exclusive']];entries={'ModelValue':base}
SP=0x28080000;BLOCK=0x28020000;VALUE=SP+0x34;TEXT=0x282c66ec
REGS=[UC_ARM_REG_R0,UC_ARM_REG_R1,UC_ARM_REG_R2,UC_ARM_REG_R3,
      UC_ARM_REG_R4,UC_ARM_REG_R5,UC_ARM_REG_R6,UC_ARM_REG_R7,
      UC_ARM_REG_R8,UC_ARM_REG_R9,UC_ARM_REG_R10,UC_ARM_REG_R11,UC_ARM_REG_R12,UC_ARM_REG_LR]
fontbase=rows[7]['payload_offset']+512
font=struct.unpack_from('<12I',view,fontbase+0x320c+543*48)
fontmetrics=view[fontbase+font[11]:fontbase+font[11]+128*12]
assert font[10]==128 and font[5]==1

def machine(model=53,pid=PID,knob=0,param=0,half=0,name='NAM_0123456789ab FRIEDMAN HBE',result=0,text=None,anchor=48):
    u=unicorn.Uc(unicorn.UC_ARCH_ARM,unicorn.UC_MODE_THUMB)
    u.mem_map(0x28000000,0x1000000)
    for s in arm['segments']:
        if s['load_address']>=0x28000000:u.mem_write(s['load_address'],memory.read(s['load_address'],s['bytes']))
    u.mem_write(entries['ModelValue'],cave)
    u.mem_write(SITE,assemble('b.w '+hex(entries['ModelValue']),SITE))
    u.mem_write(SP-256,b'\xa5'*512)
    u.mem_write(VALUE,(str(model) if text is None else text).encode()+b'\0')
    u.mem_write(BLOCK+1,struct.pack('<I',pid));u.mem_write(SP+0x17,bytes([half]));u.mem_write(SP+0x8c,struct.pack('<I',param))
    u.mem_write(TEXT+8,struct.pack('<2I',anchor,190))
    # This is the stock glyph table, not a fake width function. Execute the
    # native measurement function using its real per-character metrics.
    u.mem_write(0x28b950c4,struct.pack('<I',font[5])+fontmetrics)
    if 1<=model<=128:u.mem_write(0x282c6f9c+(model-1)*120,name.encode()+b'\0')
    initial=[result&0xffffffff,22,33,44,55,knob,BLOCK,77,VALUE,99,110,121,132,0x28ca5501]
    for reg,val in zip(REGS,initial):u.reg_write(reg,val)
    u.reg_write(UC_ARM_REG_SP,SP)
    exits=[];writes=[];calls=[];trace=[]
    def code(uc,address,size,user):
        if address in (0x28c7d680,0x28c7d688,0x28c7d692,0x28c7d348):trace.append((hex(address),[hex(uc.reg_read(r)) for r in REGS[:4]]))
        if address in (0x28ca5500,0x28ca54f6):exits.append(address);uc.emu_stop();return
        if address in (0x28c7cab8,0x28c7cacc):
            calls.append(address);uc.reg_write(UC_ARM_REG_R0,0);uc.reg_write(UC_ARM_REG_PC,uc.reg_read(UC_ARM_REG_LR))
        elif address==0x28c7e370:
            # Renderer invalidation has a native device mutex. Keep the
            # coordinate update's ABI and record it; no renderer is emulated.
            calls.append(address);assert uc.reg_read(UC_ARM_REG_R0)==TEXT
            uc.mem_write(TEXT+8,struct.pack('<2I',uc.reg_read(UC_ARM_REG_R1),uc.reg_read(UC_ARM_REG_R2)))
            uc.reg_write(UC_ARM_REG_R0,0);uc.reg_write(UC_ARM_REG_PC,uc.reg_read(UC_ARM_REG_LR))
    u.hook_add(unicorn.UC_HOOK_CODE,code)
    u.hook_add(unicorn.UC_HOOK_MEM_WRITE,lambda uc,access,address,size,value,data:writes.append((address,size)))
    def invalid(uc,access,address,size,value,data):
        raise AssertionError(('unmapped',hex(address),'pc',hex(uc.reg_read(UC_ARM_REG_PC))))
    u.hook_add(unicorn.UC_HOOK_MEM_INVALID,invalid)
    try:u.emu_start(SITE|1,0,count=30000)
    except unicorn.UcError as e:
        raise AssertionError((str(e),'pc',hex(u.reg_read(UC_ARM_REG_PC)),[hex(u.reg_read(r)) for r in REGS[:9]],trace)) from e
    value=bytes(u.mem_read(VALUE,17)).split(b'\0',1)[0].decode('ascii')
    for i,reg in enumerate(REGS):
        want=initial[0] if i==3 else initial[i]
        assert u.reg_read(reg)==want,(i,hex(u.reg_read(reg)),hex(want))
    assert u.reg_read(UC_ARM_REG_SP)==SP
    assert exits==[0x28ca54f6 if result<0 else 0x28ca5500],exits
    assert all(SP-256<=a and a+n<=SP or VALUE<=a and a+n<=VALUE+17 or TEXT+8<=a and a+n<=TEXT+16 for a,n in writes),writes
    assert bytes(u.mem_read(VALUE-1,1))==b'\xa5'
    assert bytes(u.mem_read(VALUE+17,1))==b'\xa5'
    return value,struct.unpack('<2I',u.mem_read(TEXT+8,8)),calls

cases=[]
def test(name,run):
    run();cases.append(dict(name=name,status='PASS'))
def equal(expected,**kw):
    actual,position,calls=machine(**kw)
    assert actual==expected,(actual,expected,kw)
    return position,calls

test('actual_browser_id_is_589_not_ui_byte_count_528',lambda:equal('0. Dev by Conr4d',model=0))
for n in (1,9,10,52,53,99,100,109,128):
    expected=str(n)+'. FRIEDMAN HBE'
    # 17 bytes includes NUL; the name is bounded by both bytes and pixels.
    expected=expected[:16]
    while sum(struct.unpack_from('<3i',fontmetrics,ord(c)*12)[0]+1 for c in expected)-1>120:expected=expected[:-1]
    test('model_'+str(n),lambda n=n,e=expected:equal(e,model=n))
test('hash_only_existing_slot_stays_readable',lambda:equal('52. NAM',model=52,name='NAM_0123456789ab'))
test('empty_slot_readable',lambda:equal('54. Empty',model=54,name=''))
test('normal_ir_is_not_named_as_a_nam',lambda:equal('53. IR',name='My cabinet'))
test('invalid_hash_not_accepted',lambda:equal('53. IR',name='NAM_zzzzzzzzzzzz WRONG'))
test('non_ascii_is_bounded',lambda:equal('53. HI',name='NAM_0123456789ab HI\x7fBAD'))
test('long_name_is_bounded',lambda:equal('53. WWWWWWW',name='NAM_0123456789ab WWWWWWWWWWWWWWWWW'))
for k in range(1,5):test('other_knob_'+str(k),lambda k=k:equal('53',knob=k))
for p in (1,5,10):test('other_page_param_'+str(p),lambda p=p:equal('53',param=p))
for p in (528,588,590,206):test('other_model_'+str(p),lambda p=p:equal('53',pid=p))
test('cab_half_unchanged',lambda:equal('53',half=1))
test('invalid_index_129_unchanged',lambda:equal('129',model=129))
test('negative_index_unchanged',lambda:equal('-1',model=-1))
test('empty_formatted_value_unchanged',lambda:equal('',text=''))
test('decimal_value_unchanged',lambda:equal('5.0',text='5.0'))
test('too_many_digits_unchanged',lambda:equal('0000',text='0000'))
test('formatter_error_unchanged',lambda:equal('53',result=-42))

def geometry():
    name,pos,_=machine(model=0);assert pos==(64,190)
    assert sum(struct.unpack_from('<3i',fontmetrics,ord(c)*12)[0]+1 for c in name)-1==108
test('builtin_credit_108px_with_shifted_anchor',geometry)
def restore_anchor():
    value,position,_=machine(pid=590,anchor=64)
    assert value=='53' and position==(48,190)
test('normal_amp_restores_stock_value_anchor',restore_anchor)

def title_unchanged():
    image=arm['image_offset']
    for address,expected in ((0x28ca4f14,'237b0d2b00f09c80'),(0x28ca5568,'337b0d2b00f08380'),(0x28ca4f64,'8df86560')):
        assert memory.read(address,len(bytes.fromhex(expected)))==bytes.fromhex(expected)
test('all_old_title_hooks_absent',title_unchanged)

report=dict(status='PASS_ARM_EMULATION_ONLY',cases=cases,USB_opened=False,SDK_started=False,
    physical_display_verified=False,native_ir_getter_executed=True,native_font_measurement_executed=True,
    renderer_emulated=False,stock_title_unchanged=True,model_knob_label_unchanged=True,
    builtin_value='0. Dev by Conr4d',cave_bytes=len(cave),preset_id=PID)
(ROOT/'build/model-value-tests.json').write_text(json.dumps(report,indent=2))
print(json.dumps(dict(status=report['status'],cases=len(cases),USB_opened=False)))
