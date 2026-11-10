using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Forms;
class UiAudit {
 static readonly List<string> Issues=new List<string>();static double Lowest=21;
 static double L(Color c){double[] v={c.R/255.0,c.G/255.0,c.B/255.0};for(int i=0;i<3;i++)v[i]=v[i]<=.04045?v[i]/12.92:Math.Pow((v[i]+.055)/1.055,2.4);return v[0]*.2126+v[1]*.7152+v[2]*.0722;}
 static void Contrast(string name,Color fg,Color bg){double a=L(fg),b=L(bg),ratio=(Math.Max(a,b)+.05)/(Math.Min(a,b)+.05);Lowest=Math.Min(Lowest,ratio);if(ratio<4.5)Issues.Add(name+" contrast="+ratio.ToString("F2"));}
 static Color Background(Control c){return c.BackColor.A<255&&c.Parent!=null?Background(c.Parent):c.BackColor;}
 static void Inspect(Control c,string mode){
  var b=c as Button;if(b!=null){
   int width=TextRenderer.MeasureText(b.Text,b.Font,new Size(10000,100),TextFormatFlags.SingleLine|TextFormatFlags.NoPadding).Width;
   if(width>b.Width-10)Issues.Add(mode+" button clipped: "+b.Text);
   Contrast(mode+" "+b.Text,b.ForeColor,b.BackColor);Contrast(mode+" hover "+b.Text,b.ForeColor,b.FlatAppearance.MouseOverBackColor);Contrast(mode+" pressed "+b.Text,b.ForeColor,b.FlatAppearance.MouseDownBackColor);
   var type=b.GetType();var df=type.GetField("DisabledTextColor");var db=type.GetField("DisabledBackColor");if(df!=null)Contrast(mode+" disabled "+b.Text,(Color)df.GetValue(b),(Color)db.GetValue(b));
  }
  if(c is Label&&!String.IsNullOrEmpty(c.Text))Contrast(mode+" label "+c.Text,c.ForeColor,Background(c));
  foreach(Control child in c.Controls)Inspect(child,mode);
 }
 [STAThread]static int Main(string[]args){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);var assembly=Assembly.LoadFrom(args[0]);var type=assembly.GetType("FishManager");
  using(var form=(Form)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{true},null)){
   if(Math.Abs(form.Font.Size-12)>0.02)Issues.Add("Main font is not 1.2x");
   foreach(bool dark in new[]{true,false,true}){type.GetField("DarkMode",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,dark);type.GetMethod("ApplyTheme",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);Inspect(form,dark?"dark":"light");}
   foreach(bool dark in new[]{true,false}){var voiceType=assembly.GetType("VoiceSettings");string root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(args[0]),".."));using(var voice=(Form)Activator.CreateInstance(voiceType,new object[]{root,form.Icon,dark}))Inspect(voice,dark?"voice dark":"voice light");}
  }
  foreach(var issue in Issues)Console.WriteLine("FAIL "+issue);Console.WriteLine("Minimum text contrast="+Lowest.ToString("F2")+"; issues="+Issues.Count);return Issues.Count==0?0:1;
 }
}
