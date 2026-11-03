"""POD Go 2.50: capture names in the Model value; title stays unchanged."""
import struct,zlib,re,keystone,capstone
SITE=0x28ca54f2
EXPECTED=bytes.fromhex("031e04da")
PID=0x24d
KS=keystone.Ks(keystone.KS_ARCH_ARM,keystone.KS_MODE_THUMB)
def assemble(text,address):return bytes(KS.asm(text,address)[0])
def aligned(data):return data+b"\x00\xbf"*(((-len(data))%4)//2)

def make_cave(base):
    # At the hook: r6=packed block; r5=physical knob; r8=17-byte value;
    # sp+0x8c=parameter index; sp+0x17=Cab half. Preserve all input regs.
    # Stock font 0 is pod15_regularFont. 120 px fits before Input's value;
    # move only this first value from x=48 to x=64 to retain its prefix.
    source='''
        push.w {r0-r7, r12, lr}
        sub sp, #24
        cmp r5, #0
        bne restore
        movs r4, #48
        cmp r0, #0
        blt position
        ldr.w r3, [r6, #1]
        movw r2, #589
        cmp r3, r2
        bne position
        ldrb.w r3, [sp, #87]
        cmp r3, #0
        bne position
        ldr.w r3, [sp, #204]
        cmp r3, #0
        bne position
        mov r1, r8
        movs r6, #0
        movs r7, #0
    parse:
        ldrb r2, [r1], #1
        cmp r2, #0
        beq parsed
        subs r2, #48
        cmp r2, #9
        bhi position
        adds r7, #1
        cmp r7, #3
        bhi position
        add.w r6, r6, r6, lsl #2
        add.w r6, r2, r6, lsl #1
        b parse
    parsed:
        cmp r7, #0
        beq position
        cmp r6, #128
        bhi position
        cmp r6, #0
        beq builtin_name
        subs r0, r6, #1
        add r1, sp, #16
        bl 0x28cbdf30
        cmp r0, #0
        blt position
        ldr r5, [sp, #16]
        ldrb r3, [r5]
        cbz r3, empty_name
        ldr r2, [r5]
        ldr r3, =0x5f4d414e
        cmp r2, r3
        bne ir_name
        movs r2, #4
    hash:
        ldrb r3, [r5, r2]
        subs r3, #48
        cmp r3, #9
        bls hex_ok
        subs r3, #49
        cmp r3, #5
        bhi ir_name
    hex_ok:
        adds r2, #1
        cmp r2, #16
        bne hash
        ldrb r3, [r5, #16]
        cmp r3, #0
        beq legacy_name
        cmp r3, #32
        bne ir_name
        adds r5, #17
        ldrb r3, [r5]
        cbz r3, legacy_name
        b prefix
    builtin_name:
        adr.w r5, builtin
        b prefix
    empty_name:
        adr.w r5, empty
        b prefix
    ir_name:
        adr.w r5, ir
        b prefix
    legacy_name:
        adr.w r5, legacy
    prefix:
        mov r7, r8
        mov r0, r6
        cmp r0, #100
        blo tens
        movs r2, #49
        strb r2, [r7], #1
        subs r0, #100
        b ten_digit
    tens:
        cmp r0, #10
        blo units
    ten_digit:
        movs r2, #0
    divide_ten:
        cmp r0, #10
        blo digit_ready
        subs r0, #10
        adds r2, #1
        b divide_ten
    digit_ready:
        adds r2, #48
        strb r2, [r7], #1
    units:
        adds r0, #48
        strb r0, [r7], #1
        movs r0, #46
        strb r0, [r7], #1
        movs r0, #32
        strb r0, [r7], #1
        sub.w r4, r7, r8
    copy:
        cmp r4, #16
        bhs copied
        ldrb r3, [r5], #1
        cmp r3, #32
        blo copied
        cmp r3, #126
        bhi copied
        strb r3, [r7], #1
        adds r4, #1
        b copy
    copied:
        movs r3, #0
        strb r3, [r7]
    measure:
        mov r0, sp
        mov r1, r8
        movs r2, #16
        movs r3, #0
        bl 0x28c7d680
        ldr r3, [sp]
        cmp r3, #120
        ble sized
        cmp r4, #6
        bls sized
        subs r4, #1
        movs r3, #0
        strb r3, [r8, r4]
        b measure
    sized:
        movs r4, #64
    position:
        ldr r0, =0x282c66ec
        ldr r3, [r0, #8]
        cmp r3, r4
        beq restore
        mov r1, r4
        ldr r2, [r0, #12]
        bl 0x28c7e370
    restore:
        add sp, #24
        pop.w {r0-r7, r12, lr}
        subs r3, r0, #0
        bge success
        b.w 0x28ca54f6
    success:
        b.w 0x28ca5500
        .align 2
    builtin:
        .asciz "Dev by Conr4d"
    empty:
        .asciz "Empty"
    ir:
        .asciz "IR"
    legacy:
        .asciz "NAM"
        .align 2
    '''
    # Keystone's Thumb relaxation can shift absolute BL destinations if a
    # forward conditional branch is widened on its second pass. Fix widths
    # up front and check every external branch independently after assembly.
    source=re.sub(r'(?m)^(\s*)(b(?:eq|ne|lt|hs|hi|lo|ls|ge|le)?)(\s+)',r'\1\2.w\3',source)
    code=aligned(assemble(source,base))
    cs=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB)
    external=[int(i.op_str[1:],16) for i in cs.disasm(code,base)
              if i.mnemonic in ('bl','b.w') and i.op_str.startswith('#')
              and not base<=int(i.op_str[1:],16)<base+len(code)]
    assert external==[0x28cbdf30,0x28c7d680,0x28c7e370,0x28ca54f6,0x28ca5500],list(map(hex,external))
    return code

def apply(raw,view,rows):
 from map_l6ia import parse_arm_segments,Memory
 arm=parse_arm_segments(view,rows[0]);memory=Memory(view,arm['segments']);image=arm['image_offset']
 body=bytearray(view[image:rows[0]['end_exclusive']]);seg=arm['segments'][6];start=seg['view_offset']-image;end=start+seg['bytes']
 assert len(body)-end==1 and body[end:]==b'\0'
 assert memory.read(0x28004750,2)==struct.pack('<H',PID)
 base=seg['load_address']+seg['bytes'];cave=make_cave(base)
 offset=memory.offset(SITE,4)-image;assert body[offset:offset+4]==EXPECTED
 body[offset:offset+4]=assemble('b.w '+hex(base),SITE)
 body[end:end]=cave;struct.pack_into('<3I',body,start-12,seg['bytes']+len(cave),seg['load_address'],end+len(cave))
 struct.pack_into('<I',body,4,len(body));struct.pack_into('<I',body,8,zlib.crc32(body[52:])&0xffffffff)
 return bytes(body)
