using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
namespace Etiket.SolidWorks
{
 internal sealed class BelgeOlaylari:IDisposable
 {
  const string IslemOzelligi="PronomIslemId";
  readonly IModelDoc2 model;readonly KaydetmeIslemYoneticisi yonetici;readonly KaliciIslemGunlugu gunluk;
  readonly Action<string> bildir;readonly Action kapandi;readonly string tur;
  readonly Action<Action> sirayaAl;readonly Func<bool> aktifMi;
  readonly Func<bool> nativeDenemeAktif;
  readonly Func<bool> programatikBagimsiz;
  readonly Func<bool> standartPencereAktif;
  readonly StandartKayitYoneticisi standartKayit;
  bool kopyaSirada,programatikKopya,kapatildi;
  PartDoc parca;AssemblyDoc montaj;
  KaydetmeIslemi bekleyen;bool isleyiciIcinde,nativeKaydetmeBekleniyor,ozellikDegisti,eskiOzellikVardi;
  string oncekiYol="",eskiOzellik="";
  public BelgeOlaylari(IModelDoc2 model,KaydetmeIslemYoneticisi yonetici,KaliciIslemGunlugu gunluk,Action<string> bildir,Action kapandi,Action<Action> sirayaAl,Func<bool> aktifMi,Func<bool> nativeDenemeAktif,Func<bool> programatikBagimsiz=null,Func<bool> standartPencereAktif=null)
  {
   this.model=model;this.yonetici=yonetici;this.gunluk=gunluk;this.bildir=bildir;this.kapandi=kapandi;
   this.sirayaAl=sirayaAl;this.aktifMi=aktifMi;
   this.nativeDenemeAktif=nativeDenemeAktif;
   this.programatikBagimsiz=programatikBagimsiz??(()=>false);
   this.standartPencereAktif=standartPencereAktif??(()=>false);
   standartKayit=new StandartKayitYoneticisi(yonetici,gunluk);
   tur=model.GetType()==(int)swDocumentTypes_e.swDocPART?"P":"M";
   if(tur=="P")
   {
    KayitOlayTanisi.Yaz("ParcaOlayBagla","olay sağlayıcısı öncesi");
    parca=(PartDoc)model;
    KayitOlayTanisi.Yaz("ParcaOlayBagla","FileSaveAsNotify2");parca.FileSaveAsNotify2+=FarkliKaydet;
    KayitOlayTanisi.Yaz("ParcaOlayBagla","FileSaveNotify");parca.FileSaveNotify+=NormalKaydet;
    KayitOlayTanisi.Yaz("ParcaOlayBagla","FileSavePostNotify");parca.FileSavePostNotify+=Kaydedildi;
    KayitOlayTanisi.Yaz("ParcaOlayBagla","FileSavePostCancelNotify");parca.FileSavePostCancelNotify+=KaydetmeIptal;
    KayitOlayTanisi.Yaz("ParcaOlayBagla","DestroyNotify2");
    parca.DestroyNotify2+=YokEdildi;
   }
   else
   {
    KayitOlayTanisi.Yaz("MontajOlayBagla","olay sağlayıcısı öncesi");
    montaj=(AssemblyDoc)model;
    montaj.FileSaveAsNotify2+=FarkliKaydet;montaj.FileSaveNotify+=NormalKaydet;
    montaj.FileSavePostNotify+=Kaydedildi;montaj.FileSavePostCancelNotify+=KaydetmeIptal;
    montaj.DestroyNotify2+=YokEdildi;
   }
  }
  public void BekleyenOnayiToparla(string bilinenYol=null)
  {
   // Sadece SolidWorks'ün gerçekten açtığı, yolu ve dosya içi kimliği eşleşen belge.
   var yol=bilinenYol??model.GetPathName();if(string.IsNullOrWhiteSpace(yol))return;
   // İlgili bekleyen işlem yoksa yüklenen modelin custom property API'sine dokunmayın.
   var adaylar=gunluk.BekleyenleriOku().Where(x=>x.KaydetmeBicimi!=KaydetmeBicimi.BagimsizBilesen&&x.KaydetmeBicimi!=KaydetmeBicimi.StandartPencere&&(x.Asama=="Ayrildi"||x.Asama=="Kaydedildi")&&DosyaAdiKurali.AyniYol(x.HedefDosya,yol)).ToArray();
   if(adaylar.Length==0)return;
   KayitOlayTanisi.Yaz("BelgeIslemOzelligiOkunuyor",yol);
   var islemMetni=OzelligiOku(out _);
   if(!Guid.TryParse(islemMetni,out var id))return;
   var aday=adaylar.FirstOrDefault(x=>x.IslemId==id);
   if(aday==null||aday.Asama=="IptalBekliyor")return;
   bekleyen=aday;
   yonetici.KaydetmeTamamlandi(aday,yol);bekleyen=null;
  }
  int FarkliKaydet(string dosyaAdi)
  {
   if(programatikBagimsiz())return 0;
   KayitOlayTanisi.Yaz("FileSaveAsNotify2","tur="+tur+" onerilen="+dosyaAdi);
   if(DxfAktarimKurali.DxfAdiMi(dosyaAdi)&&DxfAktarimKurali.Eslesiyor(tur,model.GetPathName(),dosyaAdi))
   {
    if(nativeDenemeAktif()||standartPencereAktif()||standartKayit.Bekliyor||bekleyen!=null||isleyiciIcinde||nativeKaydetmeBekleniyor||kopyaSirada||programatikKopya)
    {bildir("Önce önceki CAD kayıt işlemini tamamlayın. DXF için yeni numara ayrılmadı.");return 1;}
    KayitOlayTanisi.Yaz("DxfKayitSerbest","kaynak="+model.GetPathName()+" hedef="+dosyaAdi+" merkeziIslem=yok");
    return 0; // CAD kimligi/ozelligi/numara/HTTP degismez; native DXF devam eder.
   }
   if(nativeDenemeAktif())return 0; // Pencere denemesinde native UI; gerçek Save ayrı olarak engellenir.
   if(NativeAdDenemePenceresi.StandartKayitAcik)
   {
    if(standartPencereAktif())return 0;
    bildir("Standart kayıt penceresi için işlem hazırlanamadı; kaydı iptal edin ve olay günlüğünü paylaşın.");return 1;
   }
   if(programatikKopya)return 0;
   if(kopyaSirada)return 1;
   if(isleyiciIcinde||nativeKaydetmeBekleniyor)return 1;
   isleyiciIcinde=true;
   try
   {
    if(bekleyen!=null)
    {
     bekleyen=gunluk.Oku(bekleyen.IslemId);
     if(bekleyen.Asama=="Kaydedildi")yonetici.KaydetmeTamamlandi(bekleyen,bekleyen.HedefDosya);
     if(bekleyen.Asama=="IptalBekliyor")yonetici.IptalEt(bekleyen);
     if(bekleyen.Asama=="Onaylandi"||bekleyen.Asama=="Iptal")bekleyen=null;
    }
    if(bekleyen==null)
    {
     using(var secici=new KaydetmeSecimPenceresi(model.GetPathName()))
     {
      if(secici.ShowDialog()!=DialogResult.OK)return 1;
      // Bağlam içi referansların kopyalanması farklı API gerektirir; sessizce yanlış kopya üretmeyin.
      if(secici.Bicim!=KaydetmeBicimi.FarkliKaydet&&model.Extension.ListExternalFileReferencesCount()>0)
       throw new InvalidOperationException("Bu belgenin dış referansları var. Bu sürümde kopya kaydetme yalnızca dış referansı olmayan belgeler için kullanılabilir.");
      bekleyen=yonetici.Baslat(tur,secici.HedefKlasor,secici.Bicim);
     }
    }
    var hedef=yonetici.AdiAl(bekleyen);
    if(bekleyen.KaydetmeBicimi!=KaydetmeBicimi.FarkliKaydet)
    {
     kopyaSirada=true;sirayaAl(()=>KopyayiKaydet(hedef));
     return 1; // İlk native Save iptal; kopya COM callback dışındaki OnIdle'da kaydedilir.
    }
    oncekiYol=model.GetPathName();eskiOzellik=OzelligiOku(out eskiOzellikVardi);ozellikDegisti=true;
    var sonuc=model.Extension.get_CustomPropertyManager("").Add3(IslemOzelligi,(int)swCustomInfoType_e.swCustomInfoText,bekleyen.IslemId.ToString("D"),(int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
    if(sonuc!=(int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged)throw new IOException("İşlem kimliği modele yazılamadı; dosya kaydedilmedi.");
    nativeKaydetmeBekleniyor=true;
    model.SetSaveAsFileName(hedef);
    return 1; // Belgelenmiş S_false: belirlediğimiz adla native Save devam eder.
   }
   catch(Exception h)
   {
    nativeKaydetmeBekleniyor=false;KaynakOzelliginiGeriYukle();bildir(h.Message+" Kaydetme başlatılmadı; mevcut işlem yeniden deneme için korunuyor.");return 1;
   }
   finally{isleyiciIcinde=false;}
  }
  int NormalKaydet(string dosyaAdi)
  {
   if(programatikBagimsiz())return 0;
   KayitOlayTanisi.Yaz("FileSaveNotify","tur="+tur+" hedef="+dosyaAdi);
   if(nativeDenemeAktif())return 1;
   if(NativeAdDenemePenceresi.StandartKayitAcik)
   {
    if(standartKayit.Bekliyor)
    {
     if(standartKayit.Bekleyen.Asama=="NativeYazimBasladi")
     {
      if(!standartPencereAktif()&&!string.IsNullOrWhiteSpace(model.GetPathName())&&DosyaAdiKurali.AyniYol(model.GetPathName(),dosyaAdi))return 0;
      if(standartKayit.HedefEslesiyor(dosyaAdi)||(standartPencereAktif()&&standartKayit.KaydetmeOncesiYolEslesiyor(dosyaAdi)))return 0;bildir("Native kayıt hedefi değişti; işlem korundu.");return 1;
     }
     try{standartKayit.SonucuTekrarDene();}catch(Exception h){bildir("Önceki merkezi sonuç bekliyor. Dosya yeniden yazılmadı; işlem kimliği korundu. "+h.Message);return 1;}
    }
    return 0; // Mevcut dosyada Ctrl+S: HTTP/ad ayırma yok.
   }
   if(programatikKopya)return 0;
   if(kopyaSirada)return 1;
   if(nativeKaydetmeBekleniyor)return 0;
   if(isleyiciIcinde)return 1;
   try
   {
    var yol=model.GetPathName();
    if(string.IsNullOrWhiteSpace(yol))return 0; // İlk Save native FileSaveAsNotify2'ye ilerler.
    // Elle adlandırılmış mevcut dosyada normal Save merkezi kayıt veya yeni numara üretmez.
    if(bekleyen!=null&&bekleyen.Asama=="Kaydedildi")
    {
     try{yonetici.KaydetmeTamamlandi(bekleyen,bekleyen.HedefDosya);bekleyen=null;}
     catch(Exception h){bildir("Dosya adı korunuyor; merkezi onay bekliyor. "+h.Message);}
    }
    return 0; // Normal Save numara veya yeni rezervasyon istemez.
   }
   catch(Exception h){bildir(h.Message);return 1;}
  }
  int Kaydedildi(int kaydetmeTuru,string dosyaAdi)
  {
   if(programatikBagimsiz())return 0;
   KayitOlayTanisi.Yaz("FileSavePostNotify","tur="+tur+" kaydetmeTuru="+kaydetmeTuru+" hedef="+dosyaAdi);
   if(NativeAdDenemePenceresi.StandartKayitAcik)
   {
    if(!standartKayit.Bekliyor)return 0;
    var i=standartKayit.Bekleyen;
    try
    {
     var modelYolu=model.GetPathName();var modelKimligi=OzelligiOku(out _);
     KayitOlayTanisi.Yaz("StandartKayitKaniti","islem="+i.IslemId+" beklenen="+i.GercekDosya+" olay="+dosyaAdi+" model="+modelYolu+" hedefVar="+File.Exists(i.GercekDosya)+" kimlikEslesiyor="+(modelKimligi==i.IslemId.ToString("D")));
     standartKayit.Kaydedildi(kaydetmeTuru,dosyaAdi,modelYolu,modelKimligi);
     KayitOlayTanisi.Yaz("StandartKayitTamamlandi","islem="+i.IslemId+" durum="+i.Asama+" kaydetmeTuru="+kaydetmeTuru+" hedef="+i.GercekDosya);
    }
    catch(Exception h){KayitOlayTanisi.Yaz("StandartKayitHatasi","islem="+i.IslemId+" asama="+i.Asama+" "+h);bildir("Standart kayıt sonucu doğrulanamadı veya merkezi sonuç bekliyor. İşlem kimliği: "+i.IslemId+". "+h.Message);}
    finally{KaynakOzelliginiGeriYukle();}
    return 0;
   }
   if(programatikKopya)return 0; // SaveAs3 dönüş değeri + hata kodu doğrulandıktan sonra onaylanır.
   if(!nativeKaydetmeBekleniyor||bekleyen==null)return 0;
   try
   {
    var yol=dosyaAdi;
    if(string.IsNullOrWhiteSpace(yol)||!Path.IsPathRooted(yol))yol=model.GetPathName();
    if(OzelligiOku(out _)!=bekleyen.IslemId.ToString("D"))throw new IOException("Modelin işlem kimliği uyuşmuyor; onay gönderilmedi.");
    yonetici.KaydetmeTamamlandi(bekleyen,yol);bekleyen=null;
   }
   catch(Exception h){bildir("Kaydetme sonrası merkezi onay tamamlanamadı. İşlem günlüğü korundu. "+h.Message);}
   finally{nativeKaydetmeBekleniyor=false;KaynakOzelliginiGeriYukle();}
   return 0;
  }
  int KaydetmeIptal()
  {
   if(programatikBagimsiz())return 0;
   KayitOlayTanisi.Yaz("FileSavePostCancelNotify","tur="+tur);
   if(NativeAdDenemePenceresi.StandartKayitAcik){KaynakOzelliginiGeriYukle();return 0;}
   if(programatikKopya||kopyaSirada)return 0;
   if(!nativeKaydetmeBekleniyor||bekleyen==null)return 0;
   try{yonetici.IptalEt(bekleyen);bekleyen=null;}
   catch(Exception h){bildir("İptal bildirimi bekliyor; aynı işlem kimliği korundu. "+h.Message);}
   finally{nativeKaydetmeBekleniyor=false;KaynakOzelliginiGeriYukle();}
   return 0;
  }
  public void StandartKaydiHazirla(KaydetmeIslemi islem,string ad)
  {
   if(kapatildi||!aktifMi()||!string.Equals(Path.GetFileName(islem.GercekDosya),BagimsizDosyaAdiKurali.Tamamla(ad,tur),StringComparison.OrdinalIgnoreCase))throw new IOException("Standart kaydın etkin belgesi veya hedef adı değişti.");
   standartKayit.Hazirla(islem,model.GetPathName());
   oncekiYol=model.GetPathName();eskiOzellik=OzelligiOku(out eskiOzellikVardi);ozellikDegisti=true;
   try
   {
    var sonuc=model.Extension.get_CustomPropertyManager("").Add3(IslemOzelligi,(int)swCustomInfoType_e.swCustomInfoText,islem.IslemId.ToString("D"),(int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
    if(sonuc!=(int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged)throw new IOException("Native kayıt işlem kimliği modele yazılamadı.");
   }
   catch{KaynakOzelliginiGeriYukle();throw;}
  }
  string OzelligiOku(out bool var)
  {
   string ham,cozulmus;bool cozuldu,bagli;
   var sonuc=model.Extension.get_CustomPropertyManager("").Get6(IslemOzelligi,false,out ham,out cozulmus,out cozuldu,out bagli);
   var=sonuc!=(int)swCustomInfoGetResult_e.swCustomInfoGetResult_NotPresent;return ham??"";
  }
  void KopyayiKaydet(string hedef)
  {
   try
   {
    if(kapatildi)return;
    if(!aktifMi())throw new InvalidOperationException("Kaynak belge artık etkin değil. Belgeyi etkinleştirip Farklı Kaydet'i tekrar deneyin; aynı işlem korunuyor.");
    if(File.Exists(hedef)||Directory.Exists(hedef))throw new IOException("Hedef dosya zaten var; üzerine yazılmadı.");
    oncekiYol=model.GetPathName();eskiOzellik=OzelligiOku(out eskiOzellikVardi);ozellikDegisti=true;
    var sonuc=model.Extension.get_CustomPropertyManager("").Add3(IslemOzelligi,(int)swCustomInfoType_e.swCustomInfoText,bekleyen.IslemId.ToString("D"),(int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
    if(sonuc!=(int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged)throw new IOException("İşlem kimliği modele yazılamadı.");
    programatikKopya=true;
    bool basarili;int hata=0,uyari=0;
    try
    {
     var secenek=(int)swSaveAsOptions_e.swSaveAsOptions_Silent|
      (bekleyen.KaydetmeBicimi==KaydetmeBicimi.KopyaVeAc?(int)swSaveAsOptions_e.swSaveAsOptions_CopyAndOpen:(int)swSaveAsOptions_e.swSaveAsOptions_Copy);
     basarili=model.Extension.SaveAs3(hedef,(int)swSaveAsVersion_e.swSaveAsCurrentVersion,secenek,null,null,ref hata,ref uyari);
    }
    finally{programatikKopya=false;KaynakOzelliginiGeriYukle();}
    yonetici.ApiKaydetmeTamamlandi(bekleyen,hedef,basarili,hata);bekleyen=null;
    if(uyari!=0)bildir("Kopya kaydedildi. SolidWorks uyarı kodu: "+uyari);
   }
   catch(Exception h){bildir("Kopya kaydetme: "+h.Message);}
   finally{kopyaSirada=false;programatikKopya=false;KaynakOzelliginiGeriYukle();}
  }
  void KaynakOzelliginiGeriYukle()
  {
   if(!ozellikDegisti)return;
   try
   {
    // Save as copy and continue kaynağındaki eski kimliği koru.
    if(string.Equals(model.GetPathName(),oncekiYol,StringComparison.OrdinalIgnoreCase))
    {
     var p=model.Extension.get_CustomPropertyManager("");
     if(eskiOzellikVardi)p.Add3(IslemOzelligi,(int)swCustomInfoType_e.swCustomInfoText,eskiOzellik,(int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
     else p.Delete2(IslemOzelligi);
    }
   }
   catch(Exception){bildir("Kaynak belgenin işlem özelliği geri yüklenemedi. Belgeyi kontrol edin.");}
   finally{ozellikDegisti=false;}
  }
  int YokEdildi(int tur){try{if(tur==(int)swDestroyNotifyType_e.swDestroyNotifyDestroy)kapandi();}catch(Exception h){bildir("Belge olayları ayrılırken hata: "+h.Message);}return 0;}
  public void Dispose()
  {
   kapatildi=true;
   if(parca!=null){parca.FileSaveAsNotify2-=FarkliKaydet;parca.FileSaveNotify-=NormalKaydet;parca.FileSavePostNotify-=Kaydedildi;parca.FileSavePostCancelNotify-=KaydetmeIptal;parca.DestroyNotify2-=YokEdildi;parca=null;}
   if(montaj!=null){montaj.FileSaveAsNotify2-=FarkliKaydet;montaj.FileSaveNotify-=NormalKaydet;montaj.FileSavePostNotify-=Kaydedildi;montaj.FileSavePostCancelNotify-=KaydetmeIptal;montaj.DestroyNotify2-=YokEdildi;montaj=null;}
  }
 }
}
