"""Rebuild POD Go NAM v8 from the user's stock 2.50 HXF and source code.

No USB/updater calls. Generated vendor-derived files remain in ignored build/.
Requires Python 3.10+ and keystone-engine (for the bounded ARM Model value hook).
"""
from pathlib import Path
import argparse,hashlib,json,struct,sys,zlib
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'firmware/port'))
from map_l6ia import wordswap,parse_records
from inspect_model_catalog import parse_catalog
from podgo_module_resources import ModuleResources
import nam_podgo_lab as port
from nam_podgo_builtin import build_builtin_kernel
from nam_podgo_dsp import build_dsp
from nam_podgo_common import build_common
from nam_podgo_catalog import build_catalog,build_arm_metadata
from nam_capture import read_capture
import nam_titles
SHA=lambda b:hashlib.sha256(b).hexdigest()
STOCK='9ac67fdd55f8b8feb8c21f231eaafb5c6eb7883a8218430c9c4a23567c2f34d1'
TARGET_RAW='7f45301113ef6adbd859ee2edb33157884ce935279e908b627fe208731087b47'

def chunks(file):
 assert file[:4]==b'FORM' and file[8:12]==b'L6FF' and len(file)-8==struct.unpack_from('>I',file,4)[0]
 at=12;out=[]
 while at<len(file):
  n=struct.unpack_from('>I',file,at+4)[0];assert n<=len(file)-at-8
  out.append((file[at:at+4],file[at+8:at+8+n]));at+=8+n+(n&1)
 assert at==len(file);return out
def fletcher(b):
 a=c=0
 for (word,) in struct.iter_unpack('>I',b):a=(a+word)&0xffffffff;c=(c+a)&0xffffffff
 return a,c
def archive(raw,replacements):
 _,rows,_=parse_records(raw);out=bytearray(raw[:24])
 for row in rows:
  if row['index'] not in replacements:out+=raw[row['header_offset']:row['end_exclusive']];continue
  le=replacements[row['index']];assert len(le)%4==0
  sub=bytearray(raw[row['payload_offset']:row['payload_offset']+512]);struct.pack_into('>2I',sub,8,zlib.crc32(le)&0xffffffff,len(le))
  payload=sub+wordswap(le);header=bytearray(raw[row['header_offset']:row['payload_offset']]);struct.pack_into('>3I',header,16,len(payload)//4,*fletcher(payload));out+=header+payload
 out+=raw[rows[-1]['end_exclusive']:];return bytes(out)
def hxf(parts,raw):
 body=bytearray(b'L6FF');region=0;_,rows,_=parse_records(raw);end=rows[-1]['end_exclusive']
 for tag,data in parts:
  if tag==b'HEAD':
   data=bytearray(data);struct.pack_into('>I',data,16,len(raw));data[20:36]=hashlib.md5(raw).digest()
  elif tag==b'dinf':
   data=bytearray(data);struct.pack_into('>I',data,12 if region==0 else 8,end);region+=1
  elif tag==b'data':data=zlib.compress(raw,9)
  body+=tag+struct.pack('>I',len(data))+data+(b'\0' if len(data)&1 else b'')
 return b'FORM'+struct.pack('>I',len(body))+body
def build(stock,output):
 file=Path(stock).read_bytes();assert SHA(file)==STOCK,'Use the exact POD Go 2.50 stock HXF'
 parts=chunks(file);raw=zlib.decompress(next(data for tag,data in parts if tag==b'data'));view=wordswap(raw);_,rows,_=parse_records(raw);cat=parse_catalog(view,rows[3]);library=ModuleResources(view,rows[4],cat)
 work=ROOT/'build/firmware-local';(work/'artifacts').mkdir(parents=True,exist_ok=True)
 port.ROOT=work;port.checked_stock=lambda:(raw,view,rows,cat,library)
 (work/'artifacts/pod_go_dsp.ldr').write_bytes(raw[rows[2]['payload_offset']+512:rows[2]['end_exclusive']])
 _,weights,_=read_capture(ROOT/'models/Developed by Conr4d.nam');weights=list(weights);weights[-1]*=10**(-5/20)
 kernel=build_builtin_kernel(weights);dsp,asm,layout,_=build_dsp(kernel);common,_=build_common(layout);catalog,_=build_catalog(True);arm,_=build_arm_metadata();control,_,_=port.host_control_component(True)
 image=bytearray(view[rows[4]['payload_offset']+512:rows[4]['end_exclusive']]);directory=struct.unpack_from('<6I',image)[3]
 for index,data in ((0,common),(206,control)):
  count,offset=struct.unpack_from('<2I',image,4*(directory+2*index));assert len(data)<=count*4
  image[4*offset:4*offset+len(data)]=data;struct.pack_into('<I',image,4*(directory+2*index),len(data)//4)
 intermediate=archive(raw,{0:arm,2:wordswap(dsp),3:catalog,4:bytes(image)});v=wordswap(intermediate);_,r,_=parse_records(intermediate);arm=nam_titles.apply(intermediate,v,r);target=archive(intermediate,{0:arm})
 assert SHA(target)==TARGET_RAW,'Source reconstruction differs from the reviewed v8 payload'
 result=hxf(parts,target);output=Path(output);output.parent.mkdir(parents=True,exist_ok=True);output.write_bytes(result)
 (work/'nam-kernel.asm').write_text(asm);(work/'source-build.json').write_text(json.dumps(dict(status='PASS_SOURCE_REBUILD_OFFLINE',firmware_sha256=SHA(result),payload_sha256=SHA(target),USB_opened=False),indent=2))
 return file,raw,result,target
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('stock');parser.add_argument('--output',default=str(ROOT/'build/POD_GO_NAM_Conr4d_v8.hxf'));args=parser.parse_args();_,_,file,_=build(args.stock,args.output);print('Source rebuild verified:',SHA(file))
