using System;
using System.IO;
using System.Reflection;

namespace Pronom.Kurulum
{
 // Framework 4.8 DPI ayarini EXE icinde tasir. Ayri AppDomain, WinForms
 // yuklenmeden once belgelenmis ConfigurationFile ayarini uygular.
 static class TekDosyaGiris
 {
  [STAThread]static int Main(string[] args)
  {
   string folder=null;AppDomain domain=null;
   try
   {
    var assembly=Assembly.GetExecutingAssembly();
    folder=Path.Combine(Path.GetTempPath(),"PronomSetup-"+Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    var config=Path.Combine(folder,"Arayuz.config");
    using(var input=assembly.GetManifestResourceStream("Pronom.Arayuz.config"))
    using(var output=new FileStream(config,FileMode.CreateNew,FileAccess.Write,FileShare.None))input.CopyTo(output);
    domain=AppDomain.CreateDomain("Pronom Kurulum",null,new AppDomainSetup{ApplicationBase=Path.GetDirectoryName(assembly.Location),ConfigurationFile=config});
    var giris=(KurulumGiris)domain.CreateInstanceFromAndUnwrap(assembly.Location,typeof(KurulumGiris).FullName);
    return giris.Calistir(args);
   }
   catch(Exception e)
   {
    Console.Error.WriteLine(e);
    if(args.Length==0||args[0]=="--kaldir"||args[0]=="--bekleyen")GirisHatasi.Goster(e.Message);
    return 1;
   }
   finally
   {
    if(domain!=null)AppDomain.Unload(domain);
    if(folder!=null)try{File.Delete(Path.Combine(folder,"Arayuz.config"));Directory.Delete(folder,false);}catch{}
   }
  }
 }
 static class GirisHatasi
 {
  internal static void Goster(string message)=>System.Windows.Forms.MessageBox.Show("Kurulum başlatılamadı. "+message,"Pronom",System.Windows.Forms.MessageBoxButtons.OK,System.Windows.Forms.MessageBoxIcon.Warning);
 }
 public sealed class KurulumGiris:MarshalByRefObject
 {
  public int Calistir(string[] args)=>Program.Calistir(args);
  public override object InitializeLifetimeService()=>null;
 }
}
