using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

sealed class StudioWindow:Form {
 public const string GithubUrl="https://github.com/conr4d86?tab=repositories";
 readonly Color bg=Color.FromArgb(14,20,29),side=Color.FromArgb(11,16,23),accent=Color.FromArgb(0,220,102),muted=Color.LightSlateGray;
 readonly Button namTab=new StudioButton(),firmwareTab=new StudioButton();readonly Label usbStatus=new Label(),notice=new Label();readonly Panel content=new Panel();
 readonly ComboBox language=new ComboBox();
 public bool RestartLanguage{get;private set;}
 readonly NamWindow nam;readonly StudioFirmwarePane firmware;readonly bool preview;readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
 bool firmwareVisible,switching,closing,closedReady;Image mark;string failureNotice="";
 public bool IsBusy{get{return switching||nam.IsBusy||firmware.IsBusy;}}
 public bool FirmwareVisible{get{return firmwareVisible;}}
 public bool InstallEnabled{get{return firmware.InstallEnabled;}}
 public StudioWindow(bool offlinePreview=false,string initialNam=null,string savedBank=null){
  preview=offlinePreview;Text="POD Go NAM Manager · "+StudioApp.Version;Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);Size=new Size(1500,930);MinimumSize=new Size(1440,900);StartPosition=FormStartPosition.CenterScreen;BackColor=bg;ForeColor=Color.Gainsboro;Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
  var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=Padding.Empty};root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,204));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,34));Controls.Add(root);
  var sidebar=new Panel{Dock=DockStyle.Fill,BackColor=side,Margin=Padding.Empty};root.Controls.Add(sidebar);content.Dock=DockStyle.Fill;content.Margin=Padding.Empty;root.Controls.Add(content);
  using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("podgo-icon.png")){if(input!=null){using(var decoded=Image.FromStream(input))mark=new Bitmap(decoded);var picture=new PictureBox{Image=mark,SizeMode=PictureBoxSizeMode.Zoom,Location=new Point(20,22),Size=new Size(164,164)};sidebar.Controls.Add(picture);}else sidebar.Controls.Add(new Label{Text="POD GO NAM\nMANAGER",Location=new Point(20,42),Size=new Size(175,110),Font=new Font("Segoe UI",20,FontStyle.Bold),ForeColor=accent});}
  var version=new Label{Text=StudioApp.Version+"  ·  A2 LITE",Location=new Point(22,194),Size=new Size(172,22),ForeColor=muted,Font=new Font("Segoe UI",9)};sidebar.Controls.Add(version);
  language.Name="InterfaceLanguage";language.SetBounds(20,224,164,28);language.DropDownStyle=ComboBoxStyle.DropDownList;language.Items.AddRange(new object[]{"Espa\u00f1ol","English"});language.SelectedIndex=UiText.English?1:0;language.BackColor=side;language.ForeColor=Color.Gainsboro;sidebar.Controls.Add(language);language.SelectedIndexChanged+=async(s,e)=>await SwitchLanguage(language.SelectedIndex==1?"en":"es");
  NavButton(namTab,"Biblioteca NAM",266);NavButton(firmwareTab,"Firmware / recuperar",324);sidebar.Controls.AddRange(new Control[]{namTab,firmwareTab});
  var foot=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=170,Padding=new Padding(20,12,18,12),ColumnCount=1,RowCount=3};foot.RowStyles.Add(new RowStyle(SizeType.Absolute,30));foot.RowStyles.Add(new RowStyle(SizeType.Absolute,45));foot.RowStyles.Add(new RowStyle(SizeType.Percent,100));sidebar.Controls.Add(foot);
  foot.Controls.Add(new Label{Text="COMUNICACIÓN",Dock=DockStyle.Fill,ForeColor=accent,Font=new Font("Segoe UI",9,FontStyle.Bold)});usbStatus.Dock=DockStyle.Fill;foot.Controls.Add(usbStatus);notice.Dock=DockStyle.Fill;notice.ForeColor=muted;notice.Font=new Font("Segoe UI",9);foot.Controls.Add(notice);
  var credit=new LinkLabel{Name="Conr4dGithub",Text="Developed by Conr4d",Tag=GithubUrl,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",13,FontStyle.Bold),LinkColor=Color.White,VisitedLinkColor=Color.White,ActiveLinkColor=accent,LinkBehavior=LinkBehavior.HoverUnderline,Margin=Padding.Empty,AccessibleName="Abrir los repositorios de Conr4d en GitHub"};credit.LinkClicked+=(s,e)=>{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(GithubUrl){UseShellExecute=true});}catch(Exception ex){failureNotice=ex.Message;UpdateShell();}};root.Controls.Add(credit,0,1);root.SetColumnSpan(credit,2);
  nam=new NamWindow(initialNam,preview,savedBank,true){TopLevel=false,Dock=DockStyle.Fill,ShowInTaskbar=false};firmware=new StudioFirmwarePane(preview);content.Controls.Add(nam);content.Controls.Add(firmware);nam.Show();nam.Hide();firmwareVisible=true;firmware.Show();firmware.BringToFront();
  nam.ViewStateChanged+=UpdateShell;firmware.ViewStateChanged+=UpdateShell;namTab.Click+=async(s,e)=>await SelectSectionAsync(false);firmwareTab.Click+=async(s,e)=>await SelectSectionAsync(true);
  timer.Interval=300;timer.Tick+=(s,e)=>UpdateShell();timer.Start();FormClosing+=OnClosing;UpdateShell();
 }
 void NavButton(Button b,string title,int y){b.Text=title;b.SetBounds(12,y,180,48);b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;b.TextAlign=ContentAlignment.MiddleLeft;b.Padding=new Padding(10,0,0,0);b.Font=new Font("Segoe UI",10,FontStyle.Bold);b.ForeColor=Color.Gainsboro;b.Cursor=Cursors.Hand;b.UseVisualStyleBackColor=false;}
 void UpdateShell(){if(IsDisposed||nam==null||firmware==null)return;bool busy=IsBusy||closing;language.Enabled=namTab.Enabled=firmwareTab.Enabled=!busy;namTab.BackColor=!firmwareVisible?Color.FromArgb(30,57,51):side;firmwareTab.BackColor=firmwareVisible?Color.FromArgb(30,57,51):side;namTab.ForeColor=!firmwareVisible?accent:Color.Silver;firmwareTab.ForeColor=firmwareVisible?accent:Color.Silver;
  string owner=StudioUsbGate.Owner;usbStatus.Text=owner.Length>0?owner+"\nUSB reservado":nam.IsOnline?"Biblioteca consultada\nUSB libre entre operaciones":"USB sin abrir";usbStatus.ForeColor=owner.Length>0?Color.Turquoise:Color.Gainsboro;
  notice.Text=failureNotice.Length>0?failureNotice:busy?"Operación en curso.\nEspera a que termine.":"";
 }
 public async Task<bool> SelectSectionAsync(bool showFirmware){if(showFirmware==firmwareVisible)return true;if(IsBusy||closing){notice.Text="Espera a que termine la operación actual.";return false;}failureNotice="";switching=true;UpdateShell();try{
  if(!preview){if(firmwareVisible)await firmware.ReleaseAsync();else await nam.ReleaseAsync();}
  firmwareVisible=showFirmware;nam.Visible=!showFirmware;firmware.Visible=showFirmware;if(showFirmware)firmware.BringToFront();else nam.BringToFront();return true;
 }catch(Exception e){failureNotice=e.Message;return false;}finally{switching=false;UpdateShell();}}
 async Task SwitchLanguage(string selected){if(selected==UiText.Language)return;if(IsBusy||closing){language.SelectedIndex=UiText.English?1:0;return;}switching=true;UpdateShell();try{if(!preview){await nam.ReleaseAsync();await firmware.ReleaseAsync();}UiText.Save(selected);RestartLanguage=true;closedReady=true;Close();}catch(Exception ex){failureNotice=ex.Message;language.SelectedIndex=UiText.English?1:0;}finally{switching=false;UpdateShell();}}
 async void OnClosing(object sender,FormClosingEventArgs e){if(closedReady)return;e.Cancel=true;if(IsBusy||closing){notice.Text="Espera al resultado de la operación antes de cerrar.";return;}closing=true;UpdateShell();try{if(!preview){await nam.ReleaseAsync();await firmware.ReleaseAsync();}closedReady=true;Close();}catch(Exception ex){closing=false;failureNotice=ex.Message;UpdateShell();}}
 public void PrepareFirmwarePreview(string path){firmware.PreparePreview(path);}
 public void SelectModelPreview(int model){nam.SelectPreviewModel(model);}
 public void Render(string path,bool showFirmware,int model=-1){if(!preview)throw new InvalidOperationException("La captura se realiza únicamente en modo local.");ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Location=new Point(-25000,-25000);Show();Application.DoEvents();SelectSectionAsync(showFirmware).GetAwaiter().GetResult();if(model>=0)nam.SelectPreviewModel(model);Application.DoEvents();CheckButtons(this);using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(Point.Empty,Size));bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}Hide();}
 static void CheckButtons(Control parent){foreach(Control c in parent.Controls){if(c.Visible&&c is Button&&!parent.ClientRectangle.Contains(c.Bounds))throw new InvalidOperationException("Botón fuera de su panel: "+c.Text);CheckButtons(c);}}
 protected override void Dispose(bool disposing){if(disposing){timer.Dispose();if(mark!=null)mark.Dispose();if(Icon!=null)Icon.Dispose();nam.Dispose();firmware.Dispose();}base.Dispose(disposing);}
}
