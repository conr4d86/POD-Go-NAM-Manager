using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Text;
// Offline protocol peer, never attached to a USB interface.
sealed class NamFixtureDevice {public byte[] Stored;public string Name="";public int Slot=51;}
sealed class NamWireFixture:IPodBulk {
 readonly Queue<byte[]> replies=new Queue<byte[]>();readonly List<byte> staged=new List<byte>();readonly Queue<byte[]> stream=new Queue<byte[]>();readonly NamFixtureDevice device;int unit,credit;public int DropCreditAt;public bool Occupied,StoredBlobTransaction;public byte[] Stored{get{return device.Stored;}}public int DataBytes,DataFrames;public int TransferSessions;public bool OneTransferOnly;
 public NamWireFixture(NamFixtureDevice shared=null){device=shared??new NamFixtureDevice();}
 static byte[] ReplyBody(object o){var b=PodFrame.Tlv(6,PodMsg.Encode(o));b[0]=0;return b;}
 void Reply(PodFrame f,byte[] b,byte cmd=4){replies.Enqueue(new PodFrame{Src=f.Dst,Dst=f.Src,Seq=(byte)(f.Seq+19),Cmd=cmd,Body=b}.Encode());}
 public void Write(byte[] bytes){var f=PodFrame.Decode(bytes).Single();if(f.Magic==0x28){Reply(f,new byte[]{0,2,0,0},2);return;}if(f.Cmd==2){Reply(f,new byte[0],2);return;}if(f.Body.Length==0){if(stream.Count>0)Reply(f,stream.Dequeue());else Reply(f,new byte[0],8);return;}
  var envelope=PodMsg.Envelope(f.Body);if(envelope!=null&&envelope.ContainsKey(100)&&Convert.ToInt32(envelope[100])==254&&staged.Count>0){staged.Clear();Reply(f,ReplyBody(PodMsg.Map(102,envelope[102],103,0,104,PodMsg.Map())));return;}if(staged.Count>0||(f.Body.Length>=8&&BitConverter.ToUInt32(f.Body,4)>496)){
   staged.AddRange(f.Body);DataBytes+=f.Body.Length;DataFrames++;unit+=f.Body.Length;int total=staged.Count>=8?8+BitConverter.ToInt32(staged.ToArray(),4):int.MaxValue;
   if(staged.Count==total){var all=PodMsg.Envelope(staged.ToArray());var target=(Dictionary<long,object>)all[101];device.Stored=(byte[])target[110];device.Slot=Convert.ToInt32(target[112]);device.Name=Encoding.UTF8.GetString((byte[])target[109]).TrimEnd('\0');staged.Clear();}
   if(unit>=512||Stored!=null){unit=0;credit++;if(credit!=DropCreditAt)Reply(f,new byte[0],8);}return;
  }
  if(envelope==null){Reply(f,Encoding.ASCII.GetBytes("P34Main"));return;}int op=Convert.ToInt32(envelope[100]);var targetMap=(Dictionary<long,object>)envelope[101];object payload=PodMsg.Map();int slot=targetMap.ContainsKey(112)?Convert.ToInt32(targetMap[112]):51;
  if(op==255&&++TransferSessions>1&&OneTransferOnly)throw new IOException("fixture: stale session reused");
  bool usedHere=slot==device.Slot&&(Stored!=null||Occupied);if(op==12)payload=PodMsg.Map(112,slot,113,usedHere&&Stored!=null?PodWireSession.Checksum(Stored):0,109,Encoding.UTF8.GetBytes(usedHere?device.Name:""),114,usedHere?1:0,115,3,123,false,124,false,125,0);
  if(op==11){if(Stored==null)throw new IOException("fixture empty");byte[] full=ReplyBody(PodMsg.Map(102,StoredBlobTransaction?1003:envelope[102],103,0,104,Stored));if(StoredBlobTransaction){full[2]=0;full[3]=0x28;}for(int at=256;at<full.Length;at+=256)stream.Enqueue(full.Skip(at).Take(256).ToArray());Reply(f,full.Take(256).ToArray());return;}
  if(op==254)staged.Clear();Reply(f,ReplyBody(PodMsg.Map(102,envelope[102],103,0,104,payload)));
 }
 public byte[] Read(){if(replies.Count==0){System.Threading.Thread.Sleep(1);return null;}return replies.Dequeue();}public void ReadTimeout(uint ms){}public void Dispose(){}
}
