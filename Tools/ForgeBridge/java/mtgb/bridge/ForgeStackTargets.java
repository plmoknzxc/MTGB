// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.spellability.SpellAbility;
import forge.game.spellability.SpellAbilityStackInstance;
import java.util.*;

/** Read-only confirmed stack targets, never pending GUI target drafts. */
final class ForgeStackTargets {
    static List<Object> project(SpellAbilityStackInstance entry,Player viewer) {
        List<Object> result=new ArrayList<>();Set<String> seen=new HashSet<>();
        for(var part=entry;part!=null;part=part.getSubInstance())for(var target:part.getTargetChoices()) {
            Map<String,Object> value=new LinkedHashMap<>();String key;
            if(target instanceof Player player) {
                key="player:"+player.getId();value.put("kind","player");value.put("id",player.getId());
                value.put("playerSeat",ForgeBridge.game.getPlayers().indexOf(player)+1);
            } else {
                Card card;
                if(target instanceof Card c) {
                    card=c;key="card:"+c.getId()+":"+c.getGameTimestamp();value.put("kind","card");value.put("id",c.getId());
                    var current=ForgeBridge.game.getCardState(c);
                    value.put("leftZone",current==null||current.getGameTimestamp()!=c.getGameTimestamp());
                } else if(target instanceof SpellAbility spell) {
                    card=spell.getHostCard();key="stack:"+spell.getId();value.put("kind","stack");value.put("id",spell.getId());
                    for(var item:ForgeBridge.game.getStack())if(item.getSpellAbility()==spell){value.put("stackId",item.getId());break;}
                } else continue;
                value.put("ownerSeat",ForgeBridge.game.getPlayers().indexOf(card.getOwner())+1);
                value.put("zone",card.getZone()==null?"":card.getZone().getZoneType().name());
                // Even an owner preview must not turn a public face-down target
                // into its private original. Hidden hand/library identities stay hidden.
                if(!card.isFaceDown()&&card.getView().canBeShownTo(viewer.getView()))value.put("card",ForgeBridge.card(card));
                else value.put("card",Map.of("id",card.getId(),"name","Face-down card","faceDown",true,"zone",value.get("zone"),"ownerSeat",value.get("ownerSeat")));
            }
            if(seen.add(key))result.add(value);
        }
        return result;
    }
}
