using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Windows.Forms;

sealed class VoiceSettings : Form {
 readonly string Root,ConfigFile;
 readonly TextBox Key=new TextBox();readonly ComboBox Voice=new ComboBox();
 readonly CheckBox VoiceEnabled=new CheckBox();readonly NumericUpDown Daily=new NumericUpDown();
 readonly Label Result=new Label();readonly JavaScriptSerializer Json=new JavaScriptSerializer();
 public VoiceSettings(string root,Icon icon,bool dark){
  Root=root;ConfigFile=Path.Combine(root,"private","breeze-tts.json");
  Text="大肥鱼语音 · BreezeBlue";Icon=icon;ClientSize=new Size(660,490);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei UI",10);BackColor=Color.FromArgb(242,246,251);
  AddLabel("大肥鱼的声音",24,20,610,35,17);
  AddLabel("使用你指定的两款音色，生成后由 QQ 原生语音通道发送。",24,63,610,26,10);
  AddLabel("BreezeBlue API Key",24,106,300,25,10);Key.SetBounds(24,137,610,30);Key.UseSystemPasswordChar=true;Controls.Add(Key);
  var show=new CheckBox{Text="显示密钥",Location=new Point(24,175),Size=new Size(150,27)};show.CheckedChanged+=delegate{Key.UseSystemPasswordChar=!show.Checked;};Controls.Add(show);
  var get=new LinkLabel{Text="打开官网创建 API Key",Location=new Point(385,175),Size=new Size(250,27),TextAlign=ContentAlignment.MiddleRight};get.LinkClicked+=delegate{Process.Start("https://breezeblue.ai/app/developer/api-keys");};Controls.Add(get);
  AddLabel("默认音色",24,218,150,25,10);Voice.SetBounds(190,215,444,32);Voice.DropDownStyle=ComboBoxStyle.DropDownList;Voice.Items.AddRange(new object[]{"大肥鱼（机灵版）","大肥鱼（迷糊版）"});Voice.SelectedIndex=0;Controls.Add(Voice);
  VoiceEnabled.Text="启用语音合成";VoiceEnabled.SetBounds(24,266,200,28);VoiceEnabled.Checked=true;Controls.Add(VoiceEnabled);
  AddLabel("每日申请字符上限",265,267,180,27,10);Daily.SetBounds(467,265,167,30);Daily.Minimum=1;Daily.Maximum=100000;Daily.Value=3000;Daily.Increment=500;Controls.Add(Daily);
  AddLabel("按需发语音，每条最多 300 字；失败改用文字。BreezeBlue 会消耗账号额度。\n密钥保存在项目的 private 文件夹，不进入 Git；改完保存即生效。",24,311,610,51,9);
  var save=ButtonAt("保存设置",24,380,185);save.Click+=delegate{try{Save();Result.Text="已保存，可在 QQ 里说“用语音回复我”。";}catch{Result.Text="保存失败，请检查 private 文件夹是否可写。";}};
  var sample=ButtonAt("生成试听（耗额度）",226,380,205);sample.Click+=async delegate{
   sample.Enabled=false;save.Enabled=false;Result.Text="正在合成试听，不会给 QQ 发消息…";
   try{Save();var data=await Task.Run(()=>RunSample());if(data.ContainsKey("path")){Result.Text="试听已生成，正在打开音频文件。";Process.Start(new ProcessStartInfo(Convert.ToString(data["path"])){UseShellExecute=true});}else Result.Text=data.ContainsKey("error")?Convert.ToString(data["error"]):"试听未完成。";}catch{Result.Text="试听未完成，请检查 API Key、账号额度和网络。";}
   finally{sample.Enabled=true;save.Enabled=true;}
  };
  var folder=ButtonAt("音频文件夹",448,380,186);folder.Click+=delegate{try{string dir=Path.Combine(FishManager.ReadInstancePath(Root,"runtimeDir"),"media","voice");Directory.CreateDirectory(dir);Process.Start("explorer.exe","\""+dir+"\"");}catch{Result.Text="请先按 README 配置本机路径。";}};
  Result.SetBounds(24,439,610,44);Result.Font=new Font(Font.FontFamily,9);Controls.Add(Result);
  try{if(File.Exists(ConfigFile)){var c=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(ConfigFile,Encoding.UTF8));if(c.ContainsKey("apiKey"))Key.Text=Convert.ToString(c["apiKey"]);if(c.ContainsKey("voiceId"))Voice.SelectedIndex=Convert.ToString(c["voiceId"])=="voc_8yud7hz7c9q2"?1:0;if(c.ContainsKey("enabled"))VoiceEnabled.Checked=Convert.ToBoolean(c["enabled"]);if(c.ContainsKey("dailyChars"))Daily.Value=Math.Max(Daily.Minimum,Math.Min(Daily.Maximum,Convert.ToDecimal(c["dailyChars"])));}}catch{Result.Text="读取旧设置失败，请重新填写后保存。";}
  FishTheme.Enlarge(this);FishTheme.Apply(this,dark);
  Voice.DrawMode=DrawMode.OwnerDrawFixed;Voice.ItemHeight=26;
  Voice.DrawItem+=delegate(object sender,DrawItemEventArgs e){if(e.Index<0)return;bool selected=(e.State&DrawItemState.Selected)!=0;var bg=selected?Color.FromArgb(38,111,219):(dark?Color.FromArgb(12,21,35):Color.White);var fg=selected?Color.White:(dark?FishTheme.DarkText:Color.FromArgb(31,49,72));using(var brush=new SolidBrush(bg))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,Convert.ToString(Voice.Items[e.Index]),Voice.Font,e.Bounds,fg,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);e.DrawFocusRectangle();};
 }
 void AddLabel(string value,int x,int y,int w,int h,float size){Controls.Add(new Label{Text=value,Location=new Point(x,y),Size=new Size(w,h),Font=new Font(Font.FontFamily,size),ForeColor=Color.FromArgb(31,49,72)});}
 Button ButtonAt(string value,int x,int y,int w){var b=new ReadableButton{Text=value,Location=new Point(x,y),Size=new Size(w,41),BackColor=Color.White,FlatStyle=FlatStyle.Flat};Controls.Add(b);return b;}
 void Save(){
  var config=new Dictionary<string,object>{{"enabled",VoiceEnabled.Checked},{"apiKey",Key.Text.Trim()},{"voiceId",Voice.SelectedIndex==1?"voc_8yud7hz7c9q2":"voc_u3nxkd5578wc"},{"maxChars",300},{"dailyChars",(int)Daily.Value}};
  Directory.CreateDirectory(Path.GetDirectoryName(ConfigFile));File.WriteAllText(ConfigFile,Json.Serialize(config),new UTF8Encoding(false));
  string memo=Path.Combine(Root,"private","大肥鱼密码密钥备忘.txt");string previous=File.Exists(memo)?File.ReadAllText(memo,Encoding.UTF8):"";
  const string marker="\r\n[BreezeBlue 大肥鱼语音]\r\n";int at=previous.IndexOf(marker,StringComparison.Ordinal);if(at>=0){int end=previous.IndexOf("[/BreezeBlue 大肥鱼语音]",at,StringComparison.Ordinal);if(end>=0)previous=previous.Remove(at,end+"[/BreezeBlue 大肥鱼语音]".Length-at);}
  File.WriteAllText(memo,previous+marker+"API Key："+Key.Text.Trim()+"\r\n默认音色："+Voice.Text+"\r\n配置文件："+ConfigFile+"\r\n官网：https://breezeblue.ai/app/developer/api-keys\r\n[/BreezeBlue 大肥鱼语音]\r\n",new UTF8Encoding(false));
 }
 Dictionary<string,object> RunSample(){
  var psi=new ProcessStartInfo(Path.Combine(FishManager.ReadInstancePath(Root,"runtimeDir"),"napcat","node.exe"),"\""+Path.Combine(Root,"voice","cli.mjs")+"\" sample"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
  using(var p=Process.Start(psi)){var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();if(!p.WaitForExit(100000)){try{p.Kill();}catch{}throw new Exception("Timeout");}stderr.GetAwaiter().GetResult();return Json.Deserialize<Dictionary<string,object>>(stdout.GetAwaiter().GetResult().Trim());}
 }
}
