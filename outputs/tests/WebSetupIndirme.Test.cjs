const {chromium}=require('playwright');
const fs=require('node:fs/promises');
const path=require('node:path');
const crypto=require('node:crypto');
const root=path.resolve(__dirname,'../..');
const base=process.argv[2]||'http://127.0.0.1:5082';
const expected=process.argv[3]||path.join(root,'outputs/dagitim/Pronom-SolidWorks2023-DXF24-Kurulum.exe');
const out=process.argv[4]||path.join(root,'work/web-setup-kontrol');
const tarayiciDiski=process.env.PRONOM_TARAYICI_DISK_TESTI!=='0';
let count=0;
function assert(ok,message){if(!ok)throw new Error(message);count++;}
const hash=data=>crypto.createHash('sha256').update(data).digest('hex');
(async()=>{
 await fs.mkdir(out,{recursive:true});
 const expectedHash=hash(await fs.readFile(expected));
 const browser=await chromium.launch({headless:true,downloadsPath:out,...(process.env.PRONOM_TEST_TARAYICI?{executablePath:process.env.PRONOM_TEST_TARAYICI}:{})});
 try{
  for(const [name,width,height] of [['masaustu',1440,1000],['dar-ekran',390,844]]){
   const ctx=await browser.newContext({viewport:{width,height},acceptDownloads:true});
   const page=await ctx.newPage();const errors=[];const writes=[];const logs=[];
   const debug=await ctx.newCDPSession(page);await debug.send('Log.enable');debug.on('Log.entryAdded',e=>logs.push(e.entry));
   page.on('pageerror',e=>errors.push(e.message));
   await page.route('**/api/**',route=>{
    if(!['GET','HEAD'].includes(route.request().method())){writes.push(route.request().url());return route.abort();}
    return route.continue();
   });
   await page.goto(base,{waitUntil:'networkidle'});
   const link=page.getByRole('link',{name:'Eklentiyi indir',exact:true});
   assert(await link.isVisible(),name+': indirme dugmesi gorunur');
   const rect=await link.boundingBox();
   assert(rect.x>=0&&rect.x+rect.width<=width,name+': dugme ekrana sigar');
   assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),name+': sayfa yatay tasmaz');
   assert(await page.locator('#satirlar tr').count()>0,name+': mevcut liste calisir');
   assert((await page.locator('#servisDurumu').innerText())==='Hazır',name+': servis bilgisi calisir');
   await page.screenshot({path:path.join(out,name+'.png'),fullPage:true});
   const urls=[];
   for(let i=0;i<2;i++){
    const done=page.waitForEvent('download');await link.click();const download=await done;
    const url=new URL(download.url());urls.push(url.href);
    assert(url.origin===new URL(base).origin&&url.pathname==='/indir/Pronom-Kurulum.exe',name+': ayni merkezden indirir');
    assert(!!url.searchParams.get('v'),name+': indirme onbellegi yenilenir');
    const file=path.join(out,name+'-'+i+'.exe');
    try{
     if(tarayiciDiski)await download.saveAs(file);
     else{
      // Test tarayicisinin klasor yazma yetkisi yoksa baslatma ve HTTP
      // icerigini ayri sinar; tarayici dosya yazimini gecti saymaz.
      await download.cancel();
      const response=await fetch(download.url());assert(response.ok,name+': setup HTTP200');
      await fs.writeFile(file,Buffer.from(await response.arrayBuffer()));
     }
    }catch(e){
     let tanim;
     try{const dp=await ctx.newPage();await dp.goto('chrome://downloads/');tanim=await dp.evaluate(()=>document.querySelector('downloads-manager')?.shadowRoot?.querySelector('#downloadsList')?.items);await dp.close();}catch{}
     console.error(JSON.stringify({indirme:download.url(),hata:await download.failure(),tarayiciKaydi:logs,indirmeKaydi:tanim},(k,v)=>typeof v==='bigint'?v.toString():v));throw e;
    }
    assert(hash(await fs.readFile(file))===expectedHash,name+': indirilen EXE dogru son paket');
    assert(download.suggestedFilename()==='Pronom-Kurulum.exe',name+': anlasilir dosya adi');
   }
   assert(urls[0]!==urls[1],name+': tekrar tiklamada eski onbellek kullanilmaz');
   assert(errors.length===0,name+': JavaScript hatasi yok');
   assert(writes.length===0,name+': API yazma istegi yok');
   await page.screenshot({path:path.join(out,name+'.png'),fullPage:true});
   await ctx.close();
  }
  const result={durum:'GECTI',kontrol:count,adres:base,SHA256:expectedHash,tarayiciDiski,not:tarayiciDiski?'Gercek tarayici ve dosya indirmesi.':'Gercek tarayici dugmesi/indirme baslatma + ayri HTTPdosyasi; tarayici diske yazimi dogrulanmadi.'};
  await fs.writeFile(path.join(out,'sonuc.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result));
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
