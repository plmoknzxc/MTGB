package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;
/** Projection adapters only, not real casts or status acquisition. */
public final class RoyalHelperProjectionFixture {
 @SuppressWarnings("unchecked") static Map<String,Object> project(Card c){return (Map<String,Object>)ForgeBridge.card(c);}
 static void check(boolean ok,String message){if(!ok)throw new AssertionError(message);}
 public static void main(String[] args)throws Exception{ForgeBridge.run(args,()->{
  Player p=ForgeBridge.game.getPlayers().get(0);List<String> checked=new ArrayList<>();
  p.createMonarchEffect("CN2");p.createInitiativeEffect("CLB");
  for(String name:List.of("The Monarch","The Initiative")){
   Card c=p.getCardsIn(ZoneType.Command).stream().filter(v->name.equals(v.getName())).findFirst().orElseThrow();
   var live=project(c);check(Boolean.TRUE.equals(live.get("effectMarker"))&&name.equals(live.get("helperArtName"))&&((String)live.get("currentRules")).contains("Whenever")&&((Number)live.get("id")).intValue()==c.getId(),"Live helper "+name);checked.add(name+"-live");
   p.getZone(ZoneType.Command).remove(c);var pending=project(c);check(name.equals(pending.get("helperArtName"))&&((Number)pending.get("id")).intValue()==c.getId(),"Pending removed helper");checked.add(name+"-removed");
   Card source=StateBranchFixture.place("Grizzly Bears",p,ZoneType.Battlefield);Card derived=forge.game.ability.SpellAbilityEffect.createEffect(null,source,p,name,source.getImageKey(),p.getGame().getNextTimestamp());check(!project(derived).containsKey("helperArtName"),"Source-derived effect misclassified");checked.add(name+"-source-derived");
  }
  for(ZoneType zone:List.of(ZoneType.Hand,ZoneType.Library)){
   Card c=StateBranchFixture.place("Grizzly Bears",p,zone);c.setName("The Monarch");check(!project(c).containsKey("helperArtName"),"Ordinary hidden card misclassified");checked.add("ordinary-name-"+zone);
  }
  try{Files.writeString(Path.of(ForgeBridge.config.get("output").getAsString()),ForgeBridge.JSON.toJson(Map.of("status","PASS","checks",checked,"kind","projection-adapter")));}catch(Exception e){throw new RuntimeException(e);}
 });}
}
