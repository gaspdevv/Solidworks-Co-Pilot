using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;

namespace Pronom.Kurulum
{
 static class Program
 {
  internal static int Calistir(string[] args)
  {
   try
   {
    if(args.Length==2&&args[0]=="--kendi-testleri")return KurulumTestleri.Calistir(args[1]);
    if(args.Length==3&&args[0]=="--guncelleme-testleri")return KurulumTestleri.Guncelleme(args[1],args[2]);
    if(args.Length==2&&args[0]=="--makine-kur")
    {
     var id=Guid.Parse(args[1]);var path=WindowsKurulumu.SonucYolu(id);
     try{WindowsKurulumu.MakineKur();Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,"Makine kurulumu doğrulandı.");return 0;}
     catch(Exception e){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,e.ToString());return 1;}
    }
    if(args.Length==1&&args[0]=="--kaldir")
    {
     if(!WindowsKurulumu.Yonetici){var p=Yukseltilmis("--kaldir");p.WaitForExit();if(p.ExitCode!=0)throw new IOException("Kaldırma tamamlanamadı.");}
     else WindowsKurulumu.Kaldir();
     MessageBox.Show("Pronom kurulumu kaldırıldı. CAD ve kullanıcı günlükleri korundu. Kalan kurulum dosyası Windows yeniden başlatıldığında silinir.","Pronom");return 0;
    }
    if(args.Length==1&&args[0]=="--bekleyen")
    {
     var p=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),@"System32\WindowsPowerShell\v1.0\powershell.exe");
     Process.Start(new ProcessStartInfo(p,"-NoProfile -STA -ExecutionPolicy RemoteSigned -File \""+Path.Combine(WindowsKurulumu.Hedef,"BekleyenKayitlar.ps1")+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});return 0;
    }
    Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
    if(args.Length==2&&args[0]=="--arayuz-testleri")return ArayuzTestleri.Calistir(args[1]);
    using(var f=new KurulumPenceresi())
    {
     if(args.Length==2&&args[0]=="--onizleme")
     {
      f.ShowInTaskbar=false;f.Opacity=0;f.Show();f.PerformLayout();f.Refresh();
      using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(0,0,f.Width,f.Height));b.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);}f.Close();return 0;
     }
     Application.Run(f);
    }
    return 0;
   }
   catch(Exception e){if(args.Length>0&&(args[0].EndsWith("-testleri")||args[0]=="--onizleme")){Console.Error.WriteLine(e);return 1;}MessageBox.Show(e.Message,"Pronom kurulumu",MessageBoxButtons.OK,MessageBoxIcon.Warning);return 1;}
  }
  internal static Process Yukseltilmis(string args)=>Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,args){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden});
 }
 sealed class KurulumPenceresi:Form
 {
  readonly TextBox adres=new TextBox();readonly Button kur=new Button();readonly Button kapat=new Button();
  readonly CheckBox baslangic=new CheckBox();readonly Label durum=new Label();readonly ProgressBar ilerleme=new ProgressBar();
  bool mesgul;
  string islem="Kur";
  readonly List<Control> ozelFontlar=new List<Control>();
  static Color Petrol=Color.FromArgb(20,47,54),Yesil=Color.FromArgb(20,121,109);
  public KurulumPenceresi(KurulumBilgisi onizlemeBilgisi=null)
  {
   SuspendLayout();
   AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
   AutoScroll=true;AutoScrollMinSize=new Size(0,590);
   DpiChanged+=(s,e)=>
   {
    AutoScrollMinSize=new Size(0,(int)Math.Round(590*e.DeviceDpiNew/96.0));
    // Kalitilan fontlari Framework olcekler; acik boyut verilenler ayrica yenilenir.
    foreach(var c in ozelFontlar)
    {
     var old=c.Font;c.Font=new Font(old.FontFamily,old.Size*e.DeviceDpiNew/e.DeviceDpiOld,old.Style);old.Dispose();
    }
   };
   Text="Pronom · Kurulum ve güncelleme";ClientSize=new Size(720,590);FormBorderStyle=FormBorderStyle.FixedSingle;
   StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(245,248,247);MaximizeBox=false;
   var ust=new Panel{Dock=DockStyle.Top,Height=110,BackColor=Petrol};Controls.Add(ust);
   var marka=new Label{Text="Pronom",ForeColor=Color.White,Font=new Font("Segoe UI",24,FontStyle.Bold),Location=new Point(28,16),Size=new Size(400,44)};ust.Controls.Add(marka);ozelFontlar.Add(marka);
   ust.Controls.Add(new Label{Text="SOLIDWORKS CO-PILOT  /  DXF DESTEĞİ",ForeColor=Color.FromArgb(174,209,199),Location=new Point(31,69),Size=new Size(610,24)});
   var baslik=new Label{Text="Eklentiyi kullanıma hazır hale getirin",Font=new Font("Segoe UI",16,FontStyle.Bold),ForeColor=Petrol,Location=new Point(30,130),Size=new Size(650,34)};Controls.Add(baslik);ozelFontlar.Add(baslik);
   Controls.Add(new Label{Text="SolidWorks 2023 için dosyalar, eklenti kaydı ve kullanıcı ayarları birlikte hazırlanır.",Location=new Point(32,174),Size=new Size(650,43),ForeColor=Color.FromArgb(80,99,100)});
   Controls.Add(new Label{Text="Merkez adresi",Location=new Point(32,226),Size=new Size(640,25)});
   adres.SetBounds(32,254,650,30);adres.Font=new Font("Segoe UI",12);adres.Text="http://192.0.2.10:5082";
   ozelFontlar.Add(adres);
   try{var ayar=Path.Combine(WindowsKurulumu.Kullanici,"ApiAdresi.txt");if(File.Exists(ayar))adres.Text=File.ReadAllText(ayar).Trim();}catch{}
   Controls.Add(adres);
   baslangic.Text="SolidWorks açıldığında Pronom otomatik yüklensin";baslangic.Checked=true;try{var onceki=WindowsKurulumu.BaslangicOku();if(onceki!=null)baslangic.Checked=Convert.ToInt32(onceki)!=0;}catch{}baslangic.SetBounds(32,302,650,30);Controls.Add(baslangic);
   Controls.Add(new Label{Text="Kurulum: "+WindowsKurulumu.Hedef,Location=new Point(32,351),Size=new Size(650,28),ForeColor=Color.FromArgb(80,99,100)});
   Controls.Add(new Label{Text="Windows kullanıcısı: "+Environment.UserName+"  ·  CAD dosyalarınız ve işlem günlükleriniz korunur.",Location=new Point(32,383),Size=new Size(650,44),ForeColor=Color.FromArgb(80,99,100)});
   durum.Text="Dell merkez API'si açık, SolidWorks kapalı olmalı.";durum.SetBounds(32,431,650,64);durum.ForeColor=Petrol;Controls.Add(durum);
   ilerleme.SetBounds(32,501,650,5);ilerleme.Visible=false;ilerleme.Style=ProgressBarStyle.Marquee;Controls.Add(ilerleme);
   kapat.Text="Kapat";kapat.SetBounds(442,528,100,36);kapat.FlatStyle=FlatStyle.Flat;kapat.Click+=(s,e)=>Close();Controls.Add(kapat);
   kur.Text="Kur";kur.SetBounds(554,528,128,36);kur.BackColor=Yesil;kur.ForeColor=Color.White;kur.FlatStyle=FlatStyle.Flat;kur.Click+=async(s,e)=>await Kur();Controls.Add(kur);
   try
   {
    var bilgi=onizlemeBilgisi??WindowsKurulumu.KurulumBilgisiOku();islem=bilgi.Dugme;kur.Text=islem;
    baslik.Text=bilgi.Baslik;durum.Text=bilgi.Aciklama+" Dell merkez API'si açık, SolidWorks kapalı olmalı.";
   }
   catch(Exception e){kur.Enabled=false;durum.Text="Mevcut kurulum doğrulanamadı: "+e.Message;}
   ResumeLayout(true);
   FormClosing+=(s,e)=>{if(mesgul)e.Cancel=true;};
  }
  void Mesgul(bool value){mesgul=value;kur.Enabled=!value;kapat.Enabled=!value;adres.Enabled=!value;baslangic.Enabled=!value;ilerleme.Visible=value;}
  async Task Kur()
  {
   var otomatik=baslangic.Checked;Mesgul(true);
   try
   {
    var uri=KurulumCekirdegi.ApiAdresi(adres.Text);
    await Task.Run((Action)WindowsKurulumu.OnKontrol);
    durum.Text="Merkez bağlantısı kontrol ediliyor...";
    await WindowsKurulumu.Saglik(uri);
    if(!await Task.Run((Func<bool>)WindowsKurulumu.HazirMi))
    {
     durum.Text="Eklenti kuruluyor. Windows yönetici iznini onaylayın.";
     var id=Guid.NewGuid();
     using(var p=Program.Yukseltilmis("--makine-kur "+id))
     {
      await Task.Run(()=>p.WaitForExit());
      if(p.ExitCode!=0)
      {
       var log=WindowsKurulumu.SonucYolu(id);var bilgi=File.Exists(log)?File.ReadAllText(log):"Windows yönetici izni alınamadı.";
       throw new IOException("Makine kurulumu tamamlanamadı.\r\n"+bilgi);
      }
     }
     if(!await Task.Run((Func<bool>)WindowsKurulumu.HazirMi))throw new IOException("Kurulum dosyaları veya COM kaydı doğrulanamadı.");
    }
    durum.Text="Bağlantı ve kayıt ayarları hazırlanıyor...";
    await Task.Run(()=>
    {
     WindowsKurulumu.KapaliMi();
     KurulumCekirdegi.KullaniciAyarlari(WindowsKurulumu.Kullanici,uri,otomatik,WindowsKurulumu.BaslangicOku,WindowsKurulumu.BaslangicYaz);
    });
    string kisayol="";
    try{WindowsKurulumu.Kisayollar(uri);}catch{kisayol=" Başlat menüsü kısayolu oluşturulamadı.";}
    durum.Text="Kurulum tamamlandı. SolidWorks'ü açabilirsiniz."+kisayol;durum.ForeColor=Yesil;kur.Text="Yeniden kontrol et";
   }
   catch(Exception e){durum.ForeColor=Color.FromArgb(166,64,43);durum.Text="Kurulum tamamlanmadı. Ayarları kontrol edip yeniden deneyin.";MessageBox.Show(this,e.Message,"Pronom",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
   finally{Mesgul(false);}
  }
 }
}
