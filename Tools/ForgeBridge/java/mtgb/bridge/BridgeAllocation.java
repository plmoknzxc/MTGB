// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import forge.game.GameEntityView;
import forge.game.card.CardView;
import forge.game.player.PlayerView;
import java.util.*;
import java.util.concurrent.CompletableFuture;

/** Server-owned assignment draft. Constraints mirror the pinned Forge assignment UI. */
final class BridgeAllocation {
    static Map<CardView,Integer> combat(ForgeBridge.Gui gui,CardView source,List<CardView> blockers,int total,GameEntityView defender,boolean override,boolean maySkip) {
        boolean divide=source.getCurrentState().hasDivideDamage();
        List<Object> targets=new ArrayList<>(blockers);
        boolean toDefender=defender!=null && (source.getCurrentState().hasTrample() || divide && override);
        if(toDefender)targets.add(defender);
        int[] lethal=new int[blockers.size()];
        for(int i=0;i<lethal.length;i++) {
            CardView card=blockers.get(i);
            lethal[i]=card.getCurrentState().isPlaneswalker()?Integer.parseInt(card.getCurrentState().getLoyalty()):Math.max(0,card.getLethalDamage());
            if(!card.getCurrentState().isPlaneswalker() && source.getCurrentState().hasDeathtouch())lethal[i]=Math.min(lethal[i],1);
        }
        Map<Object,Integer> raw=run(gui,source,targets,total,null,false,"分配战斗伤害",maySkip,(amounts,index)->{
            if(divide)return true;
            if(index==blockers.size() || !override)
                for(int i=0;i<Math.min(index,lethal.length);i++)if(amounts[i]<lethal[i])return false;
            return true;
        });
        if(raw==null)return null;
        Map<CardView,Integer> result=new LinkedHashMap<>();
        for(Object target:targets)result.put(target==defender?null:(CardView)target,raw.get(target));
        return result;
    }
    static Map<Object,Integer> generic(ForgeBridge.Gui gui,CardView source,Map<Object,Integer> targets,int total,boolean atLeastOne,String label) {
        return run(gui,source,new ArrayList<>(targets.keySet()),total,targets,atLeastOne,"分配 · "+label,false,(amounts,index)->true);
    }
    interface Allowed { boolean test(int[] amounts,int index); }
    static Map<Object,Integer> run(ForgeBridge.Gui gui,CardView source,List<Object> targets,int total,Map<Object,Integer> maximum,boolean atLeastOne,String title,boolean maySkip,Allowed allowed) {
        int[] amounts=new int[targets.size()];if(atLeastOne)Arrays.fill(amounts,1);
        long context=++ForgeBridge.revision;
        while(true) {
            int spent=Arrays.stream(amounts).sum();
            CompletableFuture<Integer> answer=new CompletableFuture<>();
            List<ForgeBridge.Option> options=new ArrayList<>();
            for(int i=0;i<targets.size();i++) {
                final int index=i;Object target=targets.get(i);
                int max=maximum==null || maximum.get(target)==null?total:maximum.get(target);
                if(spent<total && amounts[i]<max && allowed.test(amounts,i))options.add(new ForgeBridge.Option("amount-add:"+i,"+",()->answer.complete(index+1)));
                if(amounts[i]>(atLeastOne?1:0))options.add(new ForgeBridge.Option("amount-sub:"+i,"−",()->answer.complete(-index-1)));
            }
            if(spent==total)options.add(new ForgeBridge.Option("confirm-amount","确认分配",()->answer.complete(Integer.MAX_VALUE)));
            options.add(new ForgeBridge.Option("reset-amount","重置",()->answer.complete(0)));
            if(maySkip)options.add(new ForgeBridge.Option("skip-amount","稍后分配",()->answer.complete(Integer.MIN_VALUE)));
            ForgeBridge.Pending request=new ForgeBridge.Pending(gui.seat,"AssignAmount",title,options);
            request.modal=true;request.contextId=context;request.sourceCard=gui.choiceCard(source);request.sourceCardId=source.getId();
            request.selectedCount=spent;request.remainingCount=total-spent;request.maxCount=total;
            for(int i=0;i<targets.size();i++) {
                Object target=targets.get(i);Map<String,Object> item=new LinkedHashMap<>();item.put("id",i);item.put("amount",amounts[i]);
                item.put("label",target instanceof CardView c?c.getCurrentState().getName():target instanceof PlayerView p?p.getName():String.valueOf(target));
                item.put("card",gui.choiceCard(target));request.allocations.add(item);
            }
            ForgeBridge.publish(request);
            try {
                int action=answer.get();
                if(action==Integer.MIN_VALUE)return null;
                if(action==Integer.MAX_VALUE){Map<Object,Integer> result=new LinkedHashMap<>();for(int i=0;i<targets.size();i++)result.put(targets.get(i),amounts[i]);return result;}
                if(action==0)Arrays.fill(amounts,atLeastOne?1:0);
                else {int index=Math.abs(action)-1;amounts[index]+=action>0?1:-1;}
                // Removing lethal assignment also releases dependent trample/ordered damage.
                for(int i=0;i<amounts.length;i++)if(amounts[i]>0 && !allowed.test(amounts,i))amounts[i]=0;
            }catch(Exception e){throw new IllegalStateException("Unanswered damage assignment",e);}
        }
    }
}
