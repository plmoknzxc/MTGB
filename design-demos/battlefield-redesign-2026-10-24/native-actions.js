(()=>{
const U=window.MTGB_UI_STATES,N=window.MTGB_NATIVE_CAPTURE,data=window.MTGB_DATA;
document.getElementById('stack-list').tabIndex=0;document.getElementById('stack-list').setAttribute('aria-label','堆叠列表');
const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const pass=document.getElementById('pass-button'),actions=document.createElement('div');actions.id='decision-actions';pass.before(actions);actions.append(pass);
const undo=document.createElement('button');undo.id='undo-sample';undo.className='undo-action';undo.type='button';undo.hidden=true;document.getElementById('table').append(undo);
const windows=document.createElement('div');windows.id='native-window-samples';document.getElementById('table').append(windows);
let currentKey='priority',currentLocale='zh-CN';
const button=b=>`<button type="button" class="decision-button ${/Cancel|Reset|EndPhase/.test(b.name)?'secondary':''}" data-layout-action="${esc(b.name)}" ${b.enabled?'':'disabled'}>${esc(b.label||b.name)}</button>`;
function sampleWindows(s){
 windows.innerHTML='';windows.className='';
 const main=s.panels.find(p=>['CardSelection','PileSelection','AbilityOrder','DamageAllocation','MulliganChoices'].includes(p.name))||s.panels.find(p=>!p.inDock&&['CardAbilityChoices','ForgeChoices'].includes(p.name));
 const selected=main?[main]:s.panels.filter(p=>p.name==='TargetOrigin'||p.name==='ChoiceHintPanel');
 windows.classList.toggle('modal',!!main);windows.setAttribute('aria-label',main?'原生选择窗口静态样本':'原生提示静态样本');
 const chinese=U.samples.find(v=>v.key===s.key&&v.locale==='zh-CN');
 for(const p of selected){
  const matching=chinese?.panels.find(v=>v.name===p.name)||p;let r={...matching.bounds};
  if(p.name==='TargetOrigin')for(const b of matching.buttons){let x=Math.min(r.x,b.bounds.x),y=Math.min(r.y,b.bounds.y);r={x,y,width:Math.max(r.x+r.width,b.bounds.x+b.bounds.width)-x,height:Math.max(r.y+r.height,b.bounds.y+b.bounds.height)-y};}
  const tile=document.createElement('div');tile.className='native-sample-tile';let x=r.x;if(p.name==='TargetOrigin')x=Math.min(x,1268-r.width-8);
  Object.assign(tile.style,{left:x+'px',top:r.y+'px',width:r.width+'px',height:r.height+'px'});
  tile.innerHTML=`<img src="native-action-${esc(s.key)}.png" alt="${esc(U.cases.find(c=>c.key===s.key)?.title)}，中文原生静态样本" style="width:${N.width}px;height:${N.height}px;left:${-r.x}px;top:${-r.y}px">`;windows.append(tile);
 }
}
function apply(key=currentKey,locale=currentLocale,scale=1){
 const s=U.samples.find(v=>v.key===key&&v.locale===locale);if(!s)return;
 currentKey=key;currentLocale=locale;document.getElementById('table').style.setProperty('--ui-text-scale',scale);
 const un=s.buttons.find(b=>b.name==='UndoMana');undo.hidden=!un;undo.textContent=un?.label||'';undo.disabled=un?!un.enabled:false;
 const bs=s.buttons.filter(b=>b.name!=='UndoMana');const nums=bs.filter(b=>['Decrease','Increase'].includes(b.name)),rest=bs.filter(b=>!['Decrease','Increase'].includes(b.name));
 actions.innerHTML=(nums.length?`<div class="numeric-controls">${button(nums.find(b=>b.name==='Decrease'))}<output class="numeric-value">${esc(s.numericValue)}</output>${button(nums.find(b=>b.name==='Increase'))}</div>`:'')+(rest.length?`<div class="decision-buttons" data-count="${rest.length}">${rest.map(button).join('')}</div>`:'');
 if(!bs.length)actions.innerHTML='<div class="decision-status">'+(s.key==='priority'?({"zh-CN":"等待操作","en-US":"Waiting","ja-JP":"待機中"}[locale]):({"zh-CN":"请在牌面或窗口中选择","en-US":"Choose on the board or window","ja-JP":"カードかウィンドウで選択"}[locale]))+'</div>';
 sampleWindows(s);document.getElementById('table').dataset.actionSample=key;
}
window.setActionSample=apply;
document.addEventListener('click',e=>{const b=e.target.closest('[data-layout-action],#undo-sample');if(!b||b.disabled)return;b.classList.add('pressed');setTimeout(()=>b.classList.remove('pressed'),180);if(['Increase','Decrease'].includes(b.dataset.layoutAction)){const o=actions.querySelector('output'),n=Number(o.textContent);o.textContent=String(Math.max(0,n+(b.dataset.layoutAction==='Increase'?1:-1)));}});
const originalStack=data.stack.map(s=>({...s,targets:[...s.targets]}));
window.setStackCount=count=>{
 count=Math.max(0,Math.min(30,Number(count)));const items=Array.from({length:count},(_,i)=>{const o=originalStack[i%originalStack.length];return{...o,id:'layout-'+i,targets:o.targets.length&&i+1<count?['layout-'+(i+1)]:[]};});
 window.updatePreviewStack(items);document.querySelector('.stack-area').hidden=count===0;document.querySelector('.stack-heading').innerHTML=`堆叠<span>${count===2?'从上到下结算':'布局样本 · '+count+' 项'}</span>`;document.getElementById('stack-list').scrollTop=0;
};
window.setDense=on=>window.setStackCount(on?12:2);
apply();
})();
