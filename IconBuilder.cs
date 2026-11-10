using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;

class FishIconBuilder {
    static Bitmap Draw(int size){
        var bmp=new Bitmap(size,size,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(bmp)){
            g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size/256f,size/256f);g.Clear(Color.Transparent);
            using(var bg=new SolidBrush(Color.FromArgb(24,81,153)))g.FillEllipse(bg,4,4,248,248);
            using(var wave=new SolidBrush(Color.FromArgb(33,119,205)))g.FillEllipse(wave,14,104,228,140);
            using(var white=new SolidBrush(Color.FromArgb(240,250,255))){
                g.FillPolygon(white,new[]{new Point(165,128),new Point(229,91),new Point(214,146),new Point(230,179),new Point(166,165)});
                g.FillEllipse(white,33,80,158,112);
            }
            using(var fin=new SolidBrush(Color.FromArgb(137,204,244)))g.FillEllipse(fin,91,151,46,22);
            using(var eye=new SolidBrush(Color.FromArgb(23,58,100)))g.FillEllipse(eye,64,112,13,17);
            using(var shine=new SolidBrush(Color.White))g.FillEllipse(shine,67,114,4,5);
            using(var pen=new Pen(Color.FromArgb(55,111,165),4)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;g.DrawArc(pen,57,127,32,25,15,115);}
            using(var spray=new Pen(Color.FromArgb(161,221,255),10)){spray.StartCap=LineCap.Round;spray.EndCap=LineCap.Round;g.DrawLine(spray,125,73,125,47);g.DrawArc(spray,103,31,24,26,195,170);g.DrawArc(spray,126,29,26,26,170,170);}
        }return bmp;
    }
    static byte[] Dib(Bitmap bmp){
        byte[] raw;using(var ms=new MemoryStream()){bmp.Save(ms,ImageFormat.Bmp);raw=ms.ToArray();}
        int pixelOffset=BitConverter.ToInt32(raw,10), bytes=bmp.Width*bmp.Height*4, maskStride=((bmp.Width+31)/32)*4;
        using(var ms=new MemoryStream())using(var bw=new BinaryWriter(ms)){
            bw.Write(40);bw.Write(bmp.Width);bw.Write(bmp.Height*2);bw.Write((short)1);bw.Write((short)32);bw.Write(0);bw.Write(bytes);bw.Write(0);bw.Write(0);bw.Write(0);bw.Write(0);
            bw.Write(raw,pixelOffset,bytes);bw.Write(new byte[maskStride*bmp.Height]);return ms.ToArray();
        }
    }
    static int Main(string[]args){
        int[]sizes={16,24,32,48,64,128,256};var frames=new List<byte[]>();
        foreach(int size in sizes)using(var bmp=Draw(size)){frames.Add(Dib(bmp));if(size==256)bmp.Save(Path.ChangeExtension(args[0],".png"),ImageFormat.Png);}
        using(var file=File.Create(args[0]))using(var bw=new BinaryWriter(file)){
            bw.Write((short)0);bw.Write((short)1);bw.Write((short)sizes.Length);int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++){bw.Write((byte)(sizes[i]==256?0:sizes[i]));bw.Write((byte)(sizes[i]==256?0:sizes[i]));bw.Write((short)0);bw.Write((short)1);bw.Write((short)32);bw.Write(frames[i].Length);bw.Write(offset);offset+=frames[i].Length;}
            foreach(var frame in frames)bw.Write(frame);
        }
        foreach(int size in new[]{32,256})using(var icon=new Icon(args[0],new Size(size,size)))using(var bitmap=icon.ToBitmap()){Console.WriteLine("Icon requested="+size+" decoded="+bitmap.Width);if(bitmap.Width<16)return 1;}
        return 0;
    }
}
