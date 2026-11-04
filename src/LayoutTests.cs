using System;using System.IO;using System.Drawing;using System.Linq;using System.Collections;using System.Collections.Generic;using System.Reflection;using System.Windows.Forms;using System.Web.Script.Serialization;

// Renders synthetic bank data, never opens the native engine or USB.
static class LayoutTests {
 static void Need(bool ok,string message){if(!ok)throw new Exception(message);}
 static IEnumerable<Control> All(Control root){foreach(Control c in root.Controls){yield return c;foreach(var n in All(c))yield return n;}}
 public static int Run(string project,string output){
  Directory.CreateDirectory(output);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);StudioUsbGate.UseTestLock(Path.Combine(output,"fixture-device.lock"));var cases=new List<object>();int failed=0;
  Action<string,Action> test=(name,action)=>{try{action();cases.Add(new{name=name,status="PASS"});}catch(Exception e){failed++;cases.Add(new{name=name,status="FAIL",error=e.ToString()});}};
  string source=Path.Combine(project,"models","Developed by Conr4d.nam"),bank=Path.Combine(output,"synthetic-bank.json"),hxf=Path.Combine(project,"build","POD_GO_NAM_Conr4d_v8.hxf");
  var slots=Enumerable.Range(0,128).Select(i=>new PodIrSlot{Index=i,Name=""}).ToArray();slots[48].Used=true;slots[48].Name="Example cabinet IR";slots[51].Used=slots[52].Used=true;slots[51].Name="NAM_0123456789ab FRIEDMAN HBE";slots[52].Name="NAM_abcdef012345 OTHER NAM";File.WriteAllText(bank,new JavaScriptSerializer().Serialize(slots));
  foreach(string language in new[]{"es","en"}){
   string lang=language;UiText.Select(lang);
   test("layout_"+lang,()=>{using(var window=new StudioWindow(true,source,bank)){
    window.PrepareFirmwarePreview(hxf);Need(window.MaximizeBox,"Maximizing must remain available");
    foreach(var size in new[]{new Size(1500,900),new Size(1348,730),new Size(1264,680),new Size(1008,680),new Size(1920,1000),new Size(1500,900)}){
     window.SetPreviewSize(size);Need(window.LayoutScale>=.78&&window.LayoutScale<=1,"Scale bounds");
     foreach(bool firmware in new[]{true,false}){window.Render(Path.Combine(output,(firmware?"firmware-":"nam-")+lang+"-"+size.Width+"x"+size.Height+".png"),firmware,53);if(!firmware)File.WriteAllText(Path.Combine(output,"bounds-"+lang+"-"+size.Width+".json"),new JavaScriptSerializer().Serialize(All(window).Select(c=>new{type=c.GetType().Name,text=c.Text.Length>80?c.Text.Substring(0,80):c.Text,bounds=c.Bounds.ToString(),client=c.ClientSize.ToString(),minimum=c.MinimumSize.ToString(),parent=c.Parent.GetType().Name})));}
    }
    Need(Math.Abs(window.LayoutScale-1)<.001,"Full size must return to 100 percent");
    Need(All(window).OfType<CheckBox>().Single(c=>c.Name=="AutomaticInput").Checked,"Automatic default");
    Need(!window.InstallEnabled&&PortableRuntime.Root==null&&StudioUsbGate.Owner=="","Offline isolation");
   }});
  }
  test("input_gain_covers_all_conditioning_paths_and_preserves_source",()=>{
   string before=NamCapture.Sha(File.ReadAllBytes(source));
   var json=new JavaScriptSerializer{MaxJsonLength=8*1024*1024};var root=NamCapture.Dict(json.DeserializeObject(File.ReadAllText(source)));var model=root;
   if((string)root["architecture"]=="SlimmableContainer")model=((object[])NamCapture.Dict(root["config"])["submodels"]).Select(x=>NamCapture.Dict(NamCapture.Dict(x)["model"])).Single(x=>Convert.ToInt32(NamCapture.Dict(((object[])NamCapture.Dict(x["config"])["layers"])[0])["channels"])==3);
   var original=((object[])model["weights"]).Select(x=>Convert.ToDouble(x)).ToArray();var expected=(double[])original.Clone();var positions=new List<int>{0,1,2};int offset=3;
   foreach(int kernel in NamCapture.Kernels){positions.AddRange(Enumerable.Range(offset+9*kernel+3,3));offset+=9*kernel+18;}
   Need(positions.Distinct().Count()==72,"72 raw-input coefficients");foreach(int p in positions)expected[p]*=Math.Pow(10,-6.0/20);expected[1870]*=Math.Pow(10,-5.0/20);
   Need(NamCapture.Read(source,-5,-6).Wave.SequenceEqual(NamCapture.Encode(expected)),"Encoded input trim must match all input paths");
   var zero=NamCapture.Read(source,-5,0);var baseline=(double[])original.Clone();baseline[1870]*=Math.Pow(10,-5.0/20);
   Need(zero.Wave.SequenceEqual(NamCapture.Encode(baseline)),"Automatic is byte-exact original input with previous output trim");
   Need(before==NamCapture.Sha(File.ReadAllBytes(source)),"Source NAM stays unchanged");
  });
  File.WriteAllText(Path.Combine(output,"results.json"),new JavaScriptSerializer().Serialize(new{status=failed==0?"PASS_OFFLINE_ONLY":"FAIL",cases=cases,USB_opened=false,SDK_started=false}));return failed==0?0:1;
 }
}
