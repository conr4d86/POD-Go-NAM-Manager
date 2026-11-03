using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Text;

public sealed class FirmwareFile {
 public const int Product=2162695;
 public const string OfficialHash="9ac67fdd55f8b8feb8c21f231eaafb5c6eb7883a8218430c9c4a23567c2f34d1";
 public const string WithdrawnV4="537f5c5c21f1dcf2f39dbda19dfe557256e8e9be802efb32a30a221bc724fee5";
 public const string WithdrawnPayload="d495b6aee4af85473eedc574a141d3752ebc50f506393cd6289a8ad165c5ec09";
 public string PathName,Hash,Version; public long Bytes; public bool IsOfficial,IsWithdrawn;
 public int ImageCount; public byte[] Inflated; public string Summary;
 static readonly uint[] CrcTable=MakeCrc();
 static readonly int[] Capacities={16777216,1048576,524288,1048576,16248832,25600,51200,102400,16777216,235520,2360320,81920,81920,268959744,268959744,235520,578027520};
 static uint[] MakeCrc(){var t=new uint[256];for(uint i=0;i<256;i++){uint c=i;for(int j=0;j<8;j++)c=(c&1)!=0?0xedb88320U^(c>>1):c>>1;t[i]=c;}return t;}
 public static string Sha(byte[] b){using(var h=SHA256.Create())return Hex(h.ComputeHash(b));}
 public static string Hex(byte[] b){return BitConverter.ToString(b).Replace("-","").ToLowerInvariant();}
 public static uint BE(byte[] b,int p){Need(p>=0&&p<=b.Length-4,"Lectura fuera del archivo");return ((uint)b[p]<<24)|((uint)b[p+1]<<16)|((uint)b[p+2]<<8)|b[p+3];}
 public static uint LE(byte[] b,int p){Need(p>=0&&p<=b.Length-4,"Cabecera incompleta");return BitConverter.ToUInt32(b,p);}
 static void Need(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
 static uint Adler(byte[] b){uint a=1,s=0;foreach(byte v in b){a=(a+v)%65521;s=(s+a)%65521;}return (s<<16)|a;}
 public static FirmwareFile Read(string path){
  long length=new FileInfo(path).Length;Need(length>=128&&length<=128*1024*1024,"Tamaño HXF no admitido");byte[] file=File.ReadAllBytes(path);Need(file.Length==length,"El archivo cambió durante la lectura");
  Need(Encoding.ASCII.GetString(file,0,4)=="FORM"&&Encoding.ASCII.GetString(file,8,4)=="L6FF","No es un HXF L6FF");
  Need(BE(file,4)==file.Length-8,"Longitud FORM incorrecta");
  byte[] head=null,compressed=null;var regions=new List<uint[]>();int chunks=0;
  for(int p=12;p<file.Length;){Need(p<=file.Length-8,"Chunk incompleto");int n=checked((int)BE(file,p+4));Need(n>=0&&n<=file.Length-p-8,"Longitud de chunk incorrecta");string id=Encoding.ASCII.GetString(file,p,4);
   if(id=="HEAD"){Need(head==null&&n==52,"HEAD duplicado o incorrecto");head=new byte[n];Buffer.BlockCopy(file,p+8,head,0,n);}
   else if(id=="dinf"){Need(n==16,"dinf incorrecto");regions.Add(new[]{BE(file,p+8),BE(file,p+12),BE(file,p+16),BE(file,p+20)});}
   else if(id=="data"){Need(compressed==null,"data duplicado");compressed=new byte[n];Buffer.BlockCopy(file,p+8,compressed,0,n);}
   else throw new InvalidDataException("Chunk desconocido: "+id);
   p=checked(p+8+n+(n&1));Need(p<=file.Length,"Padding de chunk incorrecto");chunks++;
  }
  Need(chunks==4&&head!=null&&compressed!=null&&regions.Count==2,"Estructura HXF POD Go inesperada");
  Need(BE(head,8)==Product,"El archivo no corresponde a POD Go");
  int wanted=checked((int)BE(head,16));Need(wanted>0&&wanted<=128*1024*1024,"Salida descomprimida fuera de límite");
  Need(compressed.Length>=6&&(compressed[0]&15)==8&&(((compressed[0]<<8)|compressed[1])%31)==0&&(compressed[1]&32)==0,"Cabecera zlib incorrecta");
  byte[] raw;
  using(var source=new MemoryStream(compressed,2,compressed.Length-6))using(var deflate=new DeflateStream(source,CompressionMode.Decompress))using(var output=new MemoryStream()){
   byte[] block=new byte[65536];int n;while((n=deflate.Read(block,0,block.Length))>0){Need(output.Length+n<=wanted,"La expansión excede HEAD");output.Write(block,0,n);}raw=output.ToArray();
  }
  Need(raw.Length==wanted,"Longitud descomprimida incorrecta");Need(Adler(raw)==BE(compressed,compressed.Length-4),"Adler32 zlib incorrecto");
  using(var md5=MD5.Create()){byte[] sum=md5.ComputeHash(raw);for(int i=0;i<16;i++)Need(sum[i]==head[20+i],"MD5 de HEAD incorrecto");}
  Need(raw.Length>=24&&Encoding.ASCII.GetString(raw,0,4)=="L6IA"&&BE(raw,4)==2&&BE(raw,8)==1,"Archivo L6IA POD Go no admitido");
  int count=checked((int)BE(raw,20));Need(count==17,"Se esperaban 17 imágenes POD Go");int at=24;
  for(int image=0;image<count;image++){
   Need(at<=raw.Length-36,"Cabecera de imagen truncada");Need(BE(raw,at)==image&&BE(raw,at+12)==4,"Índice/unidad de imagen incorrectos");
   Need(BE(raw,at+4)==0x02500000,"Esta versión de la app admite la base POD Go 2.50");
   int n=checked((int)BE(raw,at+16)*4);int start=at+36;Need(n>=512&&n<=raw.Length-start&&BE(raw,at+32)==0,"Imagen/mapa truncado o no admitido");
   Need(n-512<=Capacities[image],"La imagen "+image+" supera su partición de POD Go");
   if(image==1){byte[] boot=new byte[n];Buffer.BlockCopy(raw,start,boot,0,n);Need(Sha(boot)=="4a3716ca7b2ce71061f7e20db9754c10f952caf28634bd926f1365bc4532305c","Cargador de arranque distinto del original 2.50");}
   uint s1=0,s2=0;unchecked{for(int k=start;k<start+n;k+=4){s1+=BE(raw,k);s2+=s1;}}
   Need(s1==BE(raw,at+20)&&s2==BE(raw,at+24),"Fletcher incorrecto en imagen "+image);
   Need(BE(raw,start)==0x56112603&&BE(raw,start+12)==n-512,"Wrapper incorrecto en imagen "+image);
   uint crc=0xffffffff;for(int k=start+512;k<start+n;k+=4)for(int j=3;j>=0;j--)crc=CrcTable[(crc^raw[k+j])&255]^(crc>>8);
   Need((crc^0xffffffff)==BE(raw,start+8),"CRC32 incorrecto en imagen "+image);at=start+n;
  }
  Need(regions[0][0]==0&&regions[0][2]==0&&regions[0][3]==at,"Región L6IA incorrecta");
  Need(regions[1][0]==5&&regions[1][2]==at&&regions[1][3]==94224&&at+94224==raw.Length,"Cola P34Updater incorrecta");
  byte[] updater=new byte[94224];Buffer.BlockCopy(raw,at,updater,0,updater.Length);Need(Sha(updater)=="40ae8197fef8364e74da00eaf5aa7099a1dd2bb194c3a071c8cce8b6db39403b","Actualizador de recuperación distinto del original 2.50");
  string hash=Sha(file);uint version=BE(raw,28);
  return new FirmwareFile{PathName=System.IO.Path.GetFullPath(path),Hash=hash,Bytes=file.Length,Inflated=raw,ImageCount=count,
   Version=string.Format("{0:X2}.{1:X2}.{2:X2}",version>>24,(version>>16)&255,(version>>8)&255),IsOfficial=hash==OfficialHash,IsWithdrawn=hash==WithdrawnV4||Sha(raw)==WithdrawnPayload,
   Summary="17 imágenes · MD5, Adler32, CRC32 y Fletcher correctos"};
 }
 public static string PreserveBackup(string source,string directory){
  long length=new FileInfo(source).Length;Need(length>64&&length<32*1024*1024,"Backup fuera de límite");byte[] data=File.ReadAllBytes(source);Need(data.Length==length,"El backup cambió durante la lectura");
  Need(LE(data,0)==0x4c364641&&LE(data,24)==Product,"El PGB no corresponde a POD Go");
  string hash=Sha(data);Directory.CreateDirectory(directory);string target=System.IO.Path.Combine(directory,"PODGo-"+hash.Substring(0,16)+".pgb");
  if(!File.Exists(target))File.WriteAllBytes(target,data);Need(Sha(File.ReadAllBytes(target))==hash,"No se pudo verificar la copia del backup");return target;
 }
}
