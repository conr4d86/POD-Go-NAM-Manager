using System;using System.IO;

// Uses the user's installed Line 6 Central files. No vendor binaries embedded.
static class PortableRuntime {
 public static string Root;
 public const string LegacyCustomHash="9dc704fa81fd43b2cd0d737d95cadab01d38bdbb831f9811c09e42f651f09a4e";
 public const string CustomHash="d3fc10a52d71920e60627c39cf67c88d9b9116137f42642321914677e7002eae";
 public static string Custom{get{return PublicFirmware.Custom;}}
 public static bool IsCustom(FirmwareFile file){return file!=null&&(file.Hash==CustomHash||PublicFirmware.IsCustom(file));}
 static void Copy(string from,string to){Directory.CreateDirectory(Path.GetDirectoryName(to));if(!File.Exists(to)||FirmwareFile.Sha(File.ReadAllBytes(from))!=FirmwareFile.Sha(File.ReadAllBytes(to)))File.Copy(from,to,true);}
 static void Tree(string from,string to){if(!Directory.Exists(from))return;foreach(string file in Directory.GetFiles(from,"*",SearchOption.AllDirectories))Copy(file,Path.Combine(to,file.Substring(from.Length+1)));}
 public static void Prepare(){
  if(Root!=null)return;
  string installed=Environment.GetEnvironmentVariable("PODGO_LINE6_CENTRAL");if(string.IsNullOrWhiteSpace(installed))installed=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Line6","Line 6 Central");
  string wrapper=Path.Combine(installed,"EngineCLIWrapper.dll");if(!File.Exists(wrapper))throw new IOException("Install Line 6 Central before connecting for firmware installation.");
  string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PODGoNamManager","runtime",FirmwareFile.Sha(File.ReadAllBytes(wrapper)).Substring(0,16));string engine=Path.Combine(root,"engine");
  foreach(string file in Directory.GetFiles(installed,"*.dll"))Copy(file,Path.Combine(engine,Path.GetFileName(file)));
  foreach(string name in new[]{"msvcp140.dll","vcruntime140.dll"}){string file=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),name);if(File.Exists(file))Copy(file,Path.Combine(engine,name));}
  foreach(string directory in new[]{"res","PresentationScripts"}){Tree(Path.Combine(installed,directory),Path.Combine(engine,directory));Tree(Path.Combine(installed,directory),Path.Combine(AppDomain.CurrentDomain.BaseDirectory,directory));}
  foreach(string name in new[]{"L6ConfigAmplifi.xml","AssetDefs.plist"})if(File.Exists(Path.Combine(installed,name)))Copy(Path.Combine(installed,name),Path.Combine(engine,name));
  Root=root;
 }
}
