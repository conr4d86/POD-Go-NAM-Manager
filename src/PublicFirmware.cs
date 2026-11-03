using System;using System.IO;using System.IO.Compression;using System.Linq;using System.Text;using System.Reflection;using System.Collections.Generic;using System.Security.Cryptography;using System.Web.Script.Serialization;

// Public builds contain only bounded extension deltas. The user supplies the
// exact official HXF; no official images or native DLLs are redistributed.
static class PublicFirmware {
 public const string PayloadHash="7f45301113ef6adbd859ee2edb33157884ce935279e908b627fe208731087b47";
 public sealed class Record{public int index{get;set;}public int source_size{get;set;}public string source_sha256{get;set;}public int target_size{get;set;}public string target_sha256{get;set;}public string mask{get;set;}}
 public sealed class Recipe{public string schema{get;set;}public string stock_sha256{get;set;}public string stock_payload_sha256{get;set;}public string target_payload_sha256{get;set;}public int target_size{get;set;}public int product{get;set;}public Record[] records{get;set;}public string tail_sha256{get;set;}}
 public static string Home{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PODGoNamManager","firmware");}}
 public static string Official{get{return Path.Combine(Home,"POD_Go_2.50_OFICIAL.hxf");}}
 public static string Custom{get{return Path.Combine(Home,"POD_GO_NAM_Conr4d_v8.hxf");}}
 public static bool HasOfficial{get{return File.Exists(Official)&&FirmwareFile.Sha(File.ReadAllBytes(Official))==FirmwareFile.OfficialHash;}}
 public static void ImportOfficial(string path){var file=FirmwareFile.Read(path);if(!file.IsOfficial)throw new InvalidDataException("Selecciona el HXF oficial POD Go 2.50 exacto.");Directory.CreateDirectory(Home);if(!HasOfficial)AtomicWrite(Official,File.ReadAllBytes(path));}
 static void AtomicWrite(string path,byte[] data){Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllBytes(temp,data);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
 static void Need(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
 static void BE(byte[] b,int p,uint v){b[p]=(byte)(v>>24);b[p+1]=(byte)(v>>16);b[p+2]=(byte)(v>>8);b[p+3]=(byte)v;}
 static byte[] Slice(byte[] b,int at,int n){Need(at>=0&&n>=0&&at<=b.Length-n,"Delta fuera de límites");var result=new byte[n];Buffer.BlockCopy(b,at,result,0,n);return result;}
 static uint Adler(byte[] b){uint a=1,s=0;foreach(byte v in b){a=(a+v)%65521;s=(s+a)%65521;}return(s<<16)|a;}
 public static bool IsCustom(FirmwareFile file){return file!=null&&file.Inflated!=null&&FirmwareFile.Sha(file.Inflated)==PayloadHash;}
 static byte[] InflateMask(byte[] packed,int maximum){Need(packed.Length>=6,"Parche comprimido truncado");using(var input=new MemoryStream(packed,2,packed.Length-6))using(var zip=new DeflateStream(input,CompressionMode.Decompress))using(var output=new MemoryStream()){byte[] buffer=new byte[65536];int n;while((n=zip.Read(buffer,0,buffer.Length))>0){Need(output.Length+n<=maximum,"El parche excede su limite");output.Write(buffer,0,n);}return output.ToArray();}}
 public static byte[] Apply(byte[] stock,byte[] patch){
  var recipe=new JavaScriptSerializer{MaxJsonLength=4*1024*1024}.Deserialize<Recipe>(Encoding.UTF8.GetString(patch));Need(recipe.schema=="PODGoNAM.XorPatch.v1"&&recipe.product==FirmwareFile.Product&&recipe.stock_sha256==FirmwareFile.OfficialHash&&FirmwareFile.Sha(stock)==recipe.stock_sha256,"Base oficial incorrecta");
  var chunks=new List<KeyValuePair<string,byte[]>>();for(int p=12;p<stock.Length;){int n=checked((int)FirmwareFile.BE(stock,p+4));chunks.Add(new KeyValuePair<string,byte[]>(Encoding.ASCII.GetString(stock,p,4),Slice(stock,p+8,n)));p+=8+n+(n&1);}
  byte[] compressed=chunks.Single(c=>c.Key=="data").Value,raw;using(var input=new MemoryStream(compressed,2,compressed.Length-6))using(var zip=new DeflateStream(input,CompressionMode.Decompress))using(var output=new MemoryStream()){zip.CopyTo(output);raw=output.ToArray();}
  Need(FirmwareFile.Sha(raw)==recipe.stock_payload_sha256&&recipe.records.Length==17,"Payload oficial o receta incorrectos");byte[] target;int at=24;
  using(var result=new MemoryStream()){result.Write(raw,0,24);for(int index=0;index<17;index++){
   var record=recipe.records[index];int length=checked(36+(int)FirmwareFile.BE(raw,at+16)*4);byte[] source=Slice(raw,at,length);Need(record.index==index&&record.source_size==length&&record.target_size>0&&record.target_size<=64*1024*1024&&FirmwareFile.Sha(source)==record.source_sha256,"Imagen base distinta de la receta");
   byte[] rewritten;
   if(string.IsNullOrEmpty(record.mask))rewritten=source;else{rewritten=InflateMask(Convert.FromBase64String(record.mask),record.target_size);Need(rewritten.Length==record.target_size,"Longitud de parche incorrecta");for(int q=0;q<rewritten.Length;q++)if(q<source.Length)rewritten[q]^=source[q];}
   Need(rewritten.Length==record.target_size&&FirmwareFile.Sha(rewritten)==record.target_sha256,"Imagen reconstruida distinta");result.Write(rewritten,0,rewritten.Length);at+=length;

  }byte[] tail=Slice(raw,at,raw.Length-at);Need(FirmwareFile.Sha(tail)==recipe.tail_sha256,"Actualizador base distinto");result.Write(tail,0,tail.Length);target=result.ToArray();}
  Need(target.Length==recipe.target_size&&FirmwareFile.Sha(target)==recipe.target_payload_sha256&&recipe.target_payload_sha256==PayloadHash,"Firmware reconstruido distinto del objetivo");
  byte[] zlib;using(var memory=new MemoryStream()){memory.WriteByte(0x78);memory.WriteByte(0x9c);using(var deflate=new DeflateStream(memory,CompressionLevel.Optimal,true))deflate.Write(target,0,target.Length);uint sum=Adler(target);byte[] end=new byte[4];BE(end,0,sum);memory.Write(end,0,4);zlib=memory.ToArray();}
  using(var output=new MemoryStream()){output.Write(new byte[12],0,12);foreach(var chunk in chunks){byte[] data=(byte[])chunk.Value.Clone();if(chunk.Key=="HEAD"){BE(data,16,(uint)target.Length);using(var md5=MD5.Create())Buffer.BlockCopy(md5.ComputeHash(target),0,data,20,16);}else if(chunk.Key=="dinf"){int region=checked((int)FirmwareFile.BE(data,0));if(region==0)BE(data,12,(uint)(target.Length-94224));else if(region==5)BE(data,8,(uint)(target.Length-94224));}else if(chunk.Key=="data")data=zlib;byte[] header=new byte[8];Buffer.BlockCopy(Encoding.ASCII.GetBytes(chunk.Key),0,header,0,4);BE(header,4,(uint)data.Length);output.Write(header,0,8);output.Write(data,0,data.Length);if((data.Length&1)!=0)output.WriteByte(0);}byte[] file=output.ToArray();Buffer.BlockCopy(Encoding.ASCII.GetBytes("FORM"),0,file,0,4);BE(file,4,(uint)(file.Length-8));Buffer.BlockCopy(Encoding.ASCII.GetBytes("L6FF"),0,file,8,4);return file;}
 }
 public static string BuildCustom(){Need(HasOfficial,"Selecciona antes el HXF oficial POD Go 2.50.");byte[] patch;using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("patch.json"))using(var memory=new MemoryStream()){Need(input!=null,"Falta la receta del CF");input.CopyTo(memory);patch=memory.ToArray();}byte[] file=Apply(File.ReadAllBytes(Official),patch);string temporary=Custom+"."+Guid.NewGuid().ToString("N")+".verify.hxf";Directory.CreateDirectory(Home);File.WriteAllBytes(temporary,file);try{var validated=FirmwareFile.Read(temporary);Need(IsCustom(validated),"El CF no coincide con el objetivo verificado");AtomicWrite(Custom,file);}finally{File.Delete(temporary);}return Custom;}
}
