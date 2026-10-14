// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import com.google.common.eventbus.Subscribe;
import forge.game.event.GameEventRandomLog;
import java.util.LinkedHashMap;
import java.util.Map;

/** Preserve the native public pre-clear result, without advancing any input. */
public final class BridgeCleanupNotices {
    @Subscribe public void logged(GameEventRandomLog event) {
        // This event also carries voting, random counters and match removal.
        // Those consumers retain their own presentation contracts. The pinned
        // EventBus posts synchronously, so identify this exact native producer.
        boolean cleanup=StackWalker.getInstance().walk(frames->frames.anyMatch(f->
            f.getClassName().equals("forge.game.ability.effects.CleanUpEffect")
                && f.getMethodName().equals("resolve")));
        if(!cleanup || !ForgeBridge.game.getStack().isResolving())return;
        var ability=ForgeBridge.game.getStack().peekAbility();
        if(ability==null)return;
        var source=ability.getHostCard();
        // No literal Log parent overrides Defined. Do not guess a different
        // source from a name match when a future producer has that contract.
        if(!event.message().startsWith(source.getTranslatedName()+"'s chosen ")
                && !event.message().equals(source.getTranslatedName()+" found no valid choices."))return;
        synchronized(ForgeBridge.class) {
            long id=ForgeBridge.noticeSequence.incrementAndGet();
            int actor=ForgeBridge.game.getRegisteredPlayers().indexOf(ability.getActivatingPlayer())+1;
            for(var gui:ForgeBridge.guis) {
                Map<String,Object> notice=new LinkedHashMap<>();
                notice.put("id",id);notice.put("message",event.message());
                notice.put("kind","cleanup");notice.put("value",event.message());
                notice.put("actorSeat",actor);notice.put("publicNotice",true);
                boolean visible=!source.isFaceDown()&&source.getView().canBeShownTo(gui.controller.getPlayer().getView());
                notice.put("title",visible?source.getName():"");
                if(visible)notice.put("sourceCard",ForgeBridge.card(source));
                gui.notices.addLast(notice);while(gui.notices.size()>16)gui.notices.removeFirst();
            }
        }
    }
}
