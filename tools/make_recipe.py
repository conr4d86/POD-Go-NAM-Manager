"""Create a compact XOR delta from a verified local source rebuild."""
from pathlib import Path
import argparse,base64,hashlib,json,struct,zlib
from build_firmware import ROOT,STOCK,TARGET_RAW,chunks,parse_records
sha=lambda b:hashlib.sha256(b).hexdigest()
def create(stock,custom):
 old=Path(stock).read_bytes();new=Path(custom).read_bytes();assert sha(old)==STOCK
 def raw(file):return zlib.decompress(next(data for tag,data in chunks(file) if tag==b'data'))
 a,b=raw(old),raw(new);assert sha(b)==TARGET_RAW
 _,before,_=parse_records(a);_,after,_=parse_records(b);records=[]
 for x,y in zip(before,after):
  p=a[x['header_offset']:x['end_exclusive']];q=b[y['header_offset']:y['end_exclusive']]
  mask=bytes(q[i]^(p[i] if i<len(p) else 0) for i in range(len(q))) if p!=q else b''
  records.append(dict(index=x['index'],source_size=len(p),source_sha256=sha(p),target_size=len(q),target_sha256=sha(q),mask=base64.b64encode(zlib.compress(mask,9)).decode() if mask else ''))
 tail=a[before[-1]['end_exclusive']:];assert tail==b[after[-1]['end_exclusive']:]
 recipe=dict(schema='PODGoNAM.XorPatch.v1',product=2162695,stock_sha256=STOCK,stock_payload_sha256=sha(a),target_payload_sha256=sha(b),target_size=len(b),tail_sha256=sha(tail),records=records)
 out=ROOT/'firmware/patch.json';out.write_text(json.dumps(recipe,separators=(',',':')));print('Verified source delta:',out.stat().st_size,'bytes')
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('stock');p.add_argument('custom');a=p.parse_args();create(a.stock,a.custom)
