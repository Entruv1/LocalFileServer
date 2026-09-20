namespace LocalFileServer;

/// <summary>内嵌网页资源（浏览器访问首页时返回）</summary>
public static class WebResources
{
    public static string IndexHtml { get; } = @"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>本地文件服务器</title>
<style>
*{margin:0;padding:0;box-sizing:border-box}
body{font-family:-apple-system,'Segoe UI','Microsoft YaHei',sans-serif;background:#f5f6f8;color:#1f2328;min-height:100vh}
.wrap{max-width:760px;margin:0 auto;padding:24px 16px 60px}
.header{display:flex;align-items:center;gap:12px;flex-wrap:wrap;margin-bottom:20px}
.logo{width:36px;height:36px;border-radius:9px;background:#185fa5;color:#fff;display:flex;align-items:center;justify-content:center;font-size:18px;font-weight:700}
.title{font-size:18px;font-weight:600}
.badge{background:#0f6e56;color:#fff;font-size:12px;padding:3px 10px;border-radius:20px;display:inline-flex;align-items:center;gap:5px}
.badge .dot{width:7px;height:7px;border-radius:50%;background:#7fe0c3;display:inline-block}
.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:12px;margin-bottom:22px}
.card{background:#fff;border:1px solid #e6e8ec;border-radius:12px;padding:14px 16px}
.card .lab{font-size:12px;color:#6b7280;margin-bottom:6px}
.card .val{font-size:20px;font-weight:600}
.card .val.small{font-size:14px;font-weight:500}
.section{font-size:13px;color:#6b7280;font-weight:600;margin:18px 0 10px;display:flex;justify-content:space-between;align-items:center}
.file{background:#fff;border:1px solid #e6e8ec;border-radius:10px;padding:12px 14px;margin-bottom:10px;display:flex;align-items:center;justify-content:space-between;gap:12px;flex-wrap:wrap}
.file .info{min-width:0}
.fname{font-family:Consolas,monospace;font-size:14px;font-weight:500;word-break:break-all}
.fmeta{font-size:12px;color:#6b7280;margin-top:3px}
.cmd{background:#f0f1f3;border-radius:6px;padding:6px 10px;font-family:Consolas,monospace;font-size:12px;color:#374151;margin-top:8px;display:flex;align-items:center;gap:8px;flex-wrap:wrap}
.cmd code{flex:1;min-width:0;word-break:break-all}
.btn{border:1px solid #d1d5db;background:#fff;border-radius:8px;padding:6px 12px;font-size:13px;cursor:pointer;color:#1f2328;transition:background .15s}
.btn:hover{background:#f3f4f6}
.btn.primary{background:#185fa5;border-color:#185fa5;color:#fff}
.btn.primary:hover{background:#0c447c}
.btn.danger{color:#a32d2d;border-color:#e5b6b6}
.btn.danger:hover{background:#fcebeb}
.upload{border:2px dashed #c9ced4;border-radius:12px;padding:18px;text-align:center;color:#6b7280;font-size:13px;margin-bottom:14px;cursor:pointer;transition:background .15s}
.upload:hover,.upload.drag{background:#e6f1fb;border-color:#185fa5}
.toolbar{display:flex;gap:10px;margin-bottom:16px;flex-wrap:wrap}
input[type=text],input[type=password]{border:1px solid #d1d5db;border-radius:8px;padding:8px 10px;font-size:13px;min-width:160px}
.pwdbar{display:flex;gap:8px;align-items:center;flex-wrap:wrap;margin-bottom:16px;background:#fff;border:1px solid #e6e8ec;border-radius:10px;padding:12px 14px}
.pwdbar label{font-size:13px;color:#374151;white-space:nowrap}
.toast{position:fixed;bottom:24px;left:50%;transform:translateX(-50%);background:#1f2328;color:#fff;padding:10px 18px;border-radius:8px;font-size:13px;opacity:0;transition:opacity .25s;pointer-events:none;z-index:99}
.toast.show{opacity:1}
.empty{text-align:center;color:#9ca3af;padding:30px 0;font-size:14px}
</style>
</head>
<body>
<div class=""wrap"">
  <div class=""header"">
    <div class=""logo"">S</div>
    <div class=""title"">本地文件服务器</div>
    <span class=""badge""><span class=""dot""></span>运行中</span>
    <span style=""flex:1""></span>
    <button class=""btn"" onclick=""location.reload()"">刷新</button>
  </div>

  <div class=""cards"">
    <div class=""card""><div class=""lab"">访问地址</div><div class=""val small"" id=""baseUrl"">...</div></div>
    <div class=""card""><div class=""lab"">可抓取文件</div><div class=""val"" id=""fileCount"">0</div></div>
    <div class=""card""><div class=""lab"">下载命令</div><div class=""val small"">wget / curl</div></div>
  </div>

  <div class=""pwdbar"">
    <label>访问口令(可选):</label>
    <input type=""password"" id=""token"" placeholder=""留空则不设口令"" style=""flex:1;min-width:120px"">
    <span style=""font-size:12px;color:#6b7280"">路由器抓取时加 ?token=xxx</span>
  </div>

  <div class=""toolbar"">
    <button class=""btn primary"" onclick=""document.getElementById('upfile').click()"">上传文件到共享目录</button>
    <input type=""file"" id=""upfile"" multiple style=""display:none"" onchange=""uploadFiles(this.files)"">
    <button class=""btn"" onclick=""refresh()"">刷新列表</button>
  </div>

    <div class=""upload"" id=""dropzone"">
    拖拽文件到此处，或点击上方「上传文件」 —— 把脚本放入共享目录后自动出现在下面列表
  </div>

  <div class=""section""><span>共享文件</span><span id=""countBadge""></span></div>
  <div id=""filelist""></div>
  <div class=""empty"" id=""empty"" style=""display:none"">共享目录为空，请先上传或放入脚本文件</div>
</div>
<div class=""toast"" id=""toast""></div>

<script>
var BASE = '__BASE__';
var token = localStorage.getItem('fs_token') || '';
document.getElementById('token').value = token;
function baseUrl(){ return location.protocol+'//'+location.host; }
function auth(){ var t=document.getElementById('token').value.trim(); localStorage.setItem('fs_token',t); return t; }
function showToast(m){ var t=document.getElementById('toast'); t.textContent=m; t.classList.add('show'); setTimeout(function(){t.classList.remove('show')},2200); }
function esc(s){ return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/\x22/g,'&quot;'); }
function fmt(n){ if(n<1024)return n+' B'; if(n<1048576)return (n/1024).toFixed(1)+' KB'; return (n/1048576).toFixed(2)+' MB'; }

function wgetCmd(name){
  var t=document.getElementById('token').value.trim();
  var base=baseUrl();
  var u=base+'/api/download/'+encodeURIComponent(name)+(t?'?token='+encodeURIComponent(t):'');
  return 'wget -O /root/'+name+' \x22'+u+'\x22';
}

function refresh(){
  var x=new XMLHttpRequest();
  x.open('GET','/api/files',true);
  x.onload=function(){
    if(x.status===401){ showToast('需要口令'); return; }
    var d=JSON.parse(x.responseText);
    var list=document.getElementById('filelist');
    var empty=document.getElementById('empty');
    document.getElementById('fileCount').textContent=d.files.length;
    document.getElementById('countBadge').textContent='共 '+d.files.length+' 个文件';
    list.innerHTML='';
    if(d.files.length===0){ empty.style.display='block'; return; }
    empty.style.display='none';
    d.files.forEach(function(f){
      var cmd=wgetCmd(f.name);
      var div=document.createElement('div'); div.className='file';
      div.innerHTML='<div class=""info"">'+
        '<div class=""fname"">'+esc(f.name)+'</div>'+
        '<div class=""fmeta"">'+fmt(f.size)+' &nbsp;|&nbsp; '+esc(f.modified)+'</div>'+
        '<div class=""cmd""><code>'+esc(cmd)+'</code><button class=""btn"" onclick=""copyCmd(this)"">复制</button></div>'+
        '</div>'+
        '<div style=""display:flex;gap:8px;align-items:center"">'+
        '<a class=""btn"" href=""'+encodeURI(f.name)+(document.getElementById('token').value.trim()?'?token='+encodeURIComponent(document.getElementById('token').value.trim()):'')+'\x22 download>下载</a>'+
        '<button class=""btn danger"" onclick=""delFile(this,'+JSON.stringify(f.name)+')"">删除</button>'+
        '</div>';
      list.appendChild(div);
    });
  };
  x.send();
}
function copyCmd(btn){
  var code=btn.parentNode.querySelector('code').textContent;
  // 优先用 Clipboard API（需 HTTPS/localhost），失败回退到 execCommand（HTTP 通用）
  if (navigator.clipboard && navigator.clipboard.writeText) {
    navigator.clipboard.writeText(code).then(function(){ showToast('命令已复制'); })
      .catch(function(){ fallbackCopy(code); });
  } else {
    fallbackCopy(code);
  }
}
function fallbackCopy(text){
  var ta=document.createElement('textarea');
  ta.value=text;
  ta.style.position='fixed'; ta.style.top='-9999px'; ta.style.left='-9999px';
  document.body.appendChild(ta);
  ta.select();
  try {
    document.execCommand('copy');
    showToast('命令已复制');
  } catch(e) {
    showToast('复制失败，请手动选择');
  }
  document.body.removeChild(ta);
}
function delFile(btn,name){
  if(!confirm('确定删除 '+name+' ?'))return;
  var x=new XMLHttpRequest();
  x.open('POST','/api/delete',true);
  x.setRequestHeader('Content-Type','application/json');
  x.onload=function(){ showToast(x.responseText); refresh(); };
  x.send(JSON.stringify({name:name}));
}
function uploadFiles(files){
  var t=document.getElementById('token').value.trim();
  Array.prototype.forEach.call(files,function(f){
    var fd=new FormData(); fd.append('file',f);
    var x=new XMLHttpRequest();
    x.open('POST','/api/upload?name='+encodeURIComponent(f.name)+(t?'&token='+encodeURIComponent(t):''),true);
    x.onload=function(){ showToast(f.name+' 上传完成'); refresh(); };
    x.send(fd);
  });
}
var dz=document.getElementById('dropzone');
dz.addEventListener('dragover',function(e){e.preventDefault();dz.classList.add('drag');});
dz.addEventListener('dragleave',function(){dz.classList.remove('drag');});
dz.addEventListener('drop',function(e){e.preventDefault();dz.classList.remove('drag');if(e.dataTransfer.files.length)uploadFiles(e.dataTransfer.files);});
document.getElementById('token').addEventListener('change',refresh);
document.getElementById('baseUrl').textContent=BASE;
refresh();
</script>
</body>
</html>";
}
