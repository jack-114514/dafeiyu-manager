using System;
using System.Drawing;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

sealed class ReadableButton : Button {
 bool Hover,Pressed;
 public Color DisabledTextColor=Color.FromArgb(83,99,123),DisabledBackColor=Color.FromArgb(227,234,243);
 public ReadableButton(){SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint,true);}
 protected override void OnMouseEnter(EventArgs e){Hover=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){Hover=false;Pressed=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnMouseDown(MouseEventArgs e){Pressed=true;Invalidate();base.OnMouseDown(e);}
 protected override void OnMouseUp(MouseEventArgs e){Pressed=false;Invalidate();base.OnMouseUp(e);}
 protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
 protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
 protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
 protected override void OnPaint(PaintEventArgs e){
  var bg=!Enabled?DisabledBackColor:(Pressed?FlatAppearance.MouseDownBackColor:(Hover?FlatAppearance.MouseOverBackColor:BackColor));
  using(var brush=new SolidBrush(bg))e.Graphics.FillRectangle(brush,ClientRectangle);
  ControlPaint.DrawBorder(e.Graphics,ClientRectangle,FlatAppearance.BorderColor,ButtonBorderStyle.Solid);
  TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?ForeColor:DisabledTextColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
  if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-5,-5),ForeColor,bg);
 }
}

static class FishTheme {
 sealed class Original{public Color Back,Fore;public Original(Control c){Back=c.BackColor;Fore=c.ForeColor;}}
 static readonly ConditionalWeakTable<Control,Original> Colors=new ConditionalWeakTable<Control,Original>();
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attr,ref int value,int size);
 public static readonly Color DarkText=Color.FromArgb(232,238,247),DarkMuted=Color.FromArgb(174,189,211);
 public static void Enlarge(Control root){var fonts=new List<KeyValuePair<Control,Font>>();Collect(root,fonts);foreach(var pair in fonts)pair.Key.Font=new Font(pair.Value.FontFamily,pair.Value.Size*1.2f,pair.Value.Style);}
 static void Collect(Control c,List<KeyValuePair<Control,Font>> list){list.Add(new KeyValuePair<Control,Font>(c,c.Font));foreach(Control child in c.Controls)Collect(child,list);}
 static void Capture(Control c){Colors.GetValue(c,x=>new Original(x));foreach(Control child in c.Controls)Capture(child);}
 public static void Apply(Control c,bool dark){Capture(c);ApplyCore(c,dark);}
 static void ApplyCore(Control c,bool dark){
  var original=Colors.GetValue(c,x=>new Original(x));
  Color back=original.Back,fore=original.Fore;
  if(!dark&&fore.R==99&&fore.G==116&&fore.B==138)fore=Color.FromArgb(83,99,123);
  if(dark){
   back=c is Form?Color.FromArgb(16,24,39):Color.FromArgb(30,41,59);
   fore=DarkText;
   if(c is Label){back=Color.Transparent;fore=original.Fore==Color.White?Color.White:DarkMuted;if(original.Fore.R==31&&original.Fore.G==49)fore=DarkText;}
   if(c is Panel&&original.Back.R==21&&original.Back.G==45)back=Color.FromArgb(14,28,47);
   if(c is TextBoxBase||c is ComboBox||c is NumericUpDown)back=Color.FromArgb(12,21,35);
   if(c is CheckBox)back=Color.FromArgb(16,24,39);
   if(c is Button){
    if(original.Back.B==219&&original.Back.G==111){back=Color.FromArgb(38,111,219);fore=Color.White;}
    else if(original.Back.R==255&&original.Back.G==238){back=Color.FromArgb(64,39,36);fore=Color.FromArgb(255,180,167);}
   }
  }
  c.BackColor=back;c.ForeColor=fore;
  var button=c as Button;if(button!=null){button.UseVisualStyleBackColor=false;button.FlatAppearance.BorderColor=dark?Color.FromArgb(105,130,167):Color.FromArgb(184,200,221);button.FlatAppearance.MouseOverBackColor=dark?Color.FromArgb(48,66,94):Color.FromArgb(229,238,250);button.FlatAppearance.MouseDownBackColor=dark?Color.FromArgb(64,86,119):Color.FromArgb(210,225,247);}
  if(button!=null&&original.Fore==Color.White){button.FlatAppearance.MouseOverBackColor=Color.FromArgb(32,93,184);button.FlatAppearance.MouseDownBackColor=Color.FromArgb(25,74,149);}
  if(button!=null&&dark&&original.Back.R==255&&original.Back.G==238){button.FlatAppearance.MouseOverBackColor=Color.FromArgb(76,41,36);button.FlatAppearance.MouseDownBackColor=Color.FromArgb(85,43,37);}
  var readable=c as ReadableButton;if(readable!=null){readable.DisabledTextColor=dark?DarkMuted:Color.FromArgb(83,99,123);readable.DisabledBackColor=dark?Color.FromArgb(37,50,71):Color.FromArgb(227,234,243);}
  var link=c as LinkLabel;if(link!=null){link.LinkColor=dark?Color.FromArgb(164,201,255):Color.FromArgb(27,87,179);link.ActiveLinkColor=dark?Color.White:Color.FromArgb(20,65,138);link.VisitedLinkColor=link.LinkColor;}
  var box=c as CheckBox;if(box!=null)box.FlatStyle=FlatStyle.Flat;
  foreach(Control child in c.Controls)ApplyCore(child,dark);
  var form=c as Form;if(form!=null){try{int value=dark?1:0;DwmSetWindowAttribute(form.Handle,20,ref value,4);}catch{}}
 }
}
