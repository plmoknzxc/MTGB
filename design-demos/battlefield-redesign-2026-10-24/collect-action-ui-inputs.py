from pathlib import Path
import json,hashlib,copy
W=Path(__file__).parent
out=W/'action-ui-inputs';out.mkdir(exist_ok=True)
cases=[]
def add(key,title,path,category='dock'):
 p=Path(path);v=json.loads(p.read_text(encoding='utf-8-sig'))
 dest=out/(key+'.json');dest.write_text(json.dumps(v,ensure_ascii=False),encoding='utf-8')
 cases.append(dict(key=key,title=title,path=str(dest).replace('\\','/'),source=str(p).replace('\\','/'),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),category=category))
def folder(meta):return Path(json.loads(Path(meta).read_text(encoding='utf-8-sig'))['folder'])
add('priority','普通优先权',W/'native-full-seat.json')
v=json.loads((W/'native-full-seat.json').read_text(encoding='utf-8-sig'));v['awaitingInput']=True;v['request']=dict(requestId=9000001,seat=1,kind='InputPassPriority',options=[dict(id='ok',label='OK'),dict(id='cancel',label='Undo')])
(out/'undo.json').write_text(json.dumps(v,ensure_ascii=False),encoding='utf-8');cases.append(dict(key='undo',title='优先权与撤回',path=str(out/'undo.json').replace('\\','/'),source='Explicit display adapter: same native contract used by ArenaPresentationValidation.ValidateMenuAndUndo',category='dock',adapter=True))
for key,title,path,cat in [
 ('payment','支付／取消','.tools/payment-dock-1005/native/tax-auto-payment.json','dock'),
 ('hand','手牌确认／取消','.tools/resolution-ui-1005/native/growth-yes-select-1.json','dock'),
 ('number','X 数值','.tools/resolution-ui-1005/native/wrath-two-number-3.json','dock'),
 ('energy','能量数量','.tools/resolution-ui-1005/native/wrath-two-select-0.json','dock'),
 ('attack','宣告攻击','Tools/Validation/Reports/AttackDefenders/initial.json','dock'),
 ('block','宣告阻挡','Tools/Validation/Reports/AdvancedInteractions/damage-blocking.json','dock'),
 ('search','搜寻选牌','Tools/Validation/Reports/CardSelection/fetch-live-choice.json','modal'),
 ('graveyard','坟场选牌','Tools/Validation/Reports/CardSelection/graveyard-choice.json','modal'),
 ('discard','对手手牌／弃牌','Tools/Validation/Reports/Thoughtseize/choice.json','modal'),
 ('order-cards','卡牌排序','.tools/resolution-ui-1005/native/brain-two-order-2.json','modal'),
 ('order-triggers','异能排序','Tools/Validation/Reports/StackInteractions/triggers-order.json','modal'),
 ('modes','模式／加码','Tools/Validation/Reports/ModeChoices/spree-targets-choices.json','modal'),
 ('piles','分堆','Tools/Validation/Reports/Piles/fof-left-choice.json','modal'),
 ('entry','进场选项','.tools/payment-dock-1005/native/shock-yes-confirm.json','popup'),
 ('mana-choice','产费选项','Tools/Validation/Reports/Interactions/mana-choice.json','popup')]:add(key,title,path,cat)
t=folder('.tools/target-branches-1006/native/shock-reset-cancel-result.json')
add('target','目标确认／重选',t/'target-151-1.json','target')
add('mulligan','保留／调度',t/'step-0.json','modal')
f=folder('.tools/mana-variants-1006/native/phyrexian-life-result.json')
add('phyrexian','法术力与生命支付',f/'life-payment-1.json','dock')
f=folder('.tools/quantity-allocation-1006/native/counter-division-result.json')
add('allocation','数量／伤害分配',f/'allocation-confirm.json','modal')
for key,title,base,changes in [('number-large','大数值（格式样本）','number',dict(selectedValue=2147483647)),('manual-number','数值＋取消（布局样本）','number',dict(paymentMode='manual'))]:
 v=json.loads((out/(base+'.json')).read_text(encoding='utf-8'));v['request'].update(changes)
 if key=='manual-number':v['request']['options'].append(dict(id='cancel',label='Cancel'))
 dest=out/(key+'.json');dest.write_text(json.dumps(v,ensure_ascii=False),encoding='utf-8');cases.append(dict(key=key,title=title,path=str(dest).replace('\\','/'),source='Explicit numeric layout adapter derived from '+base,category='dock',adapter=True))
(W/'action-ui-manifest.json').write_text(json.dumps(cases,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(cases),'presentation cases; explicit adapters are marked')
