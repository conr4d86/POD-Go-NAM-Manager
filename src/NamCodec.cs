// NAM A2 Lite carrier format ported from nocaseguy/hx-stomp-nam-lite
// tools/sharc/nam_lite.py, commit 2e801641df4a (GPL-3.0).
// No HX input-level calibration is applied to the POD Go.
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

sealed class NamCapture {
 public string Source,Name,Hash;public double OutputDb; public byte[] Wave;
 public static readonly int[] Kernels=Enumerable.Repeat(6,14).Concat(new[]{15,15}).Concat(Enumerable.Repeat(6,7)).ToArray();
 static readonly int[] Dilations=new[]{1,3,7,17,41,101,239,1,3,7,17,41,101,239,1,13,1,3,7,17,41,101,239};
 public static string Sha(byte[] b){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
 public static Dictionary<string,object> Dict(object o){var d=o as Dictionary<string,object>;if(d==null)throw new InvalidDataException("Estructura NAM incorrecta");return d;}
 static object[] Arr(object o){var a=o as object[];if(a!=null)return a;var x=o as ArrayList;if(x!=null)return x.ToArray();throw new InvalidDataException("Lista NAM incorrecta");}
 static double Number(object o){if(o is bool||o==null)throw new InvalidDataException("Número NAM incorrecto");double n=Convert.ToDouble(o,System.Globalization.CultureInfo.InvariantCulture);if(double.IsInfinity(n)||double.IsNaN(n))throw new InvalidDataException("NAM contiene valores no finitos");return n;}
 static void Need(bool ok,string why){if(!ok)throw new InvalidDataException("NAM no compatible: "+why);}
 static void EqualArray(Dictionary<string,object> d,string key,int[] expected){Need(d.ContainsKey(key)&&Arr(d[key]).Select(x=>(int)Number(x)).SequenceEqual(expected),key);}
 static float F(double v){return BitConverter.ToSingle(BitConverter.GetBytes((float)v),0);}
 public static NamCapture Read(string path,double outputDb=0){
  Need(!double.IsNaN(outputDb)&&!double.IsInfinity(outputDb)&&outputDb>=-60&&outputDb<=12,"nivel de salida fuera de rango");
  var bytes=File.ReadAllBytes(path);Need(bytes.Length<=8*1024*1024,"archivo demasiado grande");
  var json=new JavaScriptSerializer{MaxJsonLength=8*1024*1024};var root=Dict(json.DeserializeObject(System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\ufeff')));
  object[] models=Convert.ToString(root["architecture"])=="SlimmableContainer"?Arr(Dict(root["config"])["submodels"]).Select(s=>Dict(s)["model"]).ToArray():new object[]{root};
  var matches=models.Select(Dict).Where(m=>Convert.ToString(m["architecture"])=="WaveNet"&&Arr(Dict(m["config"])["layers"]).Length==1&&Number(Dict(Arr(Dict(m["config"])["layers"])[0])["channels"])==3).ToArray();
  Need(matches.Length==1,"se requiere un único submodelo A2 Lite de 3 canales");var model=matches[0];var config=Dict(model["config"]);var lc=Dict(Arr(config["layers"])[0]);
  object rate=model.ContainsKey("sample_rate")?model["sample_rate"]:root.ContainsKey("sample_rate")?root["sample_rate"]:null;Need(rate!=null&&Number(rate)==48000,"frecuencia distinta de 48 kHz o ausente");
  Need(Number(lc["input_size"])==1&&Number(lc["condition_size"])==1,"entrada/condición");EqualArray(lc,"kernel_sizes",Kernels);EqualArray(lc,"dilations",Dilations);
  Need(!config.ContainsKey("head")||config["head"]==null,"head adicional");var head=Dict(lc["head"]);Need(Number(head["out_channels"])==1&&Number(head["kernel_size"])==16&&Convert.ToBoolean(head["bias"]),"head");
  foreach(string key in new[]{"groups_input","groups_input_mixin","bottleneck"})if(lc.ContainsKey(key))Need(Number(lc[key])==(key=="bottleneck"?3:1),key);
  foreach(string key in lc.Keys.Where(k=>k.EndsWith("_film")||k=="head1x1"))if(lc[key]!=null)Need(!Convert.ToBoolean(Dict(lc[key])["active"]),key);
  if(lc.ContainsKey("layer1x1")){var l=Dict(lc["layer1x1"]);Need(Convert.ToBoolean(l["active"])&&Number(l["groups"])==1,"layer1x1");}
  Need(lc.ContainsKey("activation"),"activación ausente");var acts=Arr(lc["activation"]);Need(acts.Length==23,"activaciones");foreach(var a in acts){var ad=Dict(a);Need(Convert.ToString(ad["type"])=="LeakyReLU"&&Number(ad["negative_slope"])==0.01,"activación distinta de LeakyReLU 0.01");}
  if(lc.ContainsKey("gating_mode"))Need(Arr(lc["gating_mode"]).Length==23&&Arr(lc["gating_mode"]).All(g=>g==null||Convert.ToString(g)=="none"),"gating");
  if(lc.ContainsKey("secondary_activation"))Need(Arr(lc["secondary_activation"]).All(a=>a==null),"activación secundaria");
  var w=Arr(model["weights"]).Select(Number).ToArray();Need(w.Length==1871,"se requieren 1871 pesos");
  // The last serialized scalar is the output head scale. Keep all network
  // weights, its input calibration and the original .nam unchanged.
  w[1870]*=Math.Pow(10,outputDb/20);var wave=Encode(w);return new NamCapture{Source=Path.GetFullPath(path),Name=Path.GetFileNameWithoutExtension(path),Hash=Sha(bytes),Wave=wave,OutputDb=outputDb};
 }
 static List<double[]> Blocks(double[] w,out double[] head){int o=3;var blocks=new List<double[]>();foreach(int k in Kernels){int conv=o;o+=9*k;int bias=o;o+=3;int mixin=o;o+=3;int w1=o;o+=9;int b1=o;o+=3;var b=new List<double>();for(int i=0;i<3;i++){b.Add(w[bias+i]);b.Add(w[mixin+i]);}for(int t=0;t<k;t++)for(int j=0;j<3;j++)for(int i=0;i<3;i++)b.Add(w[conv+(i*3+j)*k+t]);for(int i=0;i<3;i++){b.Add(w[b1+i]);for(int j=0;j<3;j++)b.Add(w[w1+i*3+j]);}blocks.Add(b.ToArray());}head=new[]{w[o+48]}.Concat(Enumerable.Range(0,16).SelectMany(t=>Enumerable.Range(0,3).Select(j=>w[o+j*16+t]))).ToArray();return blocks;}
 public static byte[] Encode(double[] w){
  double[] head;var blocks=Blocks(w,out head);var stream=w.Take(3).Concat(head).Concat(new[]{w[1870]}).Concat(blocks.SelectMany(b=>b)).ToList();
  double[] unused;var fb=Blocks(w.Select(v=>(double)F(v)).ToArray(),out unused);var x=w.Take(3).Select(v=>F((double)F(v)*0.0)).ToArray();var hs=new float[3];var states=new List<float>();
  for(int n=0;n<23;n++){var b=fb[n];int k=Kernels[n];states.AddRange(x);var a=new float[3];for(int i=0;i<3;i++){float v=F(F(b[2*i]+F(b[2*i+1]*0.0))+0.0);for(int t=0;t<k;t++)for(int j=0;j<3;j++)v=F((double)v+F(b[6+(t*3+j)*3+i]*x[j]));a[i]=Math.Max(v,F((double)v*F(0.01)));}for(int i=0;i<3;i++)hs[i]=F((double)hs[i]+a[i]);int o=6+k*9;for(int i=0;i<3;i++){float u=(float)b[o+4*i];for(int j=0;j<3;j++)u=F((double)u+F((double)a[j]*b[o+4*i+1+j]));x[i]=F((double)x[i]+u);}}
  stream.AddRange(hs.Concat(states).Select(v=>(double)v/16));double peak=stream.Max(v=>Math.Abs(v)),scale=1;while(peak/scale>15.5)scale*=2;
  var samples=new[]{0.5,-0.5,3.0,scale,1871.0/1024,0.0,0.0,0.0}.Concat(stream.Select(v=>v/scale)).ToList();Need(samples.Count<2048&&samples.All(v=>!double.IsNaN(v)&&!double.IsInfinity(v)&&Math.Abs(v)<15.8489),"rango del portador");while(samples.Count<2048)samples.Add(0);
  using(var m=new MemoryStream())using(var b=new BinaryWriter(m)){b.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));b.Write(36+8192);b.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));b.Write(16);b.Write((short)3);b.Write((short)1);b.Write(48000);b.Write(192000);b.Write((short)4);b.Write((short)32);b.Write(System.Text.Encoding.ASCII.GetBytes("data"));b.Write(8192);foreach(double v in samples)b.Write((float)v);return m.ToArray();}
 }
 public static byte[] AudioPayload(byte[] wave){
  using(var r=new BinaryReader(new MemoryStream(wave))){Need(wave.Length>=44&&new string(r.ReadChars(4))=="RIFF","WAV");r.ReadInt32();Need(new string(r.ReadChars(4))=="WAVE","WAV");bool format=false;byte[] audio=null;while(r.BaseStream.Position+8<=r.BaseStream.Length){string id=new string(r.ReadChars(4));int n=r.ReadInt32();Need(n>=0&&r.BaseStream.Position+n<=r.BaseStream.Length,"WAV truncado");long next=r.BaseStream.Position+n+(n&1);if(id=="fmt "){Need(n>=16,"WAV fmt");format=r.ReadInt16()==3&&r.ReadInt16()==1&&r.ReadInt32()==48000;r.ReadInt32();format&=r.ReadInt16()==4&&r.ReadInt16()==32;}else if(id=="data")audio=r.ReadBytes(n);r.BaseStream.Position=next;}Need(format&&audio!=null&&audio.Length==8192,"el transporte no conserva 2048 float32 mono a 48 kHz");return audio;}
 }
 public static void VerifyCarrier(byte[] wanted,byte[] got){Need(AudioPayload(wanted).SequenceEqual(AudioPayload(got)),"el retorno USB alteró los datos NAM; no se activará");}
}
