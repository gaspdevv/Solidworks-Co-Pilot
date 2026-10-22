using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Etiket.SolidWorks
{
 // Common Item Dialog can process a button/Enter before WM_COMMAND reaches
 // its outer #32770. Stop that input before Shell sees it; never synthesize Save.
 public sealed class NativeKaydetGirdiKoruyucusu:IDisposable
 {
  const uint Istek=0x8000+0x251;
  readonly Action<IntPtr> kaydet;
  readonly HookProc hookProc;
  readonly SubProc subProc;
  static readonly UIntPtr Kimlik=new UIntPtr(0x50523235);
  IntPtr hook,dialog,dugme,edit;bool gonderildi;
  delegate IntPtr HookProc(int code,IntPtr w,IntPtr l);
  delegate IntPtr SubProc(IntPtr h,uint m,IntPtr w,IntPtr l,UIntPtr id,UIntPtr data);
  [StructLayout(LayoutKind.Sequential)]struct Mesaj {public IntPtr Hwnd;public uint Id;public IntPtr W,L;public uint Zaman;public int X,Y;public uint Ozel;}
  [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
  [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SetWindowsHookEx(int id,HookProc proc,IntPtr module,uint thread);
  [DllImport("user32.dll")]static extern bool UnhookWindowsHookEx(IntPtr h);
  [DllImport("user32.dll")]static extern IntPtr CallNextHookEx(IntPtr h,int c,IntPtr w,IntPtr l);
  [DllImport("user32.dll")]static extern bool IsWindow(IntPtr h);
  [DllImport("user32.dll")]static extern bool IsWindowEnabled(IntPtr h);
  [DllImport("user32.dll")]static extern bool IsChild(IntPtr parent,IntPtr child);
  [DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr h);
  [DllImport("user32.dll")]static extern IntPtr GetCapture();
  [DllImport("user32.dll")]static extern bool ReleaseCapture();
  [DllImport("user32.dll")]static extern short GetKeyState(int key);
  [DllImport("user32.dll")]static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
  [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
  [DllImport("comctl32.dll")]static extern bool SetWindowSubclass(IntPtr h,SubProc p,UIntPtr id,UIntPtr data);
  [DllImport("comctl32.dll")]static extern bool RemoveWindowSubclass(IntPtr h,SubProc p,UIntPtr id);
  [DllImport("comctl32.dll")]static extern IntPtr DefSubclassProc(IntPtr h,uint m,IntPtr w,IntPtr l);
  public NativeKaydetGirdiKoruyucusu(Action<IntPtr> kaydet)
  {
   this.kaydet=kaydet??throw new ArgumentNullException(nameof(kaydet));hookProc=MesajiOku;subProc=AltPencere;
   hook=SetWindowsHookEx(3,hookProc,IntPtr.Zero,GetCurrentThreadId());
   if(hook==IntPtr.Zero)throw new IOException("Kaydet girdisi koruması kurulamadı: "+Marshal.GetLastWin32Error());
  }
  public void Bagla(IntPtr pencere,IntPtr kaydetDugmesi,IntPtr adAlani)
  {
   var thread=GetCurrentThreadId();var pid=(uint)System.Diagnostics.Process.GetCurrentProcess().Id;
   foreach(var h in new[]{pencere,kaydetDugmesi,adAlani})
    if(h==IntPtr.Zero||GetWindowThreadProcessId(h,out var p)!=thread||p!=pid)throw new IOException("Kaydet girdisi farklı pencere/süreçte.");
   if(dialog!=IntPtr.Zero||!IsChild(pencere,kaydetDugmesi)||!IsChild(pencere,adAlani))throw new IOException("Kaydet girdisi pencereye bağlanamadı.");
   dialog=pencere;dugme=kaydetDugmesi;edit=adAlani;
   if(!SetWindowSubclass(dialog,subProc,Kimlik,UIntPtr.Zero)||!SetWindowSubclass(dugme,subProc,Kimlik,new UIntPtr(1)))
   {Dispose();throw new IOException("Kaydet girdisi denetim koruması kurulamadı.");}
  }
  public static bool Yakalanmali(uint mesaj,long tus,bool kaydetDugmesi,bool adAlani,bool pencere)
  {
   return kaydetDugmesi&&(mesaj==0x0202||mesaj==0x00F5||(mesaj==0x0101&&tus==32))
    ||mesaj==0x0100&&tus==13&&(kaydetDugmesi||adAlani||pencere);
  }
  bool Eslesiyor(IntPtr h,uint mesaj,IntPtr tus)=>dialog!=IntPtr.Zero&&Yakalanmali(mesaj,tus.ToInt64(),h==dugme,h==edit||h==GetParent(edit),h==dialog);
  IntPtr MesajiOku(int code,IntPtr w,IntPtr l)
  {
   if(code>=0&&w==new IntPtr(1)&&l!=IntPtr.Zero&&dialog!=IntPtr.Zero)
   {
    var m=(Mesaj)Marshal.PtrToStructure(l,typeof(Mesaj));
    if(Eslesiyor(m.Hwnd,m.Id,m.W))
    {
     // Clearing the dequeued input also covers Enter consumed by IsDialogMessage.
     m.Id=0;m.W=IntPtr.Zero;m.L=IntPtr.Zero;Marshal.StructureToPtr(m,l,false);Iste();
    }
   }
   return CallNextHookEx(hook,code,w,l);
  }
  void Iste()
  {
   if(gonderildi||dialog==IntPtr.Zero)return;
   gonderildi=true;
   if(!PostMessage(dialog,Istek,IntPtr.Zero,IntPtr.Zero))
   {gonderildi=false;KayitOlayTanisi.Yaz("NativeGirdiHatasi","Kaydet isteği kuyruğa alınamadı; native kayıt engellendi.");}
  }
  IntPtr AltPencere(IntPtr h,uint m,IntPtr w,IntPtr l,UIntPtr id,UIntPtr data)
  {
   if(h==dugme&&Yakalanmali(m,w.ToInt64(),true,false,false)){Iste();return IntPtr.Zero;}
   if(h==dialog&&m==Istek)
   {
    gonderildi=false;
    if(IsWindow(dugme))SendMessage(dugme,0x00F3,IntPtr.Zero,IntPtr.Zero); // BM_SETSTATE false
    if(GetCapture()==dugme)ReleaseCapture();
    if(IsWindow(dialog)&&IsWindowEnabled(dialog))
    {KayitOlayTanisi.Yaz("NativeKaydetGirdisi","pencere="+dialog.ToInt64());kaydet(dialog);}
    return IntPtr.Zero;
   }
   if(m==0x0082)
   {
    RemoveWindowSubclass(h,subProc,Kimlik);
    if(h==dugme)dugme=IntPtr.Zero;
    if(h==dialog){dialog=IntPtr.Zero;edit=IntPtr.Zero;}
   }
   return DefSubclassProc(h,m,w,l);
  }
  public void Dispose()
  {
   if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}
   foreach(var h in new[]{dialog,dugme})if(h!=IntPtr.Zero&&IsWindow(h))RemoveWindowSubclass(h,subProc,Kimlik);
   dialog=dugme=edit=IntPtr.Zero;gonderildi=false;
  }
 }
}
