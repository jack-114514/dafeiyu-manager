using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Windows.Forms;

sealed class FishManager : Form {
    readonly string Root=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".."));
    readonly Label Headline=new Label(), HarnessState=new Label(), QQState=new Label(), LinkState=new Label(), LifeSummary=new Label(), HealthSummary=new Label();
    readonly TextBox Activity=new TextBox();
    readonly Button StartButton=new ReadableButton(), PauseButton=new ReadableButton(), StopButton=new ReadableButton(), LifeButton=new ReadableButton();
    readonly List<Button> OperationButtons=new List<Button>();
    readonly System.Windows.Forms.Timer Poll=new System.Windows.Forms.Timer();
    readonly Button ThemeButton=new ReadableButton();
    readonly Button AutoStartButton=new ReadableButton();
    readonly Button AutoRepairButton=new ReadableButton();
    bool DarkMode=true;
    Dictionary<string,object> LastStatus;
    bool Busy=false, Paused=false, LifeEnabled=false;
    bool AutoStartAvailable, AutoStartEnabled;
    bool AutoRepairAvailable, AutoRepairEnabled;
    readonly bool Preview;
    readonly Color Ink=Color.FromArgb(31,49,72), Muted=Color.FromArgb(99,116,138), Blue=Color.FromArgb(38,111,219);

    FishManager(bool preview) {
        Preview=preview; Text="大肥鱼管家"; ClientSize=new Size(930,820); MinimumSize=new Size(946,859);
        AutoScaleMode=AutoScaleMode.None; StartPosition=FormStartPosition.CenterScreen;
        Font=new Font("Microsoft YaHei UI",10); BackColor=Color.FromArgb(242,246,251); ForeColor=Ink;
        Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        var header=new Panel{Location=new Point(0,0),Size=new Size(930,112),BackColor=Color.FromArgb(21,45,77),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right}; Controls.Add(header);
        var mark=new PictureBox{Location=new Point(24,24),Size=new Size(64,64),SizeMode=PictureBoxSizeMode.Zoom,Image=Icon.ToBitmap()};header.Controls.Add(mark);
        header.Controls.Add(LabelAt("大肥鱼管家",108,22,430,37,24,Color.White,true));
        header.Controls.Add(LabelAt("DeepSeek Harness · QQ 控制台",110,66,530,24,11,Color.FromArgb(179,204,237),false));
        var badge=LabelAt("LOCAL / v1.2",727,16,178,25,9,Color.FromArgb(196,222,250),false);badge.TextAlign=ContentAlignment.MiddleRight;header.Controls.Add(badge);
        ThemeButton.Text="切换浅色";ThemeButton.SetBounds(765,55,140,34);ThemeButton.FlatStyle=FlatStyle.Flat;ThemeButton.BackColor=Color.FromArgb(30,56,88);ThemeButton.ForeColor=Color.White;header.Controls.Add(ThemeButton);
        ThemeButton.Click+=delegate{DarkMode=!DarkMode;ApplyTheme();try{Directory.CreateDirectory(Path.Combine(Root,"private"));File.WriteAllText(Path.Combine(Root,"private","ui-settings.json"),new JavaScriptSerializer().Serialize(new{darkMode=DarkMode}),Encoding.UTF8);}catch{Log("主题已切换，但偏好未能保存。");}};
        Headline.SetBounds(28,129,866,31);Headline.Font=new Font(Font.FontFamily,14,FontStyle.Bold);Headline.Text="正在检查现有程序与连接…";Headline.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(Headline);
        Headline.Width=700;var repair=new ReadableButton();ConfigureButton(repair,"修复 QQ 收发",746,128,158,32,Color.White,Ink);repair.Click+=async delegate{await Execute("RepairQQ",false);};
        AddCard("DEEPSEEK",HarnessState,28,174);AddCard("QQ 小号",QQState,328,174);AddCard("自动回复",LinkState,628,174);
        ConfigureButton(StartButton,"一键启动",28,288,276,53,Blue,Color.White);StartButton.Font=new Font(Font.FontFamily,12,FontStyle.Bold);
        ConfigureButton(PauseButton,"暂停自动回复",328,288,276,53,Color.White,Ink);
        ConfigureButton(StopButton,"一键关闭服务",628,288,276,53,Color.FromArgb(255,238,235),Color.FromArgb(168,65,49));
        StartButton.Click+=async delegate{await Execute("Start",false);};
        PauseButton.Click+=async delegate{await Execute(Paused?"Resume":"Pause",false);};
        StopButton.Click+=async delegate{await Execute("Stop",false);};
        Controls.Add(LabelAt("暂停只停自动回复，保留 QQ 登录；关闭服务会停止 QQ 后台及 DeepSeek。",30,352,870,25,10,Muted,false));
        HealthSummary.SetBounds(30,378,870,22);HealthSummary.Font=new Font(Font.FontFamily,9);HealthSummary.ForeColor=Muted;HealthSummary.Text="收发待实测；连接正常不代表模型已经回复。";Controls.Add(HealthSummary);
        Controls.Add(LabelAt("常用入口",28,401,500,29,13,Ink,true));
        var web=new ReadableButton();ConfigureButton(web,"QQ 登录网页",28,443,160,45,Color.White,Ink);web.Click+=async delegate{await Execute("OpenWeb",false);};
        var voice=new ReadableButton();ConfigureButton(voice,"大肥鱼语音",207,443,160,45,Color.White,Ink);voice.Click+=delegate{using(var f=new VoiceSettings(Root,Icon,DarkMode))f.ShowDialog(this);};
        var keys=new ReadableButton();ConfigureButton(keys,"查看密码密钥",386,443,160,45,Color.White,Ink);keys.Click+=delegate{ShowSecrets();};
        var folder=new ReadableButton();ConfigureButton(folder,"密钥文件夹",565,443,160,45,Color.White,Ink);folder.Click+=delegate{OpenPrivateFolder();};
        var refresh=new ReadableButton();ConfigureButton(refresh,"重新检查",744,443,160,45,Color.White,Ink);refresh.Click+=async delegate{await Execute("Status",false);};
        Controls.Add(LabelAt("自主生活",28,515,125,26,12,Ink,true));
        LifeSummary.SetBounds(160,519,740,24);LifeSummary.Font=new Font(Font.FontFamily,9);LifeSummary.ForeColor=Muted;LifeSummary.Text="等待检查 B 站登录与日记…";Controls.Add(LifeSummary);
        var bili=new ReadableButton();ConfigureButton(bili,"B 站登录网页",28,553,204,44,Color.White,Ink);bili.Click+=async delegate{await Execute("BiliLogin",false);};
        ConfigureButton(LifeButton,"暂停自主活动",252,553,204,44,Color.White,Ink);LifeButton.Click+=async delegate{await Execute(LifeEnabled?"LifePause":"LifeResume",false);};
        var diaries=new ReadableButton();ConfigureButton(diaries,"打开日记文件夹",476,553,204,44,Color.White,Ink);diaries.Click+=delegate{OpenDiaryFolder();};
        var once=new ReadableButton();ConfigureButton(once,"现在活动一次",700,553,204,44,Color.White,Ink);once.Click+=async delegate{await Execute("LifeRun",false);};
        Controls.Add(LabelAt("操作记录",28,619,500,27,12,Ink,true));
        Activity.SetBounds(28,652,876,115);Activity.Multiline=true;Activity.ReadOnly=true;Activity.ScrollBars=ScrollBars.Vertical;Activity.BorderStyle=BorderStyle.FixedSingle;Activity.BackColor=Color.White;Activity.ForeColor=Muted;Activity.Font=new Font("Microsoft YaHei UI",9);Activity.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(Activity);
        var footer=LabelAt("退出管家后，后台服务继续运行。",28,789,430,23,9,Muted,false);footer.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(footer);
        ConfigureButton(AutoRepairButton,"自动修复 · 检查中",483,780,204,34,Color.White,Ink);
        AutoRepairButton.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;
        AutoRepairButton.AccessibleDescription="连续检查确认故障后，自动恢复后台进程或 QQ 连接。";
        AutoRepairButton.Enabled=false;
        AutoRepairButton.Click+=async delegate{await Execute(AutoRepairEnabled?"DisableAutoRepair":"EnableAutoRepair",false);};
        ConfigureButton(AutoStartButton,"开机自启 · 检查中",700,780,204,34,Color.White,Ink);
        AutoStartButton.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;
        AutoStartButton.AccessibleDescription="开机启动后台，登录 Windows 后打开管家。";
        AutoStartButton.Enabled=false;
        AutoStartButton.Click+=async delegate{await Execute(AutoStartEnabled?"DisableAutoStart":"EnableAutoStart",false);};
        Poll.Interval=12000;Poll.Tick+=async delegate{if(!Busy)await Execute("Status",true);};
        Shown+=async delegate{if(!Preview){await Execute("Status",false);Poll.Start();}};
        FormClosing+=delegate{Poll.Stop();}; // Manager exit deliberately does not change service state.
        try{var pref=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(Root,"private","ui-settings.json"),Encoding.UTF8));DarkMode=Convert.ToBoolean(pref["darkMode"]);}catch{}
        FishTheme.Enlarge(this);ApplyTheme();
    }
    void ApplyTheme(){FishTheme.Apply(this,DarkMode);ThemeButton.Text=DarkMode?"切换浅色":"切换深色";if(LastStatus!=null)UpdateStatus(LastStatus);}
    Label LabelAt(string text,int x,int y,int w,int h,float size,Color color,bool bold){return new Label{Text=text,Location=new Point(x,y),Size=new Size(w,h),ForeColor=color,Font=new Font(Font.FontFamily,size,bold?FontStyle.Bold:FontStyle.Regular)};}
    void AddCard(string title,Label value,int x,int y){var p=new Panel{Location=new Point(x,y),Size=new Size(276,91),BackColor=Color.White};p.Controls.Add(LabelAt(title,16,13,244,23,9,Muted,true));value.SetBounds(16,44,248,30);value.Text="检查中";value.ForeColor=Ink;value.Font=new Font(Font.FontFamily,13,FontStyle.Bold);p.Controls.Add(value);Controls.Add(p);}
    void ConfigureButton(Button b,string text,int x,int y,int w,int h,Color bg,Color fg){b.Text=text;b.SetBounds(x,y,w,h);b.BackColor=bg;b.ForeColor=fg;b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=Color.FromArgb(216,226,239);b.FlatAppearance.BorderSize=1;b.Cursor=Cursors.Hand;b.UseVisualStyleBackColor=false;Controls.Add(b);OperationButtons.Add(b);}
    static bool Flag(Dictionary<string,object>d,string k){return d.ContainsKey(k)&&Convert.ToBoolean(d[k]);}
    void UpdateStatus(Dictionary<string,object>d){
        if(!d.ContainsKey("harnessRunning"))return;
        LastStatus=d;
        AutoStartAvailable=Flag(d,"autoStartAvailable");
        AutoStartEnabled=Flag(d,"autoStartEnabled");
        AutoStartButton.Text=AutoStartAvailable?(AutoStartEnabled?"开机自启 · 已开启":"开机自启 · 已关闭"):"开机自启 · 不可用";
        AutoStartButton.Enabled=AutoStartAvailable&&!Busy;
        AutoRepairAvailable=Flag(d,"autoRepairAvailable");
        AutoRepairEnabled=Flag(d,"autoRepairEnabled");
        AutoRepairButton.Text=AutoRepairAvailable?(AutoRepairEnabled?"自动修复 · 已开启":"自动修复 · 已关闭"):"自动修复 · 不可用";
        AutoRepairButton.Enabled=AutoRepairAvailable&&!Busy;
        bool harness=Flag(d,"harnessRunning"), online=Flag(d,"online"), bridge=Flag(d,"bridgeConnected");Paused=Flag(d,"paused");
        HarnessState.Text=harness?"运行中":"未启动";QQState.Text=online?"QQ 小号 · 已在线":(Flag(d,"napcatRunning")?"待登录 / 连接中":"后台未启动");
        string level=d.ContainsKey("replyLevel")?Convert.ToString(d["replyLevel"]):"unverified";
        string summary=d.ContainsKey("replySummary")?Convert.ToString(d["replySummary"]):"收发待实测";
        string qqSession=d.ContainsKey("qqSessionState")?Convert.ToString(d["qqSessionState"]):"unknown";
        if(online&&(qqSession=="failed"||qqSession=="needs_login"))QQState.Text="会话异常 · 需重新登录";
        if(qqSession=="wrong_account")QQState.Text="登录了其他 QQ";
        bool connected=harness&&online&&bridge, verified=connected&&level=="verified";
        LinkState.Text=Paused?"已暂停":(!connected?"等待连接":(level=="error"?"收发异常":(level=="working"?"正在处理消息":(verified?"最近收发成功":"已连接 · 待验证"))));
        LinkState.ForeColor=Paused?(DarkMode?Color.FromArgb(255,211,122):Color.FromArgb(144,91,10)):(level=="error"?(DarkMode?Color.FromArgb(255,180,167):Color.FromArgb(183,57,42)):(verified?(DarkMode?Color.FromArgb(116,230,193):Color.FromArgb(22,128,98)):(DarkMode?Color.FromArgb(255,211,122):Color.FromArgb(144,91,10))));
        Headline.Text=(!harness&&!Flag(d,"napcatRunning"))?"服务已停止，可点击一键启动":(Paused?"自动回复已暂停":(!connected?"连接未就绪，请检查 QQ 登录":(level=="error"?"检测到收发异常，请查看下方详情":(verified?"最近回复已送出，后台正在运行":"基础连接存在，收发状态需核对"))));
        HealthSummary.Text=(Paused?"自动回复已暂停；QQ 登录保留":summary)+"  ·  收到 "+TimeLabel(d,"lastInbound")+"  ·  发出 "+TimeLabel(d,"lastOutbound");
        PauseButton.Text=Paused?"恢复自动回复":"暂停自动回复";
        LifeEnabled=Flag(d,"lifeEnabled");LifeButton.Text=LifeEnabled?"暂停自主活动":"开启自主活动";
        int count=d.ContainsKey("diaryCount")?Convert.ToInt32(d["diaryCount"]):0;
        LifeSummary.Text=(Flag(d,"biliLoggedIn")?"B 站已登录":"B 站待扫码")+"  ·  "+(LifeEnabled?(Flag(d,"lifeActive")?"正在活动":"混合生活 · 主动消息 ≤5/天") : "自主活动已暂停")+"  ·  日记 "+count+" 篇";
    }
    async Task Execute(string action,bool quiet){
        if(Busy)return;Busy=true;foreach(var b in OperationButtons)b.Enabled=false;
        if(!quiet)Log(action=="Status"?"正在检查进程、QQ 登录状态与连接…":"正在处理："+ActionName(action));
        try{
            var result=await Task.Run(()=>RunBackend(action));
            if(IsDisposed)return;
            UpdateStatus(result);
            string msg=result.ContainsKey("message")?Convert.ToString(result["message"]):"未收到操作结果";
            if(!quiet||!Flag(result,"ok"))Log(msg);
        }catch(Exception ex){if(!IsDisposed)Log("操作失败："+ex.Message);}
        finally{if(!IsDisposed){Busy=false;foreach(var b in OperationButtons)b.Enabled=true;AutoStartButton.Enabled=AutoStartAvailable;AutoRepairButton.Enabled=AutoRepairAvailable;}}
    }
    static string TimeLabel(Dictionary<string,object>d,string k){if(!d.ContainsKey(k)||Convert.ToDouble(d[k])<=0)return "暂无记录";return new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddMilliseconds(Convert.ToDouble(d[k])).ToLocalTime().ToString("HH:mm:ss");}
    string ActionName(string a){switch(a){case "Start":return "启动服务";case "EnableAutoStart":return "开启开机自启";case "DisableAutoStart":return "关闭开机自启";case "EnableAutoRepair":return "开启自动修复";case "DisableAutoRepair":return "关闭自动修复";case "RepairQQ":return "重连 QQ 后台并检查保存的登录授权";case "Pause":return "暂停自动回复";case "Resume":return "恢复自动回复";case "Stop":return "关闭服务";case "BiliLogin":return "打开 B 站登录";case "LifePause":return "暂停自主活动";case "LifeResume":return "开启自主活动";case "LifeRun":return "活动一次";default:return "打开 QQ 登录网页";}}
    Dictionary<string,object> RunBackend(string action){
        var psi=new ProcessStartInfo("powershell.exe","-NoLogo -NoProfile -ExecutionPolicy Bypass -File \""+Path.Combine(Root,"control.ps1")+"\" -Action "+action){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=Root};
        using(var p=Process.Start(psi)){
            var outputTask=p.StandardOutput.ReadToEndAsync();var errorTask=p.StandardError.ReadToEndAsync();
            if(!p.WaitForExit(90000)){try{p.Kill();}catch{}throw new Exception("检查超时，请稍后重新检查。");}
            string output=outputTask.GetAwaiter().GetResult().Trim();errorTask.GetAwaiter().GetResult();
            var lines=output.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);
            for(int i=lines.Length-1;i>=0;i--){if(lines[i].TrimStart().StartsWith("{"))return new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(lines[i]);}
            throw new Exception("后台未返回有效结果，请检查项目目录。");
        }
    }
    void Log(string text){string line=DateTime.Now.ToString("HH:mm:ss")+"  "+text;Activity.AppendText(line+Environment.NewLine);try{Directory.CreateDirectory(Path.Combine(Root,"logs"));File.AppendAllText(Path.Combine(Root,"logs","manager.log"),line+Environment.NewLine,Encoding.UTF8);}catch{}}
    void ShowSecrets(){
        string path=Path.Combine(Root,"private","大肥鱼密码密钥备忘.txt");
        if(!File.Exists(path)){Log("未找到密码密钥备忘，请打开密钥文件夹检查。");return;}
        using(var f=new Form{Text="密码密钥 · 本机明文备忘",StartPosition=FormStartPosition.CenterParent,Size=new Size(890,700),MinimumSize=new Size(650,450),Font=new Font("Microsoft YaHei UI",10),Icon=Icon}){
            var text=new TextBox{Multiline=true,ReadOnly=true,WordWrap=false,ScrollBars=ScrollBars.Both,Dock=DockStyle.Fill,Font=new Font("Microsoft YaHei UI",10),Text=File.ReadAllText(path,Encoding.UTF8)};f.Controls.Add(text);
            var top=new Label{Text="此文件保存在 C 盘 private 文件夹，可选中文字复制；未纳入 Git。",Dock=DockStyle.Top,Height=45,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(10,0,0,0)};f.Controls.Add(top);FishTheme.Enlarge(f);FishTheme.Apply(f,DarkMode);f.ShowDialog(this);
        }
    }
    void OpenPrivateFolder(){string dir=Path.Combine(Root,"private");Directory.CreateDirectory(dir);Process.Start(new ProcessStartInfo("explorer.exe","\""+dir+"\""){UseShellExecute=true});}
    internal static string ReadInstancePath(string root,string key){
        var config=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(root,"private","instance.json"),Encoding.UTF8));
        var path=Convert.ToString(config[key]);
        if(!Path.IsPathRooted(path))throw new InvalidDataException("Expected an absolute path in private/instance.json");
        return Path.GetFullPath(path);
    }
    void OpenDiaryFolder(){try{string dir=Path.Combine(ReadInstancePath(Root,"fishHome"),"diary");if(!Directory.Exists(dir))dir=Path.Combine(ReadInstancePath(Root,"runtimeDir"),"life","diary");Directory.CreateDirectory(dir);Process.Start(new ProcessStartInfo("explorer.exe","\""+dir+"\""){UseShellExecute=true});}catch{Log("请先按 README 配置本机路径。");}}
    void SetPreview(){Headline.Text="基础连接存在，收发状态需核对";HarnessState.Text="运行中";QQState.Text="QQ 小号 · 已在线";LinkState.Text="已连接 · 待验证";LinkState.ForeColor=DarkMode?Color.FromArgb(255,211,122):Color.FromArgb(144,91,10);HealthSummary.Text="连接存在，收发待实测  ·  收到 15:30:51  ·  发出 15:30:58";Activity.Text="连接检查通过，仅代表后台存在、QQ 接口在线。\r\n收到消息、模型处理和成功发出会分别更新状态。\r\n退出管家不会关闭后台程序。";}
    [STAThread] static int Main(string[]args){
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        if(args.Length>1&&args[0]=="--probe"){
            using(var f=new FishManager(true)){
                var result=f.RunBackend(args[1]);Directory.CreateDirectory(Path.Combine(f.Root,"logs"));
                File.WriteAllText(Path.Combine(f.Root,"logs","probe.json"),new JavaScriptSerializer().Serialize(result),Encoding.UTF8);
                return Flag(result,"ok")?0:1;
            }
        }
        if(args.Length>0&&args[0]=="--lifecycle-check"){
            using(var f=new FishManager(false)){
                var timer=new System.Windows.Forms.Timer{Interval=18000};
                timer.Tick+=delegate{timer.Stop();f.Close();};f.Shown+=delegate{timer.Start();};
                Application.Run(f);timer.Dispose();
            }return 0;
        }
        if(args.Length>0&&(args[0]=="--render"||args[0]=="--render-light"||args[0]=="--render-voice")){
            using(var f=new FishManager(true)){f.DarkMode=args[0]!="--render-light";f.ApplyTheme();f.SetPreview();
             using(var voice=args[0]=="--render-voice"?new VoiceSettings(f.Root,f.Icon,true):null){Form target=voice!=null?(Form)voice:f;target.Show();Application.DoEvents();using(var bmp=new Bitmap(target.Width,target.Height)){target.DrawToBitmap(bmp,new Rectangle(Point.Empty,target.Size));bmp.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);}target.Hide();}
            }return 0;
        }
        bool created;using(var mutex=new Mutex(true,"Local\\DeepSeekDafeiyuManager",out created)){
            if(!created){MessageBox.Show("大肥鱼管家已在运行，请从任务栏打开现有窗口。","大肥鱼管家");return 0;}
            Application.Run(new FishManager(false));
        }return 0;
    }
}
