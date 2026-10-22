using System;
using System.IO;
using System.Runtime.Serialization;
namespace Etiket.SolidWorks
{
 [DataContract]
 public sealed class HedefPencereYaniti
 {
  [DataMember]public int Surum{get;set;}=1;
  [DataMember]public Guid IslemId{get;set;}
  [DataMember]public Guid SorguId{get;set;}
  [DataMember]public int SurecId{get;set;}
  [DataMember]public long Pencere{get;set;}
  [DataMember]public string Klasor{get;set;}="";
 }
 public static class HedefAdresKurali
 {
  public static string Coz(string adres)
  {
   var s=(adres??"").Trim();
   if(s.StartsWith("Adres: ",StringComparison.Ordinal))s=s.Substring(7);
   else if(s.StartsWith("Address: ",StringComparison.Ordinal))s=s.Substring(9);
   else return null;
   if(s.IndexOfAny(new[]{'\r','\n','\0','"','<','>','|','*','?'})>=0||s.StartsWith(@"\\.\",StringComparison.Ordinal))return null;
   var drive=s.Length>=3&&((s[0]>='A'&&s[0]<='Z')||(s[0]>='a'&&s[0]<='z'))&&s[1]==':'&&s[2]=='\\';
   var unc=s.StartsWith(@"\\",StringComparison.Ordinal)&&s.Length>2&&s.Substring(2).Split('\\').Length>=2;
   if(!drive&&!unc)return null;
   if(unc){var p=s.Substring(2).Split('\\');if(string.IsNullOrWhiteSpace(p[0])||string.IsNullOrWhiteSpace(p[1])||s.Contains(":"))return null;}
   else if(s.Substring(2).Contains(":"))return null;
   foreach(var kisim in s.Split('\\'))if(kisim=="."||kisim=="..")return null;
   return Path.GetFullPath(s);
  }
  public static string CiftiDogrula(HedefPencereYaniti[] yanit,Guid islem,Guid ilk,Guid son,int pid,long pencere)
  {
   if(yanit==null||yanit.Length!=2||ilk==son)throw new InvalidDataException("Iki ayri hedef okumasi gerekli.");
   var a=Dogrula(yanit[0],islem,ilk,pid,pencere);
   var b=Dogrula(yanit[1],islem,son,pid,pencere);
   if(!string.Equals(a,b,StringComparison.OrdinalIgnoreCase))throw new IOException("Hedef klasor dogrulama sirasinda degisti; kayit yapilmadi.");
   return b;
  }
  public static string Dogrula(HedefPencereYaniti y,Guid islem,Guid sorgu,int pid,long pencere)
  {
   if(y==null||y.Surum!=1||islem==Guid.Empty||sorgu==Guid.Empty||pid<=0||pencere==0||y.IslemId!=islem||y.SorguId!=sorgu||y.SurecId!=pid||y.Pencere!=pencere)throw new InvalidDataException("Hedef yanıtı açık pencere/işlem/sorguyla uyuşmuyor.");
   var klasor=Coz("Adres: "+y.Klasor);
   if(klasor==null)throw new InvalidDataException("Hedef klasör tam dosya sistemi yolu değil.");
   return klasor;
  }
 }
}
