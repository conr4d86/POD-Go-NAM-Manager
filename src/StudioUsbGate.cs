using System;
using System.IO;

// A file lease is independent of thread affinity. The SDK's STA can therefore
// finish before the UI releases the lease; another Studio process is excluded.
static class StudioUsbGate {
 static readonly object sync=new object();static Lease current;
 static string lockPath=Path.Combine(Path.GetTempPath(),"PODGoStudio-device.lock");
 public static string Owner {get{lock(sync)return current==null?"":current.Owner;}}
 public static IDisposable Acquire(string owner){lock(sync){
  if(current!=null)throw new IOException("La comunicación está ocupada por "+current.Owner+".");
  FileStream file;try{file=new FileStream(lockPath,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){throw new IOException("Otra instancia de POD Go Studio está usando la comunicación.");}
  current=new Lease(owner,file);return current;
 }}
 sealed class Lease:IDisposable {
  public readonly string Owner;FileStream file;
  public Lease(string owner,FileStream handle){Owner=owner;file=handle;}
  public void Dispose(){lock(sync){if(file==null)return;file.Dispose();file=null;if(current==this)current=null;}}
 }
 // Tests use a separate lock file, never the user's communication lease.
 public static void UseTestLock(string path){lock(sync){if(current!=null)throw new InvalidOperationException();lockPath=path;}}
}
