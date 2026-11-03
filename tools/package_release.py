"""Prepare review ZIPs locally. Does not publish or access GitHub/USB."""
from pathlib import Path
import hashlib,json,re,zipfile

ROOT=Path(__file__).resolve().parents[1]
VERSION='0.7.2'
TOP={'.gitignore','.gitattributes','LICENSE','NOTICE','README.md','README.es.md',
     'requirements-dev.txt','build-windows.ps1','test-offline.ps1'}
DIRS={'.github','assets','docs','firmware','models','src','tests','third_party','tools'}
FORBIDDEN={'.hxf','.dfu','.dll','.exe','.pgb','.pgp','.bin','.pyc','.log'}
SHA=lambda data:hashlib.sha256(data).hexdigest()

def source_files():
    files=[]
    for path in sorted(ROOT.rglob('*')):
        if not path.is_file():continue
        rel=path.relative_to(ROOT)
        if rel.parts[0] not in DIRS and str(rel) not in TOP:continue
        if any(part in {'__pycache__','.git','build','dist'} for part in rel.parts):continue
        assert path.suffix.lower() not in FORBIDDEN,'Unexpected binary/private file: '+str(rel)
        if path.suffix.lower() in {'.cs','.py','.ps1','.md','.json','.txt','.yml','.yaml','.nam'}:
            data=path.read_text(encoding='utf-8-sig')
            assert not re.search(r'C:[/\\]Users[/\\]',data,re.I),'Local profile path: '+str(rel)
            assert not re.search(r'POD GO\s_\sCATALYST',data),'Private workspace reference: '+str(rel)
            assert not re.search(r'github_pat_[A-Za-z0-9_]{20,}|gh[pousr]_[A-Za-z0-9]{20,}|sk-[A-Za-z0-9]{32,}',data),'Credential-like text: '+str(rel)
        files.append((rel.as_posix(),path.read_bytes()))
    assert all(any(name==p for name,_ in files) for p in TOP)
    return files

def archive(path,files):
    with zipfile.ZipFile(path,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
        for name,data in sorted(files):
            info=zipfile.ZipInfo(name,date_time=(2026,11,3,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED
            z.writestr(info,data,compresslevel=9)

def main():
    dest=ROOT/'dist';dest.mkdir(exist_ok=True)
    files=source_files()
    inventory=json.dumps({name:SHA(data) for name,data in files},indent=2).encode()
    source=dest/f'PODGoNAMManager-{VERSION}-source-PREVIEW.zip'
    archive(source,files+[('SOURCE-SHA256.json',inventory)])
    exe=(ROOT/'build/app/PODGoNamManager.exe').read_bytes()
    assert exe[:2]==b'MZ'
    catalogue=json.loads((ROOT/'build/app/known-models.json').read_text(encoding='utf-8-sig'))
    assert catalogue==[],'Public catalogue must be empty'
    review=('POD Go NAM Manager '+VERSION+' - PREVIEW FOR HARDWARE REVIEW\n'
      'Spanish / English interface. Public runtime adapter needs manual validation.\n'
      'Read docs/INICIO_RAPIDO.md or docs/QUICKSTART.md before setup.\n'
      'Create a POD Go Edit backup first. Original POD Go 2.50 HXF is supplied by the user.\n'
      'Source archive is supplied alongside this binary archive. No complete HXF or vendor DLLs are bundled.\n')
    runtime=[('PODGoNamManager.exe',exe),('known-models.json',b'[]\n'),('PREVIEW.txt',review.encode())]
    runtime += [(name,data) for name,data in files if name in {'LICENSE','NOTICE','README.md','README.es.md'} or name.startswith('docs/') or name in {'assets/podgo-nam-manager.png','assets/Icon.JPG'}]
    binary=dest/f'PODGoNAMManager-{VERSION}-windows-PREVIEW.zip';archive(binary,runtime)
    report=dict(version=VERSION,status='PREPARED_FOR_EXPERIMENTAL_PRERELEASE',source_files=len(files),
      artifacts=[dict(file=p.name,bytes=p.stat().st_size,sha256=SHA(p.read_bytes())) for p in (source,binary)],
      public_executable_sha256=SHA(exe),contains_complete_hxf=False,contains_vendor_dlls=False,
      hardware_signoff_pending=True)
    (dest/'RELEASE-MANIFEST.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    (dest/'SHA256SUMS.txt').write_text(''.join(f"{a['sha256']}  {a['file']}\n" for a in report['artifacts']),encoding='utf-8')
    print(json.dumps(report,indent=2))

if __name__=='__main__':main()
