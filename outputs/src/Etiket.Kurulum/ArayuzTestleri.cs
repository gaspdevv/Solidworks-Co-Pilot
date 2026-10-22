using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;

namespace Pronom.Kurulum
{
 static class ArayuzTestleri
 {
  [DllImport("user32.dll")]static extern IntPtr GetThreadDpiAwarenessContext();
  [DllImport("user32.dll")]static extern bool AreDpiAwarenessContextsEqual(IntPtr a,IntPtr b);
  [DllImport("user32.dll")]static extern uint GetDpiForWindow(IntPtr hwnd);
  [StructLayout(LayoutKind.Sequential)]struct Rect{public int Left,Top,Right,Bottom;}
  [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr wparam,ref Rect rect);
  static int sayi;
  static void Ok(bool b,string message){if(!b)throw new Exception("ARAYUZ: "+message);sayi++;}
  static IEnumerable<Control> Altlar(Control root)
  {
   foreach(Control c in root.Controls){yield return c;foreach(var child in Altlar(c))yield return child;}
  }
  static void KirpilmaKontrolu(Form form)
  {
   foreach(var c in Altlar(form))
   {
    if(!c.Visible)continue;
    var alan=c.Parent.DisplayRectangle;
    Ok(c.Left>=alan.Left-2&&c.Top>=alan.Top-2&&c.Right<=alan.Right+2&&c.Bottom<=alan.Bottom+2,"Kontrol ulasilabilir alanda olmali: "+c.Text+" "+c.Bounds+" alan="+alan+" dpi="+form.DeviceDpi);
    if(c is Label)
    {
     var size=TextRenderer.MeasureText(c.Text,c.Font,new Size(c.ClientSize.Width,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix|TextFormatFlags.TextBoxControl);
     Ok(size.Height<=c.ClientSize.Height+2,"Yazi kirpilmamali: "+c.Text);
    }
   }
  }
  static void Goruntu(Form form,string path)
  {
   form.PerformLayout();form.Refresh();
   using(var b=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(b,new Rectangle(Point.Empty,form.Size));b.Save(path);}
  }
  public static int Calistir(string folder)
  {
   Directory.CreateDirectory(folder);
   Ok(AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(),new IntPtr(-4)),"PerMonitorV2 aktif olmali; config EXE disinda gerekmemeli.");
   uint dpi=0;
   var bilgiler=new[]{
    KurulumCekirdegi.KurulumDurumu(null,false,false,false),
    KurulumCekirdegi.KurulumDurumu(KurulumCekirdegi.EskiDll,true,false,false),
    KurulumCekirdegi.KurulumDurumu(KurulumCekirdegi.YeniDll,true,true,true),
    KurulumCekirdegi.KurulumDurumu(null,true,false,false)};
   for(var i=0;i<bilgiler.Length;i++)using(var form=new KurulumPenceresi(bilgiler[i]))
   {
    form.ShowInTaskbar=false;form.Opacity=0;form.Show();form.PerformLayout();form.Refresh();
    dpi=GetDpiForWindow(form.Handle);
    Ok(form.DeviceDpi==dpi,"WinForms ve Windows pencere DPI'si ayni olmali.");
    Ok(Math.Abs(form.ClientSize.Width-720*dpi/96.0)<3,"Pencere fiziksel DPI ile olceklenmeli.");
    KirpilmaKontrolu(form);
    Goruntu(form,Path.Combine(folder,"setup-"+i+".png"));
    foreach(var hedefDpi in new[]{120,144,192,96})
    {
     // Yalniz testin kendi gorunmez HWND'sine DPI degisimi bildirimi.
     // Fiziksel ekran olcegi ve kullanicinin Windows ayari degismez.
     var ratio=(double)hedefDpi/form.DeviceDpi;
     var rect=new Rect{Left=form.Left,Top=form.Top,Right=form.Left+(int)Math.Round(form.Width*ratio),Bottom=form.Top+(int)Math.Round(form.Height*ratio)};
     SendMessage(form.Handle,0x02E0,new IntPtr((hedefDpi<<16)|hedefDpi),ref rect);
     Ok(form.DeviceDpi==hedefDpi,"DPI bildirimi islenmeli: "+hedefDpi);
     Goruntu(form,Path.Combine(folder,"setup-"+i+"-dpi"+hedefDpi+".png"));
     KirpilmaKontrolu(form);
     foreach(var c in Altlar(form))if(c is Button)
     {
      form.ScrollControlIntoView(c);
      Ok(form.ClientRectangle.Contains(c.Bounds),"Dugmeye kaydirarak erisilebilmeli: "+c.Text);
     }
     form.AutoScrollPosition=Point.Empty;
    }
    form.Close();
   }
   File.WriteAllText(Path.Combine(folder,"sonuc.txt"),"GECTI: "+sayi+" arayuz kontrolu. Gercek PerMonitorV2; baslangic DPI="+dpi+". 4 durum; 120/144/192/96 DPI bildirimleri kendi HWND'sinde sinandi. Fiziksel ekran olcegi degistirilmedi; farkli fiziksel ekranlar arasinda tasima yapilmadi.");
   return 0;
  }
 }
}
