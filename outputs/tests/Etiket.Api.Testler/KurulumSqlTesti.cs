using Microsoft.Data.SqlClient;
namespace Etiket.Api.Testler;

internal static class KurulumSqlTesti
{
 public static async Task Calistir()
 {
  var ayar=Environment.GetEnvironmentVariable("ConnectionStrings__MerkeziVeritabani")??throw new InvalidOperationException("Test bağlantısını belirtin.");
  var b=new SqlConnectionStringBuilder(ayar);
  var ad=b.InitialCatalog;
  if(!ad.StartsWith("SolidWorksAutomation_ApiTest_",StringComparison.Ordinal)||ad.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='_'))throw new InvalidOperationException("Yalnızca açıkça belirtilen ayrı test veritabanı hazırlanabilir.");
  var kok=new DirectoryInfo(AppContext.BaseDirectory);
  while(kok is not null&&!File.Exists(Path.Combine(kok.FullName,"outputs","sql","004-dosya-adi-rezervasyon.sql")))kok=kok.Parent;
  var betik=Path.Combine(kok?.FullName??Directory.GetCurrentDirectory(),"outputs","sql","004-dosya-adi-rezervasyon.sql");
  if(!File.Exists(betik))throw new InvalidOperationException("Proje kökünden çalıştırın: SQL kurulum dosyası bulunamadı.");
  var ana=new SqlConnectionStringBuilder(ayar){InitialCatalog="master"};
  await using(var c=new SqlConnection(ana.ConnectionString))
  {
   await c.OpenAsync();await using var sorgu=new SqlCommand("SELECT DB_ID(@Ad);",c);sorgu.Parameters.AddWithValue("@Ad",ad);
   if(await sorgu.ExecuteScalarAsync() is not DBNull)throw new InvalidOperationException("Test veritabanı zaten var; silme veya sıfırlama yapılmaz. Yeni ad verin.");
   await using var olustur=new SqlCommand("CREATE DATABASE ["+ad+"];",c){CommandTimeout=60};await olustur.ExecuteNonQueryAsync();
  }
  await using(var c=new SqlConnection(ayar))
  {
   await c.OpenAsync();await using var kurulum=new SqlCommand(await File.ReadAllTextAsync(betik),c){CommandTimeout=60};await kurulum.ExecuteNonQueryAsync();
   // Yeniden kurulum sayacı veya kayıtları sıfırlamamalı.
   await kurulum.ExecuteNonQueryAsync();
   await using var geri=new SqlCommand(await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(betik)!,"005-numara-geri-kullanim.sql")),c){CommandTimeout=60};await geri.ExecuteNonQueryAsync();await geri.ExecuteNonQueryAsync();
  }
  Console.WriteLine("Yeni ve ayrı SQL test veritabanı hazırlandı; ana veritabanına dokunulmadı.");
 }
}
