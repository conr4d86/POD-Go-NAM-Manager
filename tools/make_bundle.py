"""Developer build: bundle the custom HXF and locally installed engine.

The end-user download is already bundled; it never asks for stock firmware
when selecting CF NAM Conr4d. This script does not open the SDK or USB.
"""
from pathlib import Path
import sys,os,json,hashlib,zipfile
ROOT=Path(__file__).resolve().parents[1]
custom=Path(sys.argv[1]);sha=lambda b:hashlib.sha256(b).hexdigest()
data=custom.read_bytes()
assert sha(data)=='d3fc10a52d71920e60627c39cf67c88d9b9116137f42642321914677e7002eae'
vendor=Path(os.environ.get('PODGO_LINE6_CENTRAL',str(Path(os.environ.get('ProgramFiles(x86)','C:/Program Files (x86)'))/'Line6/Line 6 Central')))
assert (vendor/'EngineCLIWrapper.dll').is_file(),'Developer build requires a local Line 6 Central engine'
files={'engine/'+p.name:p.read_bytes() for p in vendor.glob('*.dll')}
for name in ('res','PresentationScripts'):
 for p in (vendor/name).rglob('*'):
  if p.is_file():files['engine/'+p.relative_to(vendor).as_posix()]=p.read_bytes()
if (vendor/'L6ConfigAmplifi.xml').exists():files['engine/L6ConfigAmplifi.xml']=(vendor/'L6ConfigAmplifi.xml').read_bytes()
for name in ('msvcp140.dll','vcruntime140.dll'):
 p=Path(os.environ.get('WINDIR','C:/Windows'))/'SysWOW64'/name
 if p.exists():files['engine/'+name]=p.read_bytes()
files['custom/POD_GO_2.50_NAM_Conr4d_v8_MODEL_NAMES_EXPERIMENTAL.hxf']=data
out=ROOT/'build/runtime.zip';out.parent.mkdir(parents=True,exist_ok=True)
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED,compresslevel=1) as z:
 for name,b in sorted(files.items()):z.writestr(name,b)
 z.writestr('manifest.json',json.dumps([dict(path=n,sha256=sha(b)) for n,b in sorted(files.items())]))
print('Custom firmware and engine bundled. No official HXF included.')
