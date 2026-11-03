"""Wrap translated C# literals at build time; source stays readable Spanish.

Only dictionary-listed literals are wrapped. Comments, native keys, constants,
file paths and control IDs are left intact. Runtime language selection is ES/EN.
"""
from pathlib import Path
import json,re
TOKEN=re.compile(r'//[^\r\n]*|/\*[\s\S]*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"')
def prepare(root,output):
 root=Path(root);output=Path(output);output.mkdir(parents=True,exist_ok=True)
 dictionary=json.loads((root/'assets/en.json').read_text(encoding='utf-8'))
 files=[]
 for source in sorted((root/'src').glob('*.cs')):
  text=source.read_text(encoding='utf-8-sig')
  if source.name!='UiText.cs':
   def replace(match):
    token=match.group()
    if not token.startswith('"'):return token
    try:value=json.loads(token.replace('\\0','\\u0000'))
    except ValueError:return token
    return 'UiText.T('+token+')' if value in dictionary else token
   text=TOKEN.sub(replace,text)
  target=output/source.name;target.write_text(text,encoding='utf-8');files.append(target)
 return files
