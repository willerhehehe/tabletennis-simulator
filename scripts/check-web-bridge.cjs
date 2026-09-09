// Run with NODE_PATH pointing to an installed Playwright package directory.
const fs=require('fs'), path=require('path'), assert=require('assert');
const {chromium}=require('playwright');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_BIN||'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'});
 try {
  const page=await browser.newPage({viewport:{width:390,height:844},hasTouch:true,isMobile:true});
  await page.setContent('<meta name="viewport" content="width=device-width,initial-scale=1"><canvas width="780" style="width:390px"></canvas>');
  await page.addScriptTag({content:`window.LibraryManager={library:{}};window.mergeInto=Object.assign;window.UTF8ToString=x=>x;window.Module={canvas:document.querySelector('canvas')};window.unityInstance={SendMessage:(target,method,data)=>{if(method==='ApplyWebNumber')window.received={target,method,data:JSON.parse(data)};else window.numberClosed=true}};window.HEAPU8=new Uint8Array([80,75,1,2]);`});
  await page.addScriptTag({content:fs.readFileSync(path.join(__dirname,'../Assets/Plugins/WebGL/TableTennis.jslib'),'utf8')});
  assert.equal(await page.evaluate(()=>LibraryManager.library.TT_DeviceScale()),2);
  await page.evaluate(()=>LibraryManager.library.TT_Number('PhysicsLab','挥拍速度','5',0,25));
  await page.locator('#tt-value').fill('100');await page.getByRole('button',{name:'应用',exact:true}).click();
  assert.equal(await page.locator('[role="alert"]').textContent(),'请输入范围内的有效数字');
  await page.locator('#tt-value').fill('7.5');await page.getByRole('button',{name:'应用',exact:true}).click();
  assert.deepEqual(await page.evaluate(()=>window.received),{target:'PhysicsLab',method:'ApplyWebNumber',data:{label:'挥拍速度',value:7.5}});
  assert.equal(await page.locator('#tt-number').count(),0);
  assert(await page.evaluate(()=>window.numberClosed));
  await page.evaluate(()=>LibraryManager.library.TT_Number('PhysicsLab','左右速度','7.5',-20,20));
  await page.getByRole('button',{name:'切换正负号'}).click();
  assert.equal(await page.locator('#tt-value').inputValue(),'-7.5');
  await page.getByRole('button',{name:'取消',exact:true}).click();
  const downloadPromise=page.waitForEvent('download');await page.evaluate(()=>LibraryManager.library.TT_Download(0,4,'experiment.zip'));
  const download=await downloadPromise;assert.equal(download.suggestedFilename(),'experiment.zip');
  const saved=await download.path();assert.deepEqual([...fs.readFileSync(saved)],[80,75,1,2]);
  assert.equal(await page.locator('#tt-download').getAttribute('download'),'experiment.zip');
  console.log('PASS: phone numeric input validates and calls Unity; ZIP download bytes and Safari fallback link are correct.');
 } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1});
