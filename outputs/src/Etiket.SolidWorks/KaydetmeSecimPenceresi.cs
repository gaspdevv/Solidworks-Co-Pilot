using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
namespace Etiket.SolidWorks
{
 internal sealed class KaydetmeSecimPenceresi:Form
 {
  readonly Label klasor=new Label{BorderStyle=BorderStyle.FixedSingle,AutoEllipsis=true,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(6,0,6,0)};
  readonly RadioButton normal=new RadioButton{Text="Farklı kaydet",Checked=true,AutoSize=true};
  readonly RadioButton devam=new RadioButton{Text="Kopya olarak kaydet ve devam et",AutoSize=true};
  readonly RadioButton ac=new RadioButton{Text="Kopya olarak kaydet ve aç",AutoSize=true};
  public string HedefKlasor=>klasor.Text;
  public KaydetmeBicimi Bicim=>devam.Checked?KaydetmeBicimi.KopyaVeDevam:ac.Checked?KaydetmeBicimi.KopyaVeAc:KaydetmeBicimi.FarkliKaydet;
  public KaydetmeSecimPenceresi(string kaynakYol)
  {
   Text="Pronom — Farklı Kaydet";StartPosition=FormStartPosition.CenterScreen;
   ClientSize=new Size(560,260);MinimumSize=new Size(500,300);MaximizeBox=false;
   Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
   var duzen=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16),ColumnCount=2,RowCount=6};
   duzen.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));duzen.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
   var bilgi=new Label{Text="Hedef klasörü ve kayıt biçimini seçin. Dosya adını merkezi servis otomatik belirler.",AutoSize=true,Dock=DockStyle.Fill};
   duzen.Controls.Add(bilgi,0,0);duzen.SetColumnSpan(bilgi,2);
   duzen.Controls.Add(klasor,0,1);
   var gozAt=new Button{Text="Göz at…",AutoSize=true};gozAt.Click+=(s,e)=>KlasorSec();duzen.Controls.Add(gozAt,1,1);
   duzen.Controls.Add(normal,0,2);duzen.SetColumnSpan(normal,2);
   duzen.Controls.Add(devam,0,3);duzen.SetColumnSpan(devam,2);
   duzen.Controls.Add(ac,0,4);duzen.SetColumnSpan(ac,2);
   var dugmeler=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.RightToLeft,Dock=DockStyle.Fill};
   var iptal=new Button{Text="İptal",DialogResult=DialogResult.Cancel,AutoSize=true};
   var kaydet=new Button{Text="Kaydet",AutoSize=true};
   kaydet.Click+=(s,e)=>{if(!Directory.Exists(HedefKlasor)){MessageBox.Show(this,"Geçerli bir hedef klasör seçin.",Text);return;}DialogResult=DialogResult.OK;};
   dugmeler.Controls.Add(iptal);dugmeler.Controls.Add(kaydet);duzen.Controls.Add(dugmeler,0,5);duzen.SetColumnSpan(dugmeler,2);
   Controls.Add(duzen);AcceptButton=kaydet;CancelButton=iptal;
   if(!string.IsNullOrWhiteSpace(kaynakYol))klasor.Text=Path.GetDirectoryName(kaynakYol);
  }
  void KlasorSec()
  {
   using(var secici=new FolderBrowserDialog{Description="Yeni dosyanın hedef klasörünü seçin.",SelectedPath=klasor.Text,ShowNewFolderButton=true})
    if(secici.ShowDialog(this)==DialogResult.OK)klasor.Text=secici.SelectedPath;
  }
 }
}
