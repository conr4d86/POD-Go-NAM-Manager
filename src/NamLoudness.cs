using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections.Generic;

// Local, deterministic A2 Lite level comparison, not an ADC calibration or LUFS
// measurement. Every model and the reference receive the same shaped probe.
static class NamLoudness {
 public const string Method="A2Lite-shaped-noise-v1";
 static readonly object sync=new object();static readonly Dictionary<string,double> cache=new Dictionary<string,double>();static double? target;
 static readonly int[] dilations={1,3,7,17,41,101,239,1,3,7,17,41,101,239,1,13,1,3,7,17,41,101,239};
 sealed class Layer {public int Kernel,Dilation,Size,Position;public double[] Conv,Bias,Mixin,Post,PostBias,History;}
 static double[] Probe(){const int section=12000;var random=new Random(1855153);var b=new double[section*3];double low=0,high=0;double lp=1-Math.Exp(-2*Math.PI*4500/48000),hp=1-Math.Exp(-2*Math.PI*80/48000);for(int part=0;part<3;part++){double energy=0;for(int i=0;i<section;i++){double noise=random.NextDouble()*2-1;low+=lp*(noise-low);high+=hp*(low-high);double sample=low-high;b[part*section+i]=sample;energy+=sample*sample;}double gain=Math.Pow(10,(-24+6*part)/20.0)/Math.Sqrt(energy/section);for(int i=0;i<section;i++)b[part*section+i]*=gain;}return b;}
 static readonly double[] probe=Probe();
 public static double Measure(byte[] payload){string hash=NamCapture.Sha(payload);lock(sync){double value;if(cache.TryGetValue(hash,out value))return value;}var w=NamCapture.DecodePayload(payload);var layers=new Layer[23];int offset=3;for(int n=0;n<23;n++){int k=NamCapture.Kernels[n],d=dilations[n],conv=offset,bias=conv+9*k,mixin=bias+3,post=mixin+3,postBias=post+9;var layer=new Layer{Kernel=k,Dilation=d,Size=(k-1)*d+1,Conv=w.Skip(conv).Take(9*k).ToArray(),Bias=w.Skip(bias).Take(3).ToArray(),Mixin=w.Skip(mixin).Take(3).ToArray(),Post=w.Skip(post).Take(9).ToArray(),PostBias=w.Skip(postBias).Take(3).ToArray()};layer.History=new double[layer.Size*3];layers[n]=layer;offset=postBias+3;}
  double[] head=new double[48],x=new double[3],a=new double[3],skip=new double[3];int headPosition=0;double[] sums=new double[3],squares=new double[3];int[] counts=new int[3];
  for(int sample=0;sample<probe.Length;sample++){double input=probe[sample];for(int i=0;i<3;i++){x[i]=w[i]*input;skip[i]=0;}foreach(var l in layers){int position=l.Position;for(int j=0;j<3;j++)l.History[j*l.Size+position]=x[j];for(int i=0;i<3;i++){double value=l.Bias[i]+l.Mixin[i]*input;for(int j=0;j<3;j++){int history=j*l.Size,weight=(i*3+j)*l.Kernel;int p=position-(l.Kernel-1)*l.Dilation;if(p<0)p+=l.Size;for(int t=0;t<l.Kernel;t++){value+=l.Conv[weight+t]*l.History[history+p];p+=l.Dilation;if(p>=l.Size)p-=l.Size;}}a[i]=value>=0?value:value*.01;skip[i]+=a[i];}for(int i=0;i<3;i++)x[i]+=l.PostBias[i]+l.Post[i*3]*a[0]+l.Post[i*3+1]*a[1]+l.Post[i*3+2]*a[2];l.Position=(position+1)%l.Size;}
   for(int j=0;j<3;j++)head[j*16+headPosition]=skip[j];double output=w[1869];for(int j=0;j<3;j++)for(int t=0;t<16;t++)output+=w[1821+j*16+t]*head[j*16+(headPosition+1+t)%16];output*=w[1870];headPosition=(headPosition+1)%16;
   if(double.IsNaN(output)||double.IsInfinity(output)||Math.Abs(output)>1e12)throw new InvalidDataException("El análisis del NAM no es estable; ajusta la salida manualmente.");int section=sample/12000;if(sample%12000>=7200){sums[section]+=output;squares[section]+=output*output;counts[section]++;}
  }
  double result=0;for(int i=0;i<3;i++){double mean=sums[i]/counts[i],power=squares[i]/counts[i]-mean*mean;if(power<=1e-18)throw new InvalidDataException("El NAM no produce un nivel medible; ajusta la salida manualmente.");result+=10*Math.Log10(power)/3;}lock(sync){if(cache.Count>256)cache.Clear();cache[hash]=result;}return result;
 }
 public static double Target(){lock(sync){if(target.HasValue)return target.Value;}byte[] wave;using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("reference-nam.wav")){if(stream==null)throw new IOException("Falta el NAM de referencia del analizador");using(var memory=new MemoryStream()){stream.CopyTo(memory);wave=memory.ToArray();}}double value=Measure(NamCapture.AdjustPayload(NamCapture.AudioPayload(wave),0,-5));lock(sync)target=value;return value;}
 public static double OutputFor(byte[] payload){return Math.Max(-60,Math.Min(12,Math.Round(Target()-Measure(payload),1)));}
}
