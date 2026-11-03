using System;using System.IO;using System.Linq;using System.Globalization;using System.Threading;using System.Collections.Generic;using System.Reflection;using System.Web.Script.Serialization;

static class UiText {
 static readonly Dictionary<string,string> english=Read();
 static string language="es";
 public static bool English{get{return language=="en";}}
 public static string Language{get{return language;}}
 static string Settings{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PODGoNamManager","ui-language.txt");}}
 static Dictionary<string,string> Read(){using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("en.json"))using(var reader=new StreamReader(input))return new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(reader.ReadToEnd());}
 public static string T(string text){string result;return English&&english.TryGetValue(text,out result)?result:text;}
 public static void Select(string value){language=value=="es"?"es":"en";var culture=CultureInfo.GetCultureInfo(English?"en-US":"es-ES");Thread.CurrentThread.CurrentCulture=culture;Thread.CurrentThread.CurrentUICulture=culture;CultureInfo.DefaultThreadCurrentCulture=culture;CultureInfo.DefaultThreadCurrentUICulture=culture;}
 public static void Load(){string value=CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;try{if(File.Exists(Settings))value=File.ReadAllText(Settings).Trim();}catch(IOException){}Select(value);}
 public static void Save(string value){if(value!="es"&&value!="en")throw new ArgumentException("Unknown language");Directory.CreateDirectory(Path.GetDirectoryName(Settings));File.WriteAllText(Settings,value);}
 public static bool Has(string key){return english.ContainsKey(key);}
}
