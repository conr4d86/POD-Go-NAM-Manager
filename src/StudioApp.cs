using System;
using System.IO;
using System.Windows.Forms;

static class StudioApp {
 public const string Version="0.8.0";
 [STAThread]public static int Main(string[] args){try{
  if(args.Length==3&&args[0]=="--layout-self-test")return LayoutTests.Run(args[1],args[2]);
  if(args.Length==3&&args[0]=="--release-self-test")return ReleaseTests.Run(args[1],args[2]);
  if(args.Length==3&&args[0]=="--nam-self-test"){Directory.CreateDirectory(args[2]);StudioUsbGate.UseTestLock(Path.Combine(args[2],"fixture-device.lock"));return NamTests.Run(args[1],args[2]);}
  if(args.Length==5&&args[0]=="--firmware-self-test"){args[0]="--self-test";return SelfTests.Run(args);}
  if(args.Length==5&&args[0]=="--builtin-self-test")return BuiltinTests.Run(args[1],args[2],args[3],args[4]);
  if(args.Length==2&&args[0]=="--lease-child"){StudioUsbGate.UseTestLock(args[1]);try{using(StudioUsbGate.Acquire("Fixture hijo"))return 0;}catch(IOException){return 17;}}
  UiText.Load();
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  if(args.Length==5&&args[0]=="--offline-preview"){using(var window=new StudioWindow(true,args[1],args[2])){window.Text+=" · Vista previa";window.PrepareFirmwarePreview(args[3]);window.SelectSectionAsync(args[4]=="firmware").GetAwaiter().GetResult();Application.Run(window);}return 0;}
  if(args.Length==4&&args[0]=="--studio-self-test")return StudioTests.Run(args[1],args[2],args[3]);
  if(args.Length==4&&args[0]=="--render-nam"){using(var window=new StudioWindow(true,args[1],args[2]))window.Render(args[3],false,53);return 0;}
  if(args.Length==3&&args[0]=="--render-firmware"){using(var window=new StudioWindow(true)){window.PrepareFirmwarePreview(args[1]);window.Render(args[2],true);}return 0;}
  if(args.Length>0&&!(args.Length==2&&args[0]=="--prepare-nam"))throw new ArgumentException("Opción no reconocida");
  while(true){UiText.Load();using(var window=new StudioWindow(false,args.Length==2?args[1]:null)){if(args.Length==2)window.SelectSectionAsync(false).GetAwaiter().GetResult();Application.Run(window);if(!window.RestartLanguage)break;}}return 0;
 }catch(Exception e){if(args.Length>0){string error=Path.Combine(Path.GetTempPath(),"PODGoStudio-command-error.log");File.AppendAllText(error,DateTime.Now.ToString("O")+" "+e+Environment.NewLine);Console.Error.WriteLine(e);}else MessageBox.Show(e.Message,"POD Go NAM Manager",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}}
}
