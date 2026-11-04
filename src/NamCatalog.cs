using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Web.Script.Serialization;

sealed class NamCatalogRecord {
 public string PayloadHash,Name,Source,SourceHash;public double OutputDb,InputDb;
}
sealed class NamCatalog {
 readonly string path;readonly Dictionary<string,NamCatalogRecord> entries=new Dictionary<string,NamCatalogRecord>();
 public NamCatalog(string file=null){path=file??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PODGoNamManager","library.json");Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"known-models.json"));Load(path);}
 void Load(string file){if(!File.Exists(file))return;try{foreach(var r in new JavaScriptSerializer().Deserialize<NamCatalogRecord[]>(File.ReadAllText(file)))if(r!=null&&r.PayloadHash!=null&&r.PayloadHash.Length==64&&r.PayloadHash.All(c=>"0123456789abcdef".Contains(char.ToLowerInvariant(c)))&&!string.IsNullOrWhiteSpace(r.Name)&&r.OutputDb>=-60&&r.OutputDb<=12&&r.InputDb>=-60&&r.InputDb<=12)entries[r.PayloadHash]=r;}catch(IOException){}catch(ArgumentException){}catch(InvalidOperationException){}}
 public NamCatalogRecord Find(string deviceName){if(!ManagedName(deviceName))return null;var found=entries.Values.Where(r=>r.PayloadHash.StartsWith(deviceName.Substring(4,12),StringComparison.OrdinalIgnoreCase)).ToArray();return found.Length==1?found[0]:null;}
 public static bool ManagedName(string name){return name!=null&&name.Length>=16&&name.Length<=31&&name.StartsWith("NAM_",StringComparison.Ordinal)&&name.Substring(4,12).All(c=>"0123456789abcdef".Contains(char.ToLowerInvariant(c)))&&(name.Length==16||name[16]==' '&&NamTitle.Valid(name.Substring(17)));}
 public static bool ValidCarrier(byte[] blob){return blob!=null&&blob.Length==8192&&BitConverter.ToSingle(blob,0)==0.5F&&BitConverter.ToSingle(blob,4)==-0.5F&&BitConverter.ToSingle(blob,8)==3F&&BitConverter.ToSingle(blob,16)==1871F/1024;}
 public void Remember(NamCapture c,string payloadHash){entries[payloadHash]=new NamCatalogRecord{PayloadHash=payloadHash,Name=c.Name,Source=c.Source,SourceHash=c.Hash,OutputDb=c.OutputDb,InputDb=c.InputDb};Directory.CreateDirectory(Path.GetDirectoryName(path));string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(entries.Values.ToArray()));if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}
 public string Friendly(PodIrSlot slot){var record=Find(slot.Name);return record!=null?record.Name:slot.Used?(ManagedName(slot.Name)?slot.Name.Length>17?slot.Name.Substring(17):"NAM sin ficha local":slot.Name.Length==0?"IR sin nombre":slot.Name):"Espacio libre";}
}
