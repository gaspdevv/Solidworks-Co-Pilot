using System;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
namespace Etiket.SolidWorks
{
 // Yalnızca doğrulanmış hedef yol alındıktan sonra OnIdle/UI thread'inde çağrılır.
 internal sealed class SolidWorksBagimsizBilesenKaydi:IBagimsizBilesenKaydi
 {
  readonly ISldWorks uygulama;readonly IModelDoc2 montaj;readonly IComponent2 bilesen;
  readonly string kaynak;
  public SolidWorksBagimsizBilesenKaydi(ISldWorks uygulama,IModelDoc2 montaj,IComponent2 bilesen)
  {this.uygulama=uygulama;this.montaj=montaj;this.bilesen=bilesen;kaynak=bilesen.GetPathName();}
  public string KaynakYolu=>kaynak;
  public string YeniReferansYolu=>bilesen.GetPathName();
  public bool SecimiGeriYukle()
  {
   if(!AyniNesne(uygulama.ActiveDoc,montaj)||bilesen.IsVirtual||!DosyaAdiKurali.AyniYol(kaynak,bilesen.GetPathName()))return false;
   var secim=(ISelectionMgr)montaj.SelectionManager;
   // Native komut kapanırken seçimi temizlediyse yalnızca aynı bileşeni geri seç.
   // Kullanıcının başka bir seçimini sessizce değiştirme.
   if(secim.GetSelectedObjectCount2(-1)==0&&!bilesen.Select4(false,null,false))return false;
   return SecimGecerliMi();
  }
  public bool SecimGecerliMi()
  {
   if(montaj.GetType()!=(int)swDocumentTypes_e.swDocASSEMBLY||!AyniNesne(uygulama.ActiveDoc,montaj)||bilesen.IsVirtual)return false;
   var secim=(ISelectionMgr)montaj.SelectionManager;
   if(secim.GetSelectedObjectCount2(-1)!=1)return false;
   var secilen=secim.GetSelectedObjectsComponent4(1,-1) as IComponent2??secim.GetSelectedObject6(1,-1) as IComponent2;
   return secilen!=null&&AyniNesne(secilen,bilesen)&&DosyaAdiKurali.AyniYol(kaynak,bilesen.GetPathName());
  }
  public bool Kaydet(string hedef)
  {
   if(!SecimGecerliMi())throw new InvalidOperationException("Bağımsız kayıt öncesi montaj veya seçili bileşen değişti.");
   return ((IAssemblyDoc)montaj).MakeIndependent(hedef);
  }
  static bool AyniNesne(object a,object b)
  {
   if(a==null||b==null)return false;IntPtr x=IntPtr.Zero,y=IntPtr.Zero;
   try{x=Marshal.GetIUnknownForObject(a);y=Marshal.GetIUnknownForObject(b);return x==y;}
   finally{if(x!=IntPtr.Zero)Marshal.Release(x);if(y!=IntPtr.Zero)Marshal.Release(y);}
  }
 }
}
