mergeInto(LibraryManager.library, {
  TT_DeviceScale: function () { return Module.canvas.width / Math.max(1, Module.canvas.getBoundingClientRect().width); },
  TT_Download: function (ptr, length, name) {
    var bytes = HEAPU8.slice(ptr, ptr + length);
    var filename = UTF8ToString(name);
    var blob = new Blob([bytes], {type: 'application/zip'});
    var url = URL.createObjectURL(blob);
    // Keep a visible link for Safari when an asynchronous callback cannot start a download itself.
    var old = document.getElementById('tt-download'); if (old) old.remove();
    var link = document.createElement('a'); link.id = 'tt-download'; link.href = url; link.download = filename;
    link.textContent = '下载实验报告 ZIP'; link.setAttribute('aria-label', '下载实验报告 ' + filename);
    link.style.cssText = 'position:fixed;bottom:12px;left:50%;transform:translateX(-50%);z-index:10000;background:#79dfb9;color:#092b29;padding:14px 22px;border-radius:8px;font:16px sans-serif';
    document.body.appendChild(link); link.click();
    setTimeout(function () { URL.revokeObjectURL(url); link.remove(); }, 120000);
  },
  TT_Number: function (target, label, value, min, max) {
    var old = document.getElementById('tt-number'); if (old) { if(old.ttClose)old.ttClose(true); else old.remove(); }
    var name = UTF8ToString(label), receiver = UTF8ToString(target);
    var backdrop = document.createElement('div'); backdrop.id = 'tt-number';
    backdrop.style.cssText = 'position:fixed;inset:0;background:#000a;z-index:10001;display:flex;align-items:center;justify-content:center;padding:20px';
    var form = document.createElement('form'); form.style.cssText = 'width:min(360px,100%);background:#14272e;color:#f3f6ef;border-radius:12px;padding:24px;font:18px sans-serif';
    var title = document.createElement('label'); title.textContent = name + ' (' + min + ' … ' + max + ')'; title.htmlFor = 'tt-value';
    var input = document.createElement('input'); input.id = 'tt-value'; input.type = 'text'; input.inputMode = 'decimal'; input.value = UTF8ToString(value);
    input.style.cssText = 'box-sizing:border-box;width:100%;font:24px sans-serif;padding:12px;margin:18px 0;color:#14272e';
    var error = document.createElement('p'); error.style.cssText = 'color:#ffc28b;font-size:15px'; error.setAttribute('role','alert');
    var sign = document.createElement('button'); sign.type='button';sign.textContent='±';sign.setAttribute('aria-label','切换正负号');sign.onclick=function(){var v=input.value.trim();input.value=v.startsWith('-')?v.slice(1):'-'+v.replace(/^\+/, '');input.focus();};
    var ok = document.createElement('button'); ok.type='submit'; ok.textContent='应用';
    var cancel = document.createElement('button'); cancel.type='button'; cancel.textContent='取消'; cancel.onclick=function(){close();};
    [ok,cancel,sign].forEach(function(b){b.style.cssText='padding:12px 20px;margin-right:10px;font:18px sans-serif';});
    form.appendChild(title);form.appendChild(input);form.appendChild(error);if(min<0)form.appendChild(sign);form.appendChild(ok);form.appendChild(cancel);backdrop.appendChild(form);document.body.appendChild(backdrop);
    var viewport=window.visualViewport;
    function fit(){if(viewport){backdrop.style.top=viewport.offsetTop+'px';backdrop.style.bottom='auto';backdrop.style.height=viewport.height+'px';}}
    function close(skipNotify){if(viewport)viewport.removeEventListener('resize',fit);backdrop.remove();if(!skipNotify)window.unityInstance.SendMessage(receiver,'CloseWebNumber');}
    backdrop.ttClose=close;form.style.maxHeight='calc(100% - 16px)';form.style.overflowY='auto';
    if(viewport)viewport.addEventListener('resize',fit);fit();
    form.onsubmit=function(e){e.preventDefault();var v=Number(input.value.trim());if(!input.value.trim()||!Number.isFinite(v)||v<min||v>max){error.textContent='请输入范围内的有效数字';return;}
      window.unityInstance.SendMessage(receiver,'ApplyWebNumber',JSON.stringify({label:name,value:v}));close();};
    input.focus(); input.select();
  }
});
