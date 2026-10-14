package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;
import java.util.*;

/** A state/layer boundary probe, not a cast/payment or Unity interaction test. */
public final class ReferenceStateProbe {
 static Card add(String name,Player p) {
  var paper=FModel.getMagicDb().getCommonCards().getCard(name);
  if(paper==null)throw new IllegalArgumentException("Missing card: "+name);
  Card c=Card.fromPaperCard(paper,p);p.getZone(ZoneType.Battlefield).add(c,0);
  c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);return c;
 }
 static void remove(Card c) { c.getController().getZone(ZoneType.Battlefield).remove(c);c.getOwner().getZone(ZoneType.Graveyard).add(c,0); }
 static void check(boolean condition,String message) {if(!condition)throw new IllegalStateException(message);}
 static void update() {ForgeBridge.game.getTriggerHandler().resetActiveTriggers();ForgeBridge.game.getAction().checkStaticAbilities(false);}
 static Map<String,Object> describe(Card c) {return Map.of("name",c.getName(),"type",c.getType().toString(),"mana",c.getManaAbilities().stream().map(a->a.getManaPart().getOrigProduced()).toList(),"wire",ForgeBridge.card(c));}
 static void snapshot(String label,List<Card> cards) {ForgeBridge.send(Map.of("type","probe-snapshot","label",label,"cards",cards.stream().map(ReferenceStateProbe::describe).toList(),"views",ForgeBridge.views()));}
 static long markerCount(Player p,String name) {return p.getCardsIn(ZoneType.Command).stream().filter(c->c.getName().equals(name)).count();}
 public static void main(String[] args)throws Exception {ForgeBridge.run(args,()->{
  Player p=ForgeBridge.game.getPlayers().get(0),op=ForgeBridge.game.getPlayers().get(1);
  String mode=ForgeBridge.config.get("probe").getAsString();
  if(mode.equals("story")) {
   Card mox=add("Mox Opal",p),ring=add("Sol Ring",p);update();check(!p.hasEnduringStory(),"No source, no story");
   Card source=add("Óin the Brave",p);update();
   check(p.hasEnduringStory(),"Three distinct historic permanents must grant story");
   check(source.getNetPower()==2&&source.hasKeyword("Haste"),"Story modifies Oin");
   snapshot("story-earned",List.of(source,mox,ring));
   remove(mox);remove(ring);remove(source);update();check(p.hasEnduringStory(),"Story persists without source/qualifiers");
   check(markerCount(p,"An Enduring Story")==1,"One persistent story marker");
   snapshot("story-persistent",List.of());
  } else if(mode.equals("story-unique")) {
   Card mox=add("Mox Opal",p),source=add("Óin the Brave",p);update();
   check(!p.hasEnduringStory(),"Legendary artifact counts once, not twice");
   remove(source);Card ring=add("Sol Ring",p),other=add("Ornithopter",p);update();
   check(!p.hasEnduringStory(),"Three historic objects without storied source do not grant story");
   snapshot("story-negative",List.of(mox,ring,other));
  } else if(mode.equals("ascend")) {
   List<Card> field=new ArrayList<>();for(int i=0;i<10;i++)field.add(add("Forest",p));update();
   check(!p.hasBlessing(),"Ten permanents without ascend do not grant blessing");
   remove(field.remove(0));remove(field.remove(0));Card source=add("Ocelot Pride",p);update();
   check(!p.hasBlessing(),"Ascend below ten does not grant blessing");field.add(add("Forest",p));update();
   check(p.hasBlessing(),"Ascend at ten permanents grants blessing");snapshot("blessing-earned",List.of(source));
   for(Card c:field)remove(c);remove(source);update();
   check(p.hasBlessing(),"Blessing persists with zero permanents");check(markerCount(p,"City's Blessing")==1,"One blessing marker");snapshot("blessing-persistent",List.of());
  } else if(mode.equals("saga-sba")) {
   Card saga=add("Urza's Saga",p),moon=add("Blood Moon",p);update();snapshot("before-sba",List.of(saga,moon));
   ForgeBridge.game.getAction().checkStateEffects(false);
   check(saga.isInZone(ZoneType.Battlefield)&&!saga.hasChapter(),"Current rules: chapterless Saga survives SBA");snapshot("after-sba",List.of(saga));
   remove(moon);update();check(saga.hasChapter(),"Chapter abilities return when moon leaves");snapshot("saga-restored",List.of(saga));
  } else {
   var names=List.of("Forest","Snow-Covered Forest","Wooded Foothills","Breeding Pool","Minamo, School at Water's Edge","Darksteel Citadel","Hive of the Eye Tyrant","Dryad Arbor","Urza's Saga","Scrying Sheets","Plains","Island","Swamp","Mountain");
   List<Card> lands=new ArrayList<>();for(String n:names)lands.add(add(n,p));lands.add(add("Wooded Foothills",op));
   var original=lands.stream().map(c->c.getType().toString()).toList();var basic=lands.stream().map(c->c.getType().isBasic()).toList();
   var originalMana=lands.stream().map(c->new HashSet<>(c.getManaAbilities().stream().map(a->a.getManaPart().getOrigProduced()).toList())).toList();List<Card> effects=new ArrayList<>();
   for(var n:ForgeBridge.config.getAsJsonArray("effects"))effects.add(add(n.getAsString(),p));update();
   String expect=ForgeBridge.config.get("mana").getAsString();Set<String> colors=new HashSet<>();for(char x:expect.toCharArray())colors.add(String.valueOf(x));
   for(int i=0;i<lands.size();i++) {
    Card c=lands.get(i);var mana=new HashSet<>(c.getManaAbilities().stream().map(a->a.getManaPart().getOrigProduced()).toList());
    if(i<names.size()&&!basic.get(i))check(mana.equals(colors),"Mana mismatch: "+c.getName()+" "+mana+" wanted "+colors);
    if(basic.get(i))check(mana.equals(effects.stream().anyMatch(e->e.getName().equals("Dryad of the Ilysian Grove"))?new HashSet<>(List.of("W","U","B","R","G")):originalMana.get(i)),"Basic land mana and controller scope");
    check(c.getType().isBasic()==basic.get(i),"Basic/nonbasic supertype retained");
    var wire=(Map<?,?>)ForgeBridge.card(c);check(wire.get("currentType").equals(c.getType().toString()),"Effective type reaches wire");
   }
   check(lands.get(1).getType().isSnow()&&lands.get(9).getType().isSnow(),"Basic and nonbasic snow supertype retained");check(lands.get(4).getType().isLegendary(),"Legendary retained");
   check(lands.get(5).getType().isArtifact(),"Artifact retained");check(lands.get(7).isCreature(),"Land creature retained");
   String enemyColor="";for(Card e:effects)if(e.getName().equals("Blood Moon"))enemyColor="R";else if(e.getName().equals("Harbinger of the Seas"))enemyColor="U";
   check(new HashSet<>(lands.get(names.size()).getManaAbilities().stream().map(a->a.getManaPart().getOrigProduced()).toList()).equals(Set.of(enemyColor)),"Moon affects opponent; own Dryad does not");
   snapshot("land-active",lands);for(Card e:effects)remove(e);update();
   for(int i=0;i<lands.size();i++)check(lands.get(i).getType().toString().equals(original.get(i)),"Restored type: "+lands.get(i).getName());
   snapshot("land-restored",lands);
  }
  ForgeBridge.send(Map.of("type","probe-pass","probe",mode));
 });}
}
