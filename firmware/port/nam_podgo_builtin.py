"""POD-specific built-in A2 Lite data path; reviewed upstream stays immutable."""
import nam_podgo_lab as port

BUILTIN_BASE=0x30a700
BUILTIN_WORDS=2048
BUILTIN_TOP=BUILTIN_BASE+BUILTIN_WORDS-1
INTERNAL_EQ=0xe6200
MAIN_PM=0xc4ec0

def enable_builtin_dma_check(rig):
    """Allow read-only model DMA from its exact static region; retain kit checks."""
    original=rig.check_dma
    def check(instance,label):
        keep=[]
        for item in rig.dma.log:
            out,external,internal,count,emod,imod=item
            e0,e1=sorted((external,external+(count-1)*emod))
            i0,i1=sorted((internal,internal+(count-1)*imod))
            if not out and BUILTIN_BASE<=e0 and e1<BUILTIN_BASE+BUILTIN_WORDS:
                assert any(a<=i0 and i1<b for a,b in rig.allowed[:2]),label
            else:keep.append(item)
        rig.dma.log[:]=keep
        original(instance,label)
    rig.check_dma=check

def build_builtin_kernel(weights):
    lite,multi,pack,assembler,sim=port.upstream_modules()
    model_words=lite.ir_samples(weights,None)
    assert len(model_words)==BUILTIN_WORDS and model_words[5]==0
    old_variant=multi._variant
    old_code,old_eq,old_eq_end=multi.CODE,multi.DDR2,multi.DDR2_END
    def variant(source,meter=False,nodma=False,prof=None):
        source=old_variant(source,meter,nodma,prof)
        assert source.count('.section pm 0x0c4ef8')==1
        source=source.replace('.section pm 0x0c4ef8',f'.section pm {MAIN_PM:#x}')
        before="n_int:\n    R1 = 1;\n"
        after=f"""n_int:
    R0 = PASS R0;
    IF NE JUMP pod_user_ir;
    R1 = {BUILTIN_TOP:#x};
    JUMP pod_model_header;
pod_user_ir:
    R1 = 1;
"""
        assert source.count(before)==1
        source=source.replace(before,after)
        before="    R1 = R1 + R2;\n    I3 = R1;\n    F2 = DM(0, I3);"
        after="    R1 = R1 + R2;\npod_model_header:\n    I3 = R1;\n    F2 = DM(0, I3);"
        assert source.count(before)==1
        return source.replace(before,after)
    try:
        multi.CODE=MAIN_PM
        multi.DDR2=INTERNAL_EQ
        multi.DDR2_END=0xe6a00
        multi._variant=variant
        source,layout=multi.build(slots=1)
    finally:
        multi.CODE,multi.DDR2,multi.DDR2_END=old_code,old_eq,old_eq_end
        multi._variant=old_variant
    assert layout.b3_to<=INTERNAL_EQ and layout.ddr2_to<=layout.cold_nw
    assert layout.b2_to<=0xc8000
    source+=f"\n// Built-in conr4daudio capture, IR-format words in descending address order.\n.section dm {BUILTIN_BASE:#x}\n"
    source+=''.join('    .word '+hex(word)+'\n' for word in reversed(model_words))
    source+=port.frame32_wrapper()
    sections,symbols=assembler.assemble(source,'podgo_nam_builtin.asm')
    used=[]
    for section in sections:
        start=section.addr
        if section.kind=='pm':
            base=start&~0x1ffff;start=base+(start-base)*3//2
            end=start+(3*len(section.words)+1)//2
        else:end=start+len(section.words)
        assert all(end<=a or b<=start for a,b in used),(section.kind,hex(section.addr))
        used.append((start,end))
    assert symbols['POD_NAM_FRAME32']==port.WRAPPER_PM
    return source,layout,sections,symbols
