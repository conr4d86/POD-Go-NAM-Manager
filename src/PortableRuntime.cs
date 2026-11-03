using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Collections.Generic;
using System.Web.Script.Serialization;

static class PortableRuntime {
 public static string Root;
 public const string LegacyCustomHash="9dc704fa81fd43b2cd0d737d95cadab01d38bdbb831f9811c09e42f651f09a4e";
 public const string CustomHash="d3fc10a52d71920e60627c39cf67c88d9b9116137f42642321914677e7002eae";
 public static string Custom {get{return Path.Combine(Root,"custom","POD_GO_2.50_NAM_Conr4d_v8_MODEL_NAMES_EXPERIMENTAL.hxf");}}
 public sealed class Entry {public string path{get;set;}public string sha256{get;set;}}
 public static void Prepare(){
  if(Root!=null)return;byte[] packed;
  using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("runtime.zip"))using(var m=new MemoryStream()){if(input==null)throw new IOException("Falta el motor integrado");input.CopyTo(m);packed=m.ToArray();}
  string root=Path.Combine(Path.GetTempPath(),"PODGoCustomFirmware-runtime",FirmwareFile.Sha(packed).Substring(0,24));Directory.CreateDirectory(root);
  using(var zip=new ZipArchive(new MemoryStream(packed),ZipArchiveMode.Read)){
   Entry[] entries;using(var reader=new StreamReader(zip.GetEntry("manifest.json").Open()))entries=new JavaScriptSerializer().Deserialize<Entry[]>(reader.ReadToEnd());
   var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var e in entries){
    if(e.path.StartsWith("official/",StringComparison.OrdinalIgnoreCase))throw new IOException("El firmware oficial debe seleccionarlo el usuario; no forma parte del paquete.");
    string target=Path.GetFullPath(Path.Combine(root,e.path.Replace('/',Path.DirectorySeparatorChar)));
    if(!target.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!names.Add(target))throw new IOException("Ruta integrada no válida");
    if(File.Exists(target)&&FirmwareFile.Sha(File.ReadAllBytes(target))==e.sha256)continue;
    byte[] data;using(var input=zip.GetEntry(e.path).Open())using(var m=new MemoryStream()){input.CopyTo(m);data=m.ToArray();}
    if(FirmwareFile.Sha(data)!=e.sha256)throw new IOException("Motor integrado alterado: "+e.path);
    Directory.CreateDirectory(Path.GetDirectoryName(target));string temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";
    File.WriteAllBytes(temp,data);if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);
   }
  }
  // Vendor engine locates res/ beside the host image. The build already stages
  // these resources; portable copies reconstruct them only when needed.
  string engine=Path.Combine(root,"engine");
  foreach(string directory in new[]{"res","PresentationScripts"}){
   string origin=Path.Combine(engine,directory);if(!Directory.Exists(origin))continue;
   foreach(string source in Directory.GetFiles(origin,"*",SearchOption.AllDirectories)){
    string dest=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,directory,source.Substring(origin.Length+1));
    if(File.Exists(dest)&&FirmwareFile.Sha(File.ReadAllBytes(dest))==FirmwareFile.Sha(File.ReadAllBytes(source)))continue;
    Directory.CreateDirectory(Path.GetDirectoryName(dest));File.Copy(source,dest,true);
   }
  }
  string custom=Path.Combine(root,"custom","POD_GO_2.50_NAM_Conr4d_v8_MODEL_NAMES_EXPERIMENTAL.hxf");if(File.Exists(custom)&&FirmwareFile.Sha(File.ReadAllBytes(custom))!=CustomHash)throw new IOException("Custom integrado incorrecto");
  Root=root;
 }
}
