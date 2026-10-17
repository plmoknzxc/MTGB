// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.util.*;

/** Only observed ordered edges and explicitly public placements, never hidden IDs. */
final class BridgeLibraryPositions {
    private static final class Edges {
        final List<Integer> top=new ArrayList<>(),bottom=new ArrayList<>();
    }
    private record Pending(long revision,List<Integer> top,List<Integer> bottom,boolean explicit) {}
    private record Fixed(long revision,int index,int id) {}
    // Recipient zero is an explicitly public observation, not a union of private looks.
    private final Map<Integer,Map<Integer,Edges>> known=new HashMap<>();
    private final Map<Integer,Map<Integer,Pending>> pending=new HashMap<>();
    private final Map<Integer,List<Fixed>> fixedPending=new HashMap<>(),fixed=new HashMap<>();
    private final Map<forge.game.spellability.SpellAbility,Set<Integer>> privateBatches=new IdentityHashMap<>();
    void clear(){known.clear();pending.clear();fixed.clear();fixedPending.clear();privateBatches.clear();}
    void forget(int owner){known.remove(owner);pending.remove(owner);fixed.remove(owner);fixedPending.remove(owner);}
    void changed(int owner,Integer removed,boolean added,boolean complex) {
        var ability=BridgeCardNotices.cause();
        // A fixed bottom insertion cannot move existing top/interior positions.
        // The incoming cards may still have a private or random order.
        boolean append=added&&!complex&&ability!=null&&"Library".equals(ability.getParam("Destination"))
            &&"-1".equals(ability.getParam("LibraryPosition"));
        if(!append)fixed.remove(owner);
        for(var edge:known.getOrDefault(owner,Map.of()).values()) {
            if(added||complex){if(!append)edge.top.clear();edge.bottom.clear();}
            else if(removed!=null){edge.top.remove(removed);edge.bottom.remove(removed);}
        }
        if(added||complex) {
            boolean fixedPlacement=ability!=null&&"Library".equals(ability.getParam("Destination"))
                &&ability.hasParam("LibraryPosition")&&ability.getParam("LibraryPosition").matches("-?[0-9]+");
            if(!fixedPlacement){var map=pending.get(owner);if(map!=null&&map.get(0)!=null&&!map.get(0).explicit)map.remove(0);fixedPending.remove(owner);}
        }
    }
    void arranged(int recipient,List<Card> shown,List<Card> top,List<Card> bottom) {
        if(shown.isEmpty())return;
        int owner=shown.get(0).getOwner().getId();
        if(shown.stream().anyMatch(c->c.getOwner().getId()!=owner))return;
        stage(owner,recipient,top,bottom);
        boolean publicMove=shown.size()==1&&shown.stream().allMatch(c->c.getZone()!=null&&!c.getZone().getZoneType().isHidden()&&!c.isFaceDown()
            &&ForgeBridge.guis.stream().allMatch(g->c.getView().canBeShownTo(g.controller.getPlayer().getView())));
        if(publicMove)stage(owner,0,top,bottom);
    }
    void orderedMove(int recipient,List<Card> cards,forge.game.spellability.SpellAbility source) {
        if(cards.size()<2||source==null)return;
        // CR 401.4: public origins do not expose the owner's chosen batch order.
        String index=source.getParamOrDefault("LibraryPosition","0");
        for(Player owner:ForgeBridge.game.getPlayers()) {
            List<Card> ordered=new ArrayList<>(cards.stream().filter(c->c.getOwner()==owner).toList());
            if(ordered.size()<2)continue;
            privateBatches.computeIfAbsent(source,k->new HashSet<>()).addAll(ordered.stream().map(Card::getId).toList());
            if(!index.equals("0")&&!index.equals("-1"))continue;
            if(index.equals("0")){Collections.reverse(ordered);stage(owner.getId(),recipient,ordered,List.of());}
            else stage(owner.getId(),recipient,List.of(),ordered);
        }
    }
    private void stage(int owner,int recipient,List<Card> top,List<Card> bottom) {
        pending.computeIfAbsent(owner,k->new HashMap<>()).put(recipient,new Pending(ForgeBridge.revision,
            top.stream().map(Card::getId).toList(),bottom.stream().map(Card::getId).toList(),true));
    }
    void publicPlacement(Card card) {
        var ability=ForgeBridge.game.getAction().getZoneChangeCause();
        if(ability==null||ability.hasParam("RandomOrder")||privateBatches.getOrDefault(ability,Set.of()).contains(card.getId())
            ||!"Library".equals(ability.getParam("Destination")))return;
        String value=ability.getParamOrDefault("LibraryPosition","0");if(!value.matches("-?[0-9]+"))return;
        int index;try{index=Integer.parseInt(value);}catch(NumberFormatException ignored){return;}
        int owner=card.getOwner().getId();
        if(index==0||index==-1) {
            var map=pending.computeIfAbsent(owner,k->new HashMap<>());var previous=map.get(0);
            List<Integer> top=new ArrayList<>(),bottom=new ArrayList<>();
            if(previous!=null&&previous.revision==ForgeBridge.revision){top.addAll(previous.top);bottom.addAll(previous.bottom);}
            if(index==0)top.add(0,card.getId());else bottom.add(card.getId());
            map.put(0,new Pending(ForgeBridge.revision,top,bottom,false));
        } else if(index>0)fixedPending.computeIfAbsent(owner,k->new ArrayList<>()).add(new Fixed(ForgeBridge.revision,Math.min(index,card.getOwner().getCardsIn(ZoneType.Library).size()-1),card.getId()));
    }
    private static boolean matches(List<Card> library,List<Integer> ids,boolean bottom) {
        if(ids.size()>library.size())return false;
        int start=bottom?library.size()-ids.size():0;
        for(int i=0;i<ids.size();i++)if(library.get(start+i).getId()!=ids.get(i))return false;
        return true;
    }
    private void settle(Player owner,List<Card> library) {
        var waiting=pending.get(owner.getId());
        if(waiting!=null)for(var it=waiting.entrySet().iterator();it.hasNext();) {
            var entry=it.next();var value=entry.getValue();if(ForgeBridge.revision<=value.revision)continue;
            if(matches(library,value.top,false)&&matches(library,value.bottom,true)) {
                var edge=known.computeIfAbsent(owner.getId(),k->new HashMap<>()).computeIfAbsent(entry.getKey(),k->new Edges());
                if(!value.top.isEmpty()){edge.top.clear();edge.top.addAll(value.top);}
                if(!value.bottom.isEmpty()){edge.bottom.clear();edge.bottom.addAll(value.bottom);}
            }
            it.remove();
        }
        var placements=fixedPending.get(owner.getId());
        if(placements!=null)for(var it=placements.iterator();it.hasNext();) {
            var value=it.next();if(ForgeBridge.revision<=value.revision)continue;
            if(value.index<library.size()&&library.get(value.index).getId()==value.id)fixed.computeIfAbsent(owner.getId(),k->new ArrayList<>()).add(value);
            it.remove();
        }
    }
    List<Object> snapshot(Player owner,int recipient,Player viewer) {
        List<Card> library=new ArrayList<>(owner.getCardsIn(ZoneType.Library));settle(owner,library);
        if(!ForgeBridge.game.getStack().isResolving()&&ForgeBridge.game.costPaymentStack.peek()==null)privateBatches.clear();
        Map<Integer,Map<String,Object>> result=new TreeMap<>();
        for(int key:new int[]{recipient,0}) {
            var edge=known.getOrDefault(owner.getId(),Map.of()).get(key);if(edge==null)continue;
            if(!matches(library,edge.top,false))edge.top.clear();if(!matches(library,edge.bottom,true))edge.bottom.clear();
            for(int i=0;i<edge.top.size();i++)put(result,library,i,key==0);
            for(int i=0;i<edge.bottom.size();i++)put(result,library,library.size()-edge.bottom.size()+i,key==0);
        }
        for(var place:fixed.getOrDefault(owner.getId(),List.of()))
            if(place.index<library.size()&&library.get(place.index).getId()==place.id)put(result,library,place.index,true);
        for(int i=0;i<library.size();i++) {
            var card=library.get(i);if(!card.getView().canBeShownTo(viewer.getView()))continue;
            boolean publicInfo=ForgeBridge.guis.stream().allMatch(g->card.getView().canBeShownTo(g.controller.getPlayer().getView()));
            put(result,library,i,publicInfo);
        }
        return new ArrayList<>(result.values());
    }
    private static void put(Map<Integer,Map<String,Object>> result,List<Card> library,int index,boolean publicInfo) {
        var previous=result.get(index);
        if(previous!=null&&Boolean.TRUE.equals(previous.get("publicInfo")))publicInfo=true;
        result.put(index,Map.of("position",index+1,"publicInfo",publicInfo,"card",ForgeBridge.card(library.get(index))));
    }
}
