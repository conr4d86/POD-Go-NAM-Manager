using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;

sealed class StudioFirmwarePane:UserControl {
 readonly bool preview;readonly string records,logPath;readonly object logGate=new object();
 readonly Color bg=Color.FromArgb(14,20,29),card=Color.FromArgb(22,31,42),accent=Color.Turquoise,muted=Color.LightSlateGray;
 readonly Label device=new Label(),fileName=new Label(),state=new Label();readonly ToolTip details=new ToolTip();
 readonly TextBox journal=new TextBox();readonly ProgressBar progress=new ProgressBar();
 readonly Button connect=new StudioButton(),disconnect=new StudioButton(),refresh=new StudioButton(),identify=new StudioButton(),custom=new StudioButton(),official=new StudioButton(),backupButton=new StudioButton(),install=new StudioButton();
 readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
 EngineSession engine;FirmwareFile firmware;IDisposable lease;string backup="",noticeMessage="";Color noticeColor=Color.Turquoise;bool localBusy,releasing;
 public event Action ViewStateChanged;
 public bool IsBusy{get{return localBusy||releasing||engine!=null&&(engine.Busy||engine.IsAlive&&!engine.Running);}}
 public bool HasSession{get{return engine!=null&&engine.IsAlive;}}
 public string ConnectionText{get{return engine==null?"Sin conectar":engine.DeviceText;}}
 public bool InstallEnabled{get{return install.Enabled;}}
 public string StatusText{get{return state.Text;}}
 public bool ConnectionMarked{get{return ((StudioButton)connect).Marked;}}
 public bool CustomMarked{get{return ((StudioButton)custom).Marked;}}
 public bool OfficialMarked{get{return ((StudioButton)official).Marked;}}
 public bool BackupMarked{get{return ((StudioButton)backupButton).Marked;}}
 public StudioFirmwarePane(bool offlinePreview=false){
  preview=offlinePreview;AutoScaleMode=AutoScaleMode.None;Dock=DockStyle.Fill;BackColor=bg;ForeColor=Color.Gainsboro;Font=new Font("Segoe UI",10);
  records=Path.Combine(Path.GetTempPath(),"PODGoStudio-data",DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));logPath=Path.Combine(records,"firmware.log");if(!preview)Directory.CreateDirectory(records);
  var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=1,RowCount=6};
  foreach(int height in new[]{64,154,194,80,10})root.RowStyles.Add(new RowStyle(SizeType.Absolute,height));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(root);
  var header=TextLabel("Firmware y recuperación",26,Color.Gainsboro);root.Controls.Add(header);
  var connection=Card("01   CONECTAR LA POD GO");device.Dock=DockStyle.Top;device.Height=29;device.Text=preview?"Vista previa local · USB cerrado":"Sin conectar · inicio local";connection.Body.Controls.Add(device);
  var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=38,WrapContents=false};Style(connect,"Conectar POD Go",180);Style(disconnect,"Desconectar",125);Style(refresh,"Actualizar estado",155);Style(identify,"Identificar recuperación",210);buttons.Controls.AddRange(new Control[]{connect,disconnect,refresh,identify});connection.Body.Controls.Add(buttons);root.Controls.Add(connection);
  var columns=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,61));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,39));root.Controls.Add(columns);
  var fw=Card("02   ELEGIR FIRMWARE");fw.Margin=new Padding(0,0,14,0);
  var fwRows=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};fwRows.RowStyles.Add(new RowStyle(SizeType.Absolute,48));fwRows.RowStyles.Add(new RowStyle(SizeType.Percent,100));fw.Body.Controls.Add(fwRows);
  var selectors=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};Style(custom,"CF NAM Conr4d",175);Style(official,"Cargar Firmware Oficial…",240);selectors.Controls.AddRange(new Control[]{custom,official});fwRows.Controls.Add(selectors);
  fileName.Dock=DockStyle.Fill;fileName.Text="";fileName.AutoEllipsis=true;fwRows.Controls.Add(fileName);columns.Controls.Add(fw);
  var copies=Card("03   CARGA TU BACKUP");Style(backupButton,"Carga tu backup (.pgb)…",255);copies.Body.Controls.Add(backupButton);columns.Controls.Add(copies);
  var action=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(0,16,0,0)};action.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,292));action.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Style(install,"Selecciona un firmware",278);((StudioButton)install).Rounded=true;install.Height=44;action.Controls.Add(install);
  var progressRows=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(12,0,0,0)};progressRows.RowStyles.Add(new RowStyle(SizeType.Absolute,22));progressRows.RowStyles.Add(new RowStyle(SizeType.Percent,100));progress.Dock=DockStyle.Fill;progress.Maximum=1000;progressRows.Controls.Add(progress);state.Dock=DockStyle.Fill;state.ForeColor=muted;state.Padding=new Padding(0,5,0,0);state.Text="Conectar y seleccionar archivos no inicia la instalación.";progressRows.Controls.Add(state);action.Controls.Add(progressRows);root.Controls.Add(action);
  root.Controls.Add(new Panel{Dock=DockStyle.Fill});
  var logs=Card("REGISTRO DE LA SESIÓN");journal.Dock=DockStyle.Fill;journal.Multiline=true;journal.ReadOnly=true;journal.ScrollBars=ScrollBars.Both;journal.WordWrap=false;journal.BorderStyle=BorderStyle.None;journal.BackColor=card;journal.ForeColor=Color.LightSteelBlue;journal.Font=new Font("Consolas",9);logs.Body.Controls.Add(journal);var openLog=new StudioButton();Style(openLog,"Abrir registros",142);openLog.Dock=DockStyle.Right;openLog.Height=25;logs.Heading.Controls.Add(openLog);openLog.Click+=(s,e)=>{if(Directory.Exists(records))Process.Start("explorer.exe",records);};root.Controls.Add(logs);
  connect.Click+=(s,e)=>Connect();disconnect.Click+=async(s,e)=>{try{ClearNotice();await ReleaseAsync();}catch(Exception ex){Error(ex);}};refresh.Click+=(s,e)=>engine.Refresh();identify.Click+=(s,e)=>engine.IdentifyRecovery();
  custom.Click+=async(s,e)=>await LoadCustom();official.Click+=async(s,e)=>{using(var d=new OpenFileDialog{Filter="Firmware oficial POD Go (*.hxf)|*.hxf",Title="Cargar Firmware Oficial"})if(d.ShowDialog()==DialogResult.OK)await LoadFirmware(d.FileName);};
  backupButton.Click+=(s,e)=>{using(var d=new OpenFileDialog{Filter="Backup POD Go (*.pgb)|*.pgb",Title="Carga tu backup (.pgb)",InitialDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Line 6","POD Go","Backups")})if(d.ShowDialog()==DialogResult.OK)try{SetBackup(d.FileName);}catch(Exception ex){Error(ex);}};
  install.Click+=(s,e)=>{try{RequestInstall(notice=>notice.Confirm(FindForm()),(file,pgb)=>engine.Install(file,pgb));}catch(Exception ex){Error(ex);}UpdateUi();};
  timer.Interval=250;timer.Tick+=(s,e)=>UpdateUi();timer.Start();UpdateUi();
 }
 sealed class CardPanel:TableLayoutPanel {
  public readonly Panel Heading=new Panel(),Body=new Panel();
  public CardPanel(){Dock=DockStyle.Fill;ColumnCount=1;RowCount=2;Padding=new Padding(16,11,16,12);Margin=new Padding(0,0,0,12);BackColor=Color.FromArgb(22,31,42);RowStyles.Add(new RowStyle(SizeType.Absolute,44));RowStyles.Add(new RowStyle(SizeType.Percent,100));Heading.Dock=Body.Dock=DockStyle.Fill;Controls.Add(Heading);Controls.Add(Body);}
 }
 CardPanel Card(string title){var c=new CardPanel();var label=title.StartsWith("0")?new StudioStepLabel{Dock=DockStyle.Fill,Text=title,ForeColor=accent}:TextLabel(title,9,accent);if(title.StartsWith("0"))label.Font=new Font("Segoe UI",14,FontStyle.Bold);c.Heading.Controls.Add(label);return c;}
 Label TextLabel(string text,int size,Color color){return new Label{Dock=DockStyle.Fill,Text=text,ForeColor=color,Font=new Font("Segoe UI",size,size>20?FontStyle.Bold:FontStyle.Regular)};}
 void Style(Button b,string text,int width){b.Text=text;b.Width=width;b.Height=34;b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=Color.FromArgb(55,73,88);b.BackColor=Color.FromArgb(35,48,65);b.ForeColor=Color.Gainsboro;b.Cursor=Cursors.Hand;b.UseVisualStyleBackColor=false;}
 void Ui(Action action){try{if(IsDisposed)return;if(InvokeRequired)BeginInvoke(action);else action();}catch(InvalidOperationException){}}
 void Log(string message){string line=DateTime.Now.ToString("HH:mm:ss.fff")+" "+message;if(!preview)lock(logGate){try{File.AppendAllText(logPath,line+Environment.NewLine);}catch(IOException){}catch(UnauthorizedAccessException){}}Ui(()=>{if(journal.TextLength>250000)journal.Text=journal.Text.Substring(journal.TextLength-150000);journal.AppendText(line+Environment.NewLine);if(message.StartsWith("ERROR MOTOR ",StringComparison.Ordinal)||message.StartsWith("ERROR ",StringComparison.Ordinal))SetNotice("No se pudo completar la operación. Consulta el registro.",true);else if(message.StartsWith("ACTUALIZACIÓN FALLIDA:",StringComparison.Ordinal))SetNotice("Instalación fallida. Registro conservado.",true);else if(message.StartsWith("TRANSFERENCIA COMPLETADA.",StringComparison.Ordinal))SetNotice("Transferencia completada. Comprueba arranque y audio.",false);});}
 void SetNotice(string text,bool error){noticeMessage=text;noticeColor=error?Color.Salmon:accent;state.Text=text;state.ForeColor=noticeColor;}
 void ClearNotice(){noticeMessage="";state.ForeColor=muted;}
 void Error(Exception e){Log("ERROR: "+e.Message);SetNotice(e.Message,true);}
 void Connect(){try{if(preview||IsBusy||HasSession)return;ClearNotice();NamGuards.CheckOtherApps();lease=StudioUsbGate.Acquire("Firmware y recuperación");engine=new EngineSession(Log,()=>Ui(UpdateUi),value=>Ui(()=>{double pct=Math.Max(0,Math.Min(100,value));progress.Value=(int)(pct*10);state.Text="Instalando · "+pct.ToString("0.00")+" %";}));engine.Start();}catch(Exception ex){ReleaseLeaseIfClosed();Error(ex);}UpdateUi();}
 void ReleaseLeaseIfClosed(){if(lease!=null&&(engine==null||!engine.IsAlive&&engine.UsbClosed)){lease.Dispose();lease=null;}}
 public async Task ReleaseAsync(){
  if(localBusy||engine!=null&&engine.Busy)throw new InvalidOperationException("Espera a que termine la operación de firmware.");
  if(releasing)throw new InvalidOperationException("Se está cerrando la comunicación.");releasing=true;UpdateUi();
  try{var deadline=DateTime.UtcNow.AddSeconds(20);while(engine!=null&&engine.IsAlive){if(engine.Running)engine.Disconnect();if(DateTime.UtcNow>deadline)throw new IOException("El motor aún no ha confirmado su cierre. La comunicación sigue reservada.");await Task.Delay(100);}
   if(engine!=null&&!engine.UsbClosed)throw new IOException("No se confirmó el cierre USB del motor. Se mantiene bloqueada la otra conexión.");ReleaseLeaseIfClosed();engine=null;UpdateUi();
  }finally{releasing=false;UpdateUi();}
 }
 async Task LoadCustom(){ClearNotice();firmware=null;localBusy=true;UpdateUi();try{await Task.Run(()=>PortableRuntime.Prepare());await ReadFirmware(PortableRuntime.Custom);}catch(Exception ex){Error(ex);}finally{localBusy=false;UpdateUi();}}
 async Task LoadFirmware(string path){ClearNotice();firmware=null;localBusy=true;UpdateUi();try{await ReadFirmware(path,true);}catch(Exception ex){Error(ex);}finally{localBusy=false;UpdateUi();}}
 async Task ReadFirmware(string path,bool requireOfficial=false){firmware=null;fileName.Text="Verificando…";try{var file=await Task.Run(()=>FirmwareFile.Read(path));if(requireOfficial&&!file.IsOfficial)throw new InvalidDataException("Selecciona el firmware oficial POD Go 2.50.");ShowFirmware(file);Log("HXF validado SHA256="+file.Hash);}catch{fileName.Text="";throw;}}
 void ShowFirmware(FirmwareFile file){firmware=file;fileName.Text=file.IsOfficial?"Firmware oficial POD Go 2.50":"CF NAM Conr4d";}
 void SetBackup(string path){ClearNotice();backup=FirmwareFile.PreserveBackup(path,Path.Combine(records,"backups"));details.SetToolTip(backupButton,Path.GetFileName(path));Log("Backup conservado: "+backup);UpdateUi();}
 void RequestInstall(Func<InstallNotice,bool> confirm,Action<FirmwareFile,string> dispatch){
  if(!install.Enabled||firmware==null||engine==null)throw new InvalidOperationException("Falta completar la preparación.");
  var selected=firmware;var selectedBackup=backup;bool accepted;localBusy=true;UpdateUi();try{accepted=confirm(InstallNotice.For(selected));}finally{localBusy=false;UpdateUi();}
  if(!accepted){SetNotice("Operación cancelada.",false);return;}
  if(!ReferenceEquals(selected,firmware)||selectedBackup!=backup)throw new InvalidOperationException("La selección ha cambiado. Revísala antes de instalar.");
  ClearNotice();progress.Value=0;state.Text="Preparando la transferencia…";dispatch(selected,selectedBackup);
 }
 void UpdateUi(){if(IsDisposed)return;ReleaseLeaseIfClosed();bool active=engine!=null&&engine.Running,ready=engine!=null&&engine.TargetReady;bool busy=IsBusy||releasing,validFirmware=firmware!=null&&!firmware.IsWithdrawn,hasBackup=File.Exists(backup);connect.Enabled=!preview&&!busy&&!HasSession&&lease==null;disconnect.Enabled=!preview&&!busy&&HasSession;refresh.Enabled=identify.Enabled=!preview&&!busy&&active;identify.Visible=active&&!ready&&engine.RecoveryDetected; if(ready)identify.Enabled=false;custom.Enabled=official.Enabled=backupButton.Enabled=!busy;device.Text=preview?"Vista previa local · USB cerrado":(ready?"✓ ":"")+ConnectionText;device.ForeColor=ready?accent:Color.Gainsboro;
  ((StudioButton)connect).Marked=ready;((StudioButton)custom).Marked=validFirmware&&firmware.Hash==PortableRuntime.CustomHash;((StudioButton)official).Marked=validFirmware&&firmware.IsOfficial;((StudioButton)backupButton).Marked=hasBackup;
  install.Enabled=!preview&&!busy&&ready&&validFirmware&&hasBackup;((StudioButton)install).Glow=install.Enabled;install.Invalidate();install.Text=firmware==null?"Selecciona un firmware":firmware.IsOfficial?"Restaurar oficial 2.50":"Instalar CF NAM";install.BackColor=install.Enabled?accent:Color.FromArgb(35,48,65);install.ForeColor=install.Enabled?bg:muted;
  if(noticeMessage.Length>0){state.Text=noticeMessage;state.ForeColor=noticeColor;}
  else if(engine==null||!engine.Busy){state.ForeColor=install.Enabled?accent:muted;
   if(releasing)state.Text="Cerrando comunicación…";
   else if(localBusy)state.Text="Verificando la preparación…";
   else if(preview)state.Text="Vista previa local. La conexión USB está deshabilitada.";
   else if(engine!=null&&engine.IsAlive&&!active)state.Text="Abriendo el motor integrado…";
   else if(active&&!ready)state.Text="Motor iniciado. Buscando POD Go…";
   else if(!ready)state.Text="Conecta la POD Go para continuar.";
   else if(firmware==null)state.Text="POD Go conectada. Elige el firmware que quieres instalar.";
   else if(firmware.IsWithdrawn)state.Text="Firmware retirado. Elige otro archivo.";
   else if(!hasBackup)state.Text="POD Go conectada · firmware elegido · falta conservar tu backup PGB.";
   else state.Text="Todo preparado. Pulsa «"+install.Text+"» para comenzar.";
  }
  if(ViewStateChanged!=null)ViewStateChanged();}
 public void PreparePreview(string firmwarePath){if(!preview)throw new InvalidOperationException();ShowFirmware(FirmwareFile.Read(firmwarePath));journal.Text="Vista previa local. Archivo HXF validado; motor y USB sin abrir.\r\nLa instalación requiere conexión identificada, copia PGB y pulsar Instalar.";UpdateUi();}
 protected override void Dispose(bool disposing){if(disposing){timer.Dispose();details.Dispose();}base.Dispose(disposing);}
}
