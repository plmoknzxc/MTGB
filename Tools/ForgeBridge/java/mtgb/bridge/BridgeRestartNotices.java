// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import com.google.common.eventbus.Subscribe;
import forge.game.event.GameEventGameRestarted;
import java.util.*;

/** Same native Game restart, never a Match finish or a new scored session. */
public final class BridgeRestartNotices {
    @Subscribe public void restarted(GameEventGameRestarted event) {
        synchronized(ForgeBridge.class) {
            int actor=event.whoRestarted().getId()+1;
            ForgeBridge.restartRevision++;
            ForgeBridge.libraryKnowledge.restarted();
            BridgeCardNotices.restarted();
            long id=ForgeBridge.noticeSequence.incrementAndGet();
            Set<ForgeBridge.Gui> recipients=new LinkedHashSet<>(ForgeBridge.guis);
            recipients.addAll(ForgeBridge.controlGuis);
            for(var gui:recipients) {
                gui.notices.clear();gui.highlightedCards.clear();gui.flipLocations.clear();
                gui.castingAbilities.clear();gui.choiceObjects.clear();gui.spreeModes.clear();
                gui.libraryCostSource=null;gui.replacementChoices=Map.of();gui.optionalReplacement=null;
                gui.replacementAffected=null;gui.sourceChoiceAbility=null;gui.optionalCostAbility=null;
                gui.votingPlayer=null;gui.votingAbility=null;gui.interactionSourceCardId=0;
                gui.actionableCards=Set.of();gui.mulliganReturnCount=0;gui.selectionMin=gui.selectionMax=0;
                gui.targetDraft=null;gui.targetDraftInput=null;gui.contextInput=null;gui.inputContextId=0;
                gui.shownInput=null;gui.inputRevision++;gui.publishedInputRevision=-1;
                Map<String,Object> notice=new LinkedHashMap<>();
                notice.put("id",id);notice.put("kind","restart");notice.put("actorSeat",actor);
                notice.put("publicNotice",true);notice.put("value","");notice.put("message","");notice.put("title","");
                gui.notices.addLast(notice);
            }
        }
    }
}
