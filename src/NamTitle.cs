using System;using System.Linq;using System.Text;

static class NamTitle {
 public const int Maximum=14;
 static bool Allowed(char c){return c>='A'&&c<='Z'||c>='0'&&c<='9'||" ._()-+".Contains(c);}
 public static bool Valid(string name){return name!=null&&name.Trim().Length>0&&name==name.Trim()&&name.Length<=Maximum&&name.All(Allowed);}
 public static string Suggest(string name){var text=new string((name??"").ToUpperInvariant().Normalize(NormalizationForm.FormD).Where(Allowed).ToArray());while(text.Contains("  "))text=text.Replace("  "," ");text=text.Trim();if(text.Length>Maximum)text=text.Replace("-","");return text.Length==0?"NAM A2 LITE":text.Substring(0,Math.Min(Maximum,text.Length)).TrimEnd();}
 public static string DeviceName(string hash,string name){if(hash==null||hash.Length!=64||!hash.All(c=>"0123456789abcdef".Contains(c))||!Valid(name))throw new ArgumentException("Escribe un nombre corto de hasta 14 caracteres: letras, nÃºmeros, espacios o . _ ( ) - +");return "NAM_"+hash.Substring(0,12)+" "+name;}
 // Stock pod15 font metrics match the firmware's 16-character / 120px bound.
 static readonly int[] widths={2,1,0,8,7,9,8,1,3,3,5,7,2,7,1,7,7,4,7,7,8,7,7,7,7,7,1,2,6,7,6,7,10,9,8,9,9,7,7,10,8,1,7,8,7,11,9,10,8,10,8,8,9,8,9,13,9,9,7,3,7,3,7,7,0,7,7,6,7,7,4,7,7,1,3,7,1,11,7,7,7,7,4,6,4,7,7,11,8,7,7,0,0,0,0};
 static int Pixels(string value){return value.Sum(c=>c>=32&&c<=126?widths[c-32]+1:0)-1;}
 public static string Preview(int model,string name){string value=model==0?"0. Dev by Conr4d":model.ToString()+". "+name;if(value.Length>16)value=value.Substring(0,16);while(value.Length>6&&Pixels(value)>120)value=value.Substring(0,value.Length-1);return value;}
}
