using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

sealed class StudioButton:Button {
 bool marked;
 public bool Rounded,Glow,Segment;public bool ShowCheck=true;
 public bool Marked{get{return marked;}set{if(marked==value)return;marked=value;Invalidate();}}
 static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 protected override void OnPaint(PaintEventArgs e){float scale=StudioVisual.Scale;Color green=Color.FromArgb(0,220,102);if(Segment){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.Clear(Parent.BackColor);if(marked)using(var path=Round(new RectangleF(0,0,Width-1,Height-1),4*scale)){using(var brush=new SolidBrush(Color.FromArgb(30,57,51)))e.Graphics.FillPath(brush,path);using(var pen=new Pen(green,1))e.Graphics.DrawPath(pen,path);}TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,marked?green:ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);return;}if(!Rounded&&Enabled&&!marked){base.OnPaint(e);return;}e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.Clear(Parent==null?BackColor:Parent.BackColor);var bounds=ClientRectangle;if(Rounded){int inset=Glow&&Enabled?Math.Max(3,(int)(5*scale)):1;var face=new RectangleF(inset,inset,Math.Max(1,Width-2*inset-1),Math.Max(1,Height-2*inset-1));if(Glow&&Enabled)for(int i=inset;i>0;i--)using(var path=Round(new RectangleF(face.X-i,face.Y-i,face.Width+i*2,face.Height+i*2),9*scale))using(var pen=new Pen(Color.FromArgb(20,64,224,208),2))e.Graphics.DrawPath(pen,path);using(var path=Round(face,9*scale)){using(var brush=new SolidBrush(marked?Color.FromArgb(30,57,51):BackColor))e.Graphics.FillPath(brush,path);using(var pen=new Pen(Enabled?Color.FromArgb(126,245,221):Color.FromArgb(83,105,127),Math.Max(1,2*scale)))e.Graphics.DrawPath(pen,path);}}else{e.Graphics.Clear(marked?Color.FromArgb(30,57,51):BackColor);ControlPaint.DrawBorder(e.Graphics,ClientRectangle,marked?green:FlatAppearance.BorderColor,ButtonBorderStyle.Solid);}bounds.Inflate(-(int)(8*scale),-(int)(3*scale));if(marked&&ShowCheck)bounds.Width-=(int)(23*scale);TextRenderer.DrawText(e.Graphics,Text,Font,bounds,marked?Color.Gainsboro:Enabled?ForeColor:Color.FromArgb(135,151,166),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);if(marked&&ShowCheck)using(var font=new Font("Segoe UI Symbol",Math.Max(7,12*scale),FontStyle.Bold))TextRenderer.DrawText(e.Graphics,"✓",font,new Rectangle(ClientSize.Width-(int)(29*scale),0,(int)(25*scale),ClientSize.Height),green,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
}
sealed class StudioStepLabel:Label {
 protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float s=StudioVisual.Scale;int diameter=(int)(29*s),y=Math.Max(0,(Height-diameter)/2);string title=Text.TrimStart(),digits=new string(title.TakeWhile(char.IsDigit).ToArray());int number;string step=int.TryParse(digits,out number)?number.ToString():digits;title=title.Substring(digits.Length).Trim();using(var pen=new Pen(Color.Turquoise,Math.Max(1,s)))e.Graphics.DrawEllipse(pen,1,y,diameter,diameter);using(var glyph=new GraphicsPath()){glyph.AddString(step,Font.FontFamily,(int)Font.Style,16*s,PointF.Empty,StringFormat.GenericTypographic);var ink=glyph.GetBounds();using(var transform=new Matrix()){transform.Translate(1+diameter/2f-(ink.Left+ink.Width/2f),y+diameter/2f-(ink.Top+ink.Height/2f));glyph.Transform(transform);}using(var brush=new SolidBrush(ForeColor))e.Graphics.FillPath(brush,glyph);}TextRenderer.DrawText(e.Graphics,title,Font,new Rectangle((int)(41*s),0,Math.Max(1,Width-(int)(41*s)),Height),ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}
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

sealed class StudioLanguagePanel:Panel {protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);float s=StudioVisual.Scale;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float r=6*s,d=r*2;var b=new RectangleF(0,0,Width-1,Height-1);using(var path=new GraphicsPath()){path.AddArc(b.X,b.Y,d,d,180,90);path.AddArc(b.Right-d,b.Y,d,d,270,90);path.AddArc(b.Right-d,b.Bottom-d,d,d,0,90);path.AddArc(b.X,b.Bottom-d,d,d,90,90);path.CloseFigure();using(var pen=new Pen(Color.FromArgb(0,110,51),1))e.Graphics.DrawPath(pen,path);}using(var pen=new Pen(Color.FromArgb(95,138,118),1))e.Graphics.DrawLine(pen,Width/2,Height/2-7*s,Width/2,Height/2+7*s);}}
