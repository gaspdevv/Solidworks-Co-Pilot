using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swpublished;
using SolidWorks.Interop.swconst;
[assembly:ComVisible(false)]
namespace Etiket.SolidWorks
{
 // SetAddinCallbackInfo2 AddinCallbacks parametresi IDispatch olarak marshal edilir.
 // AutoDispatch callback erişimini sağlar; ISwAddin uygulaması korunur.
 [ComVisible(true),Guid("ADC907D3-B1B5-4EAB-9C3A-0B1885FB416C"),ClassInterface(ClassInterfaceType.AutoDispatch)]
 public sealed class PronomEklentisi:ISwAddin
 {
  ISldWorks uygulama;SldWorks olaylar;HttpAdServisi servis;
  KaliciIslemGunlugu gunluk;KaydetmeIslemYoneticisi yonetici;
  NativePencereTanisi nativeTani;
  NativeAdDenemePenceresi nativeAd;
  NativeAdDenemePenceresi standartAd;
  ErtelenmisBelgeTaramasi belgeTaramasi;
  readonly KayitOncesiBelgeBaglama kayitOncesiBaglama=new KayitOncesiBelgeBaglama();
  readonly YuklemeKomutDurumu yuklemeDurumu=new YuklemeKomutDurumu();
  readonly string ornekKimligi=Guid.NewGuid().ToString("N");bool taramaBeklemesiYazildi;
  readonly Dictionary<IntPtr,BelgeOlaylari> belgeler=new Dictionary<IntPtr,BelgeOlaylari>();
  readonly Dictionary<IntPtr,BilesenKayitTanisi> bilesenTanilari=new Dictionary<IntPtr,BilesenKayitTanisi>();
  readonly Queue<Action> bosZamanIslemleri=new Queue<Action>();bool idleIslemiCalisiyor;
  bool bagimsizYazimCalisiyor;
  System.Windows.Forms.Timer kuyrukTaniSayaci;
  int kuyrukIdleSayisi,kuyrukSorguSayisi,kuyrukTaniSayisi;
  string kuyrukSonSorgu="sorgulanmadi";
  DateTime sonrakiKomutDogrulamasi=DateTime.MinValue;
  public bool ConnectToSW(object ThisSW,int Cookie)
  {
   var adim="SolidWorks uygulamasına erişim";
   try
   {
    uygulama=(ISldWorks)ThisSW;
    adim="SolidWorks sürüm kontrolü";
    var revizyon=uygulama.RevisionNumber();
    if(!SolidWorksSurumKurali.TestHedefiMi(revizyon))throw new InvalidOperationException("Bu test paketi SolidWorks 2023 içindir. Okunan API revizyonu: "+revizyon);
    KayitOlayTanisi.Yaz("SurumKontrolu",revizyon);
    adim="Kullanıcı ayarı ve işlem günlüğü";
    servis=new HttpAdServisi(EklentiAyarlari.ApiAdresi());
    gunluk=new KaliciIslemGunlugu(Path.Combine(EklentiAyarlari.KullaniciKlasoru,"Islemler"));
    yonetici=new KaydetmeIslemYoneticisi(servis,gunluk);
    // Native araçlar yalnızca Bağımsız Yap çağrısında oluşturulur.

    belgeTaramasi=new ErtelenmisBelgeTaramasi(BelgeleriBagla);
    adim="Eklenti callback kaydı";
    if(!uygulama.SetAddinCallbackInfo2(0,this,Cookie))throw new InvalidOperationException("SolidWorks callback kaydı oluşturulamadı.");
    adim="SolidWorks uygulama olaylarını bağlama";
    // Olay arayüzünü doğrudan COM nesnesine cast etmeyin; CoClass olay sağlayıcısını kullanın.
    olaylar=(SldWorks)ThisSW;olaylar.FileOpenNotify2+=BelgeAcilmayaBasladi;olaylar.DocumentLoadNotify2+=BelgeYuklendi;olaylar.FileNewNotify2+=YeniBelge;olaylar.FileOpenPostNotify+=BelgeAcildi;olaylar.OnIdleNotify+=BosZaman;
    olaylar.PromptForFilenameNotify+=DosyaAdiIstendi;olaylar.CommandOpenPreNotify+=KomutAciliyor;olaylar.CommandCloseNotify+=KomutKapandi;
    adim="Açık belgelerin olaylarını bağlama";
    belgeTaramasi.Iste();KayitOlayTanisi.Yaz("EklentiHazir",uygulama.RevisionNumber()+" tani=27-yinelenen-komut-sirasi ornek="+ornekKimligi+"");return true;
   }
   catch(Exception h){Bildir("Pronom eklentisi başlatılamadı. Adım: "+adim+". "+h.Message+" ("+h.GetType().Name+", 0x"+h.HResult.ToString("X8")+")");DisconnectFromSW();return false;}
  }
  int YeniBelge(object yeni,int tur,string sablon){KayitOlayTanisi.Yaz("YeniBelgeErtelendi","tur="+tur);belgeTaramasi?.Iste();return 0;}
  int BelgeAcildi(string yol){KayitOlayTanisi.Yaz("BelgeAcildiErtelendi",yol);kayitOncesiBaglama.BelgeAcildi(yol);yuklemeDurumu.BilesenSecimiBitti();belgeTaramasi?.Iste();return 0;}
  int BelgeAcilmayaBasladi(string yol){KayitOlayTanisi.Yaz("FileOpenNotify2",yol);return 0;}
  int BelgeYuklendi(string baslik,string yol){KayitOlayTanisi.Yaz("DocumentLoadNotify2","baslik="+baslik+" yol="+yol);yuklemeDurumu.SecimdeBelgeYuklendi(yol);belgeTaramasi?.Iste();return 0;}
  int DosyaAdiIstendi(int acKaydet,string onerilen,int belgeTuru,int neden)
  {
   if(bagimsizYazimCalisiyor)return 0;
   KayitOlayTanisi.Yaz("PromptForFilenameNotify","acKaydet="+acKaydet+" tur="+belgeTuru+" neden="+neden+" ("+((swPrompForFilenameCause_e)neden)+") ad="+onerilen);
   if(acKaydet==1)
   {
    if(neden==(int)swPrompForFilenameCause_e.swAddComponent)
    {
     yuklemeDurumu.BilesenSecimiBasladi();
     KayitOlayTanisi.Yaz("BilesenSecimiBasladi","sahip="+yuklemeDurumu.SecimiBaslatanKomut+" komutlar="+yuklemeDurumu.BekleyenKomutlar);
    }
    if(neden==(int)swPrompForFilenameCause_e.swPromptForFilename_Cancelled)yuklemeDurumu.BilesenSecimiBitti();
    KayitOlayTanisi.Yaz("PromptAcmaDondu","neden="+neden+" ornek="+ornekKimligi+" native/COM/HTTP yok");
    return 0;
   }
   if(acKaydet==0&&neden==(int)swPrompForFilenameCause_e.swGeneric&&NativeAdDenemePenceresi.StandartKayitAcik
    &&(belgeTuru==(int)swDocumentTypes_e.swDocPART||belgeTuru==(int)swDocumentTypes_e.swDocASSEMBLY))
   {
    try
    {
     var model=uygulama.ActiveDoc as IModelDoc2;
     if(model==null||model.GetType()!=belgeTuru)throw new IOException("Standart kaydın etkin belgesi doğrulanamadı.");
     var kaynak=model.GetPathName();
     if(DxfAktarimKurali.Eslesiyor(belgeTuru==(int)swDocumentTypes_e.swDocPART?"P":"M",kaynak,onerilen))
     {
      if(standartAd?.RezervasyonBekliyor==true||nativeAd?.RezervasyonBekliyor==true)throw new IOException("Önce önceki CAD kayıt işlemini tamamlayın; DXF için yeni numara ayrılmadı.");
      KayitOlayTanisi.Yaz("DxfPencereSerbest","kaynak="+kaynak+" hedef="+onerilen+" merkeziIslem=yok");
      return 0; // SolidWorks'un DXF secim/pencere/export seceneklerini koru.
     }
     if(!string.IsNullOrEmpty(kaynak)&&!DosyaAdiKurali.AyniYol(kaynak,onerilen))throw new IOException("Kayıt penceresi etkin belgeye ait değil; iç içe belge kaydı bu sürümde desteklenmiyor.");
     BelgeyiBagla(model);var kimlik=Marshal.GetIUnknownForObject(model);Marshal.Release(kimlik);
     var bag=belgeler[kimlik];
     if(standartAd==null)standartAd=new NativeAdDenemePenceresi(yonetici);
     standartAd.Hazirla(belgeTuru==(int)swDocumentTypes_e.swDocPART?"P":"M",onerilen,bag.StandartKaydiHazirla,Bildir,true);
    }
    catch(Exception h){KayitOlayTanisi.Yaz("StandartPencereHatasi",h.ToString());Bildir("Standart kayıt hazırlanamadı: "+h.Message+" Dosya kaydetmeyin; İptal seçin.");}
   }
   if(acKaydet==0&&neden==(int)swPrompForFilenameCause_e.swPromptForFilename_Cancelled&&standartAd?.RezervasyonBekliyor==true&&standartAd.IptalEslesiyor(onerilen))
    try{standartAd.IptalEt();}catch(Exception h){Bildir("Standart kayıt iptali/sonucu bekliyor; aynı işlem kimliği korundu. "+h.Message);}
   if(acKaydet==0&&neden==(int)swPrompForFilenameCause_e.swMakeComponentIndependent&&NativeAdDenemePenceresi.ModAcik)
    try
    {
     Action<KaydetmeIslemi,string> kayit=null;
     if(NativeAdDenemePenceresi.GercekKayitAcik)
     {
      var montaj=uygulama.ActiveDoc as IModelDoc2;
      if(montaj==null||montaj.GetType()!=(int)swDocumentTypes_e.swDocASSEMBLY)throw new InvalidOperationException("Etkin montaj bulunamadı.");
      var secim=(ISelectionMgr)montaj.SelectionManager;
      if(secim.GetSelectedObjectCount2(-1)!=1)throw new InvalidOperationException("Bu sürümde tam bir bileşen seçilmelidir.");
      var bilesen=secim.GetSelectedObjectsComponent4(1,-1) as IComponent2??secim.GetSelectedObject6(1,-1) as IComponent2;
      if(bilesen==null)throw new InvalidOperationException("Seçili bileşen bulunamadı.");
      var adaptor=new SolidWorksBagimsizBilesenKaydi(uygulama,montaj,bilesen);
      if(!adaptor.SecimGecerliMi()||!DosyaAdiKurali.AyniYol(adaptor.KaynakYolu,onerilen))throw new InvalidOperationException("Seçili kaynak bileşen doğrulanamadı.");
      kayit=(i,ad)=>BagimsizKaydiYaz(i,ad,adaptor);
     }
     if(nativeAd==null)nativeAd=new NativeAdDenemePenceresi(yonetici);
     nativeAd.Hazirla(belgeTuru==(int)swDocumentTypes_e.swDocPART?"P":belgeTuru==(int)swDocumentTypes_e.swDocASSEMBLY?"M":"X",onerilen,kayit,Bildir);
    }
    catch(Exception h){Bildir("Native ad denemesi: "+h.Message+" Dosya kaydetmeyin; İptal seçin.");}
   if(acKaydet==0&&neden==(int)swPrompForFilenameCause_e.swPromptForFilename_Cancelled&&nativeAd?.RezervasyonBekliyor==true&&nativeAd.IptalEslesiyor(onerilen))
    try{nativeAd.IptalEt();}catch(Exception h){Bildir("Deneme iptali bekliyor; işlem kimliği korundu. "+h.Message);}
   if(acKaydet==0&&neden==(int)swPrompForFilenameCause_e.swMakeComponentIndependent)try{if(nativeTani==null)nativeTani=new NativePencereTanisi();nativeTani.Baslat();}catch(Exception h){KayitOlayTanisi.Yaz("NativeTaniHatasi",h.Message);}
   return 0; // Tanı dinleyicisi native komutu veya dosya adını değiştirmez.
  }
  int KomutAciliyor(int komut,int kullaniciKomutu)
  {
   KayitOlayTanisi.Yaz("CommandOpenPreNotify","komut="+komut+" kullanici="+kullaniciKomutu+" ornek="+ornekKimligi);
   yuklemeDurumu.KomutAcildi(komut);
   try
   {
    IModelDoc2 hedef=null;
    kayitOncesiBaglama.Dene(komut,bagimsizYazimCalisiyor||idleIslemiCalisiyor||yuklemeDurumu.BilesenSecimiSuruyor||nativeAd?.Aktif==true||standartAd?.Aktif==true,
     ()=>{hedef=uygulama.ActiveDoc as IModelDoc2;return hedef?.GetPathName();},
     ()=>{BelgeyiBagla(hedef,false);KayitOlayTanisi.Yaz("KayitOncesiBelgeBaglandi","yol="+hedef.GetPathName()+" bekleyenKomutlar="+yuklemeDurumu.BekleyenKomutlar);});
   }
   catch(Exception h){KayitOlayTanisi.Yaz("KayitOncesiBaglamaHatasi",h.ToString());}
   // 3494: 2023 cihazında gözlenen Bağımsız Yap komutu (swCommands_MakeVirtualCompIndependent).
   if(komut==3494&&!bagimsizYazimCalisiyor&&KayitOlayTanisi.Acik)try{SeciliBilesenleriIzle();if(nativeTani==null)nativeTani=new NativePencereTanisi();nativeTani.Baslat();}catch(Exception h){KayitOlayTanisi.Yaz("BilesenTaniHatasi",h.Message);}
   return 0;
  }
  void SeciliBilesenleriIzle()
  {
   var aktif=uygulama.ActiveDoc as IModelDoc2;
   if(aktif==null||aktif.GetType()!=(int)swDocumentTypes_e.swDocASSEMBLY){KayitOlayTanisi.Yaz("BilesenSecimi","Etkin belge montaj değil.");return;}
   var secim=(ISelectionMgr)aktif.SelectionManager;var adet=secim.GetSelectedObjectCount2(-1);
   KayitOlayTanisi.Yaz("BilesenSecimi","montaj="+aktif.GetPathName()+" adet="+adet);
   for(var i=1;i<=Math.Min(adet,32);i++)
   {
    var bilesen=secim.GetSelectedObjectsComponent4(i,-1) as IComponent2??secim.GetSelectedObject6(i,-1) as IComponent2;
    if(bilesen==null){KayitOlayTanisi.Yaz("BilesenSecimi","sira="+i+" bileşen nesnesi bulunamadı.");continue;}
    var model=bilesen.GetModelDoc2() as IModelDoc2;
    KayitOlayTanisi.Yaz("BilesenSecimi","sira="+i+" sanal="+bilesen.IsVirtual+" yol="+bilesen.GetPathName()+" model="+(model==null?"yok":model.GetType()+":"+model.GetPathName()));
    if(model==null)continue; // Lightweight/suppressed bileşeni tanı için zorla çözmeyin.
    var kimlik=Marshal.GetIUnknownForObject(model);Marshal.Release(kimlik);
    // Aynı .NET olay sağlayıcısına ikinci return0 ekleyip işleyicinin return1 sonucunu bozmayın.
    if(belgeler.ContainsKey(kimlik)){KayitOlayTanisi.Yaz("BilesenTaniMevcut","Normal kayıt işleyicisi zaten bağlı.");continue;}
    if(!bilesenTanilari.ContainsKey(kimlik))bilesenTanilari.Add(kimlik,new BilesenKayitTanisi(model,()=>nativeAd?.Aktif==true));
   }
  }
  int KomutKapandi(int komut,int neden)
  {
   KayitOlayTanisi.Yaz("CommandCloseNotify","komut="+komut+" neden="+neden);
   var secimVardi=yuklemeDurumu.BilesenSecimiSuruyor;yuklemeDurumu.KomutKapandi(komut,neden);
   if(secimVardi&&!yuklemeDurumu.BilesenSecimiSuruyor)KayitOlayTanisi.Yaz("BilesenSecimiBitti","kapananKomut="+komut);
   if(komut==3494&&!bagimsizYazimCalisiyor)try
   {
    var kayit=nativeAd?.KayitKapanisiniAl();nativeTani?.Durdur();nativeAd?.Dispose();
    KayitOlayTanisi.Yaz("BagimsizKuyrukDevir","eylem="+(kayit!=null)+" komutlar="+yuklemeDurumu.BekleyenKomutlar+" secim="+yuklemeDurumu.BilesenSecimiSuruyor);
    if(kayit!=null){bosZamanIslemleri.Enqueue(kayit);KuyrukTanisiniBaslat();}
   }catch(Exception h){KayitOlayTanisi.Yaz("NativeAdTemizlikHatasi",h.Message);}
   if(komut==2||komut==620)try{standartAd?.KayitSonucunuAl();standartAd?.Dispose();}catch(Exception h){KayitOlayTanisi.Yaz("StandartTemizlikHatasi",h.ToString());}
   return 0;
  }
  void BagimsizKaydiYaz(KaydetmeIslemi islem,string ad,SolidWorksBagimsizBilesenKaydi bilesen)
  {
   bagimsizYazimCalisiyor=true;
   try
   {
    if(!bilesen.SecimiGeriYukle())throw new IOException("Etkin montaj veya bileşen seçimi değişti; dosya yazılmadı.");
    KayitOlayTanisi.Yaz("BagimsizKayitBasladi","islem="+islem.IslemId+" hedef="+islem.GercekDosya);
    new BagimsizKayitYoneticisi(yonetici,gunluk).Kaydet(islem,bilesen,ad);
    KayitOlayTanisi.Yaz("BagimsizKayitTamamlandi","islem="+islem.IslemId+" durum="+islem.Asama+" hedef="+islem.GercekDosya);
    belgeTaramasi.Iste();
   }
   catch(Exception h)
   {
    KayitOlayTanisi.Yaz("BagimsizKayitHatasi","islem="+islem.IslemId+" durum="+islem.Asama+" "+h);
    Bildir("Bağımsız kayıt tamamlanamadı veya merkezi sonucu bekliyor. İşlem kimliği: "+islem.IslemId+". Yeni numara ayrılmadı; günlük korundu. "+h.Message);
   }
   finally{bagimsizYazimCalisiyor=false;}
  }
  int BosZaman()
  {
   if(bosZamanIslemleri.Count>0)kuyrukIdleSayisi++;
   if(idleIslemiCalisiyor||(!(belgeTaramasi?.Bekliyor??false)&&bosZamanIslemleri.Count==0))return 0;
   // OnIdle, CommandOpenPre/Close bildirimlerinin dengeli olduğu anlamına gelmez.
   // Yalnızca gerçek bir yükleme/yazım koruması yokken, resmi UI durumu ile
   // eski çizim komutlarının hâlâ kullanıcı arayüzünde etkin olup olmadığını doğrula.
   if(yuklemeDurumu.KomutDogrulanabilir&&DateTime.UtcNow>=sonrakiKomutDogrulamasi)
   {
    sonrakiKomutDogrulamasi=DateTime.UtcNow.AddSeconds(1);idleIslemiCalisiyor=true;
    try
    {
     uygulama.GetRunningCommandInfo(out var komut,out var baslik,out var aktif);
     if(bosZamanIslemleri.Count>0)
     {
      kuyrukSorguSayisi++;kuyrukSonSorgu="komut="+komut+" arayuzAktif="+aktif;
      if(kuyrukSorguSayisi<=3)KayitOlayTanisi.Yaz("BagimsizKuyrukKomutSorgusu",kuyrukSonSorgu+" bekleyen="+yuklemeDurumu.BekleyenKomutlar);
     }
     var onceki=yuklemeDurumu.BekleyenKomutlar;
     if(yuklemeDurumu.CalisanKomutlaDogrula(aktif))
      KayitOlayTanisi.Yaz("KomutDurumuDogrulandi","bekleyen="+onceki+" calisan="+komut+" arayuzAktif=False");
    }
    catch(Exception h){kuyrukSonSorgu="hata="+h.GetType().Name;sonrakiKomutDogrulamasi=DateTime.UtcNow.AddSeconds(10);KayitOlayTanisi.Yaz("KomutDurumuDogrulamaHatasi",h.GetType().Name+": "+h.Message);}
    finally{idleIslemiCalisiyor=false;}
   }
   if(!yuklemeDurumu.Guvenli)
   {
    if(!taramaBeklemesiYazildi){KayitOlayTanisi.Yaz("YuklemeKomutuBekleniyor","Belge taraması/kopya işlemi komut kapanana kadar bekler. komutlar="+yuklemeDurumu.BekleyenKomutlar+" secim="+yuklemeDurumu.BilesenSecimiSuruyor+" sahip="+yuklemeDurumu.SecimiBaslatanKomut);taramaBeklemesiYazildi=true;}
    return 0;
   }
   taramaBeklemesiYazildi=false;
   idleIslemiCalisiyor=true;
   try
   {
    if(belgeTaramasi?.Bekliyor==true)belgeTaramasi.Calistir();
    else
    {
     KayitOlayTanisi.Yaz("BagimsizKuyrukCalisiyor","kuyruk="+bosZamanIslemleri.Count+" idle="+kuyrukIdleSayisi);
     bosZamanIslemleri.Dequeue()();
    }
   }
   catch(Exception h){KayitOlayTanisi.Yaz("BosZamanHatasi",h.ToString());Bildir("Ertelenen eklenti işlemi tamamlanamadı: "+h.Message);}
   finally{idleIslemiCalisiyor=false;if(bosZamanIslemleri.Count==0)KuyrukTanisiniDurdur();}
   return 0;
  }
  void KuyrukTanisiniBaslat()
  {
   KuyrukTanisiniDurdur();kuyrukIdleSayisi=0;kuyrukSorguSayisi=0;kuyrukTaniSayisi=0;kuyrukSonSorgu="sorgulanmadi";
   if(!KayitOlayTanisi.Acik)return;
   // UI zamanlayıcısı yalnızca yerel sayaçları okur: CAD/COM/HTTP veya kuyruk çalıştırma yok.
   // OnIdle gelmiyorsa da bunu ayırt eder. En çok bir dakika / 12 satır.
   kuyrukTaniSayaci=new System.Windows.Forms.Timer{Interval=5000};
   kuyrukTaniSayaci.Tick+=KuyrukTaniTick;kuyrukTaniSayaci.Start();
  }
  void KuyrukTaniTick(object sender,EventArgs e)
  {
   if(bosZamanIslemleri.Count==0||!KayitOlayTanisi.Acik){KuyrukTanisiniDurdur();return;}
   KayitOlayTanisi.Yaz("BagimsizKuyrukBekliyor","kuyruk="+bosZamanIslemleri.Count+" idle="+kuyrukIdleSayisi+" sorgu="+kuyrukSorguSayisi+" son="+kuyrukSonSorgu+" komutlar="+yuklemeDurumu.BekleyenKomutlar+" secim="+yuklemeDurumu.BilesenSecimiSuruyor+" sahip="+yuklemeDurumu.SecimiBaslatanKomut+" guvenli="+yuklemeDurumu.Guvenli+" dogrulanabilir="+yuklemeDurumu.KomutDogrulanabilir+" idleCalisiyor="+idleIslemiCalisiyor);
   if(++kuyrukTaniSayisi>=12)KuyrukTanisiniDurdur();
  }
  void KuyrukTanisiniDurdur()
  {
   if(kuyrukTaniSayaci==null)return;
   kuyrukTaniSayaci.Stop();kuyrukTaniSayaci.Tick-=KuyrukTaniTick;kuyrukTaniSayaci.Dispose();kuyrukTaniSayaci=null;
  }
  bool BelgeAktifMi(IModelDoc2 model)
  {
   var aktif=uygulama.ActiveDoc;if(aktif==null)return false;
   IntPtr a=IntPtr.Zero,b=IntPtr.Zero;
   try{a=Marshal.GetIUnknownForObject(aktif);b=Marshal.GetIUnknownForObject(model);return a==b;}
   finally{if(a!=IntPtr.Zero)Marshal.Release(a);if(b!=IntPtr.Zero)Marshal.Release(b);}
  }
  void BelgeleriBagla()
  {
   KayitOlayTanisi.Yaz("BelgeTaramaBasladi","GetDocuments öncesi");
   var liste=uygulama.GetDocuments();
   KayitOlayTanisi.Yaz("BelgeListesiAlindi",liste is object[] a?"adet="+a.Length:"liste boş");
   if(liste is object[] acik)foreach(var nesne in acik)BelgeyiBagla(nesne as IModelDoc2);
   KayitOlayTanisi.Yaz("BelgeTaramaBitti","");
  }
  void BelgeyiBagla(IModelDoc2 model,bool toparla=true)
  {
   if(model==null)return;
   KayitOlayTanisi.Yaz("BelgeTuruOkunuyor","");var tur=model.GetType();
   if(tur!=(int)swDocumentTypes_e.swDocPART&&tur!=(int)swDocumentTypes_e.swDocASSEMBLY)return;
   KayitOlayTanisi.Yaz("BelgeKimligiOkunuyor","tur="+tur);
   var kimlik=Marshal.GetIUnknownForObject(model);Marshal.Release(kimlik);
   if(belgeler.ContainsKey(kimlik))return;
   if(bilesenTanilari.TryGetValue(kimlik,out var tani)){tani.Dispose();bilesenTanilari.Remove(kimlik);}
   KayitOlayTanisi.Yaz("BelgeOlaylariBaglaniyor","tur="+tur);
   var bag=new BelgeOlaylari(model,yonetici,gunluk,Bildir,()=>BelgeyiAyir(kimlik),a=>bosZamanIslemleri.Enqueue(a),()=>BelgeAktifMi(model),()=>nativeAd?.Aktif==true,()=>bagimsizYazimCalisiyor,()=>standartAd?.Aktif==true);belgeler.Add(kimlik,bag);
   KayitOlayTanisi.Yaz("BelgeYoluOkunuyor","tur="+tur);
   var yol=model.GetPathName();KayitOlayTanisi.Yaz("BelgeBaglandi","tur="+tur+" ad="+yol);
   kayitOncesiBaglama.Baglandi(yol);
   // Tarama artık ayrı OnIdle turunda, SaveAs3 çağrısı bittikten sonra çalışır.
   if(!toparla)return; // Save öncesi yalnızca olay bağlantısı: toparlanma/HTTP yok.
   KayitOlayTanisi.Yaz("BelgeToparlamaBasladi","");
   try{bag.BekleyenOnayiToparla(yol);}catch(Exception h){KayitOlayTanisi.Yaz("BelgeToparlamaHatasi",h.ToString());Bildir("Bekleyen kayıt onayı tamamlanamadı; günlük korundu. "+h.Message);}
   KayitOlayTanisi.Yaz("BelgeToparlamaBitti","");
  }
  void BelgeyiAyir(IntPtr id){if(belgeler.TryGetValue(id,out var b)){belgeler.Remove(id);b.Dispose();}}
  void Bildir(string mesaj)
  {
   try{uygulama?.SendMsgToUser2(mesaj,(int)swMessageBoxIcon_e.swMbWarning,(int)swMessageBoxBtn_e.swMbOk);}catch(Exception){}
  }
  public bool DisconnectFromSW()
  {
   KuyrukTanisiniDurdur();
   if(olaylar!=null){try{olaylar.FileOpenNotify2-=BelgeAcilmayaBasladi;olaylar.DocumentLoadNotify2-=BelgeYuklendi;olaylar.FileNewNotify2-=YeniBelge;olaylar.FileOpenPostNotify-=BelgeAcildi;olaylar.OnIdleNotify-=BosZaman;olaylar.PromptForFilenameNotify-=DosyaAdiIstendi;olaylar.CommandOpenPreNotify-=KomutAciliyor;olaylar.CommandCloseNotify-=KomutKapandi;}catch(Exception){}finally{olaylar=null;}}
   foreach(var b in belgeler.Values)try{b.Dispose();}catch(Exception){}
   foreach(var b in bilesenTanilari.Values)try{b.Dispose();}catch(Exception){}
   bilesenTanilari.Clear();
   nativeTani?.Dispose();nativeTani=null;
   nativeAd?.Dispose();nativeAd=null;
   standartAd?.Dispose();standartAd=null;
   belgeTaramasi?.Temizle();belgeTaramasi=null;kayitOncesiBaglama.Temizle();yuklemeDurumu.Temizle();taramaBeklemesiYazildi=false;
   belgeler.Clear();bosZamanIslemleri.Clear();servis?.Dispose();servis=null;uygulama=null;return true;
  }
  [ComRegisterFunction]
  public static void Kaydet(Type t)
  {
   using(var ana=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))
   using(var k=ana.CreateSubKey(@"SOFTWARE\SOLIDWORKS\Addins\{"+t.GUID+"}"))
   {k.SetValue(null,0,RegistryValueKind.DWord);k.SetValue("Title","Pronom Otomatik Dosya Adlandırma");k.SetValue("Description","Normal Kaydet akışında merkezi P/M dosya adı.");}
  }
  [ComUnregisterFunction]
  public static void KaydiKaldir(Type t)
  {
   using(var ana=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))
    ana.DeleteSubKeyTree(@"SOFTWARE\SOLIDWORKS\Addins\{"+t.GUID+"}",false);
  }
 }
}
