using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
namespace Etiket.SolidWorks
{
 public enum KaydetmeBicimi { FarkliKaydet=0,KopyaVeDevam=1,KopyaVeAc=2,BagimsizBilesen=3,StandartPencere=4 }
 [DataContract]
 public sealed class KaydetmeIslemi
 {
  [DataMember] public Guid IslemId {get;set;}
  [DataMember] public string BelgeTuru {get;set;}="";
  [DataMember] public string HedefKlasor {get;set;}="";
  [DataMember] public string HedefDosya {get;set;}="";
  [DataMember] public string AyrilanDosyaAdi {get;set;}="";
  [DataMember] public string GercekDosya {get;set;}="";
  [DataMember] public string KaynakDosya {get;set;}="";
  [DataMember] public string KayitSHA256 {get;set;}="";
  [DataMember] public long KayitBoyutu {get;set;}
  [DataMember] public string KayitKanitiZamani {get;set;}="";
  [DataMember] public string Asama {get;set;}="AdBekleniyor";
  [DataMember] public KaydetmeBicimi KaydetmeBicimi {get;set;}
 }
 public sealed class AyrilanAd
 {
  public Guid IslemId {get;set;}
  public string DosyaAdi {get;set;}="";
  public string Durum {get;set;}="";
 }
 public interface IAdServisi
 {
  AyrilanAd Ayir(Guid id,string tur);
  void Onayla(Guid id,string beklenenAd);
  void IptalEt(Guid id);
 }
 public static class DosyaAdiKurali
 {
  public static bool Gecerli(string ad,string tur) => (tur=="P"||tur=="M")
   &&Regex.IsMatch(ad??"","^"+tur+"[0-9]{5,19}"+(tur=="P"?@"\.SLDPRT\z":@"\.SLDASM\z"),RegexOptions.CultureInvariant)
   &&!Regex.IsMatch(ad??"",@"^[PM]0+\.");
  public static bool AyniYol(string a,string b) => !string.IsNullOrWhiteSpace(a)&&!string.IsNullOrWhiteSpace(b)
   &&string.Equals(Path.GetFullPath(a),Path.GetFullPath(b),StringComparison.OrdinalIgnoreCase);
 }
}
