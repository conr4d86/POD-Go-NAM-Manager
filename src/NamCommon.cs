using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

sealed class StudioButton:Button {
 bool marked;
 public bool Marked{get{return marked;}set{if(marked==value)return;marked=value;Invalidate();}}
 protected override void OnPaint(PaintEventArgs e){if(Enabled&&!marked){base.OnPaint(e);return;}float scale=e.Graphics.DpiX/96F;Color green=Color.FromArgb(0,220,102);e.Graphics.Clear(marked?Color.FromArgb(30,57,51):BackColor);ControlPaint.DrawBorder(e.Graphics,ClientRectangle,marked?green:Color.FromArgb(52,67,80),ButtonBorderStyle.Solid);var bounds=ClientRectangle;bounds.Inflate(-(int)(8*scale),-(int)(3*scale));if(marked)bounds.Width-=(int)(23*scale);TextRenderer.DrawText(e.Graphics,Text,Font,bounds,marked?Color.Gainsboro:Color.FromArgb(135,151,166),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);if(marked)using(var font=new Font("Segoe UI Symbol",12,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,"✓",font,new Rectangle(ClientSize.Width-(int)(29*scale),0,(int)(25*scale),ClientSize.Height),green,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
}

sealed class NamDeviceState {
 public string Name,Version;public string[] Names;public PodIrSlot[] Bank;
 public string Summary{get{return Name+" · "+Names.Count(n=>!string.IsNullOrWhiteSpace(n))+" posiciones ocupadas · "+Names.Count(n=>string.IsNullOrWhiteSpace(n))+" libres";}}
}
static class NamGuards {
 public static bool SameSlot(PodIrSlot a,PodIrSlot b){return a!=null&&b!=null&&a.Index==b.Index&&a.Used==b.Used&&a.Name==b.Name&&a.Samples==b.Samples&&a.Checksum==b.Checksum;}
 public static int FreeSlot(string[] names){if(names==null||names.Length!=128)throw new IOException("Banco IR no válido");for(int n=0;n<128;n++)if(string.IsNullOrWhiteSpace(names[n]))return n;throw new IOException("No hay un espacio IR libre para otro NAM");}
 public static void CheckOtherApps(){foreach(var p in Process.GetProcesses())using(p){string n=p.ProcessName;if(p.Id==Process.GetCurrentProcess().Id)continue;if(n.Equals("POD Go Edit",StringComparison.OrdinalIgnoreCase)||n.Equals("Line 6 Central",StringComparison.OrdinalIgnoreCase)||n.StartsWith("Catalyst",StringComparison.OrdinalIgnoreCase)||n.StartsWith("PODGo",StringComparison.OrdinalIgnoreCase))throw new IOException("Cierra "+n+" antes de conectar.");}}
}
