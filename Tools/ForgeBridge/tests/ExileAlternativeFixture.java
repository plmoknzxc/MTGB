package mtgb.bridge;
import forge.game.card.Card;
import forge.game.zone.ZoneType;
import forge.model.FModel;
public final class ExileAlternativeFixture {
 public static void main(String[] args)throws Exception{ForgeBridge.run(args,()->{
  var p=ForgeBridge.game.getPlayers().get(0);
  String mode=ForgeBridge.config.get("case").getAsString();
  String name=mode.equals("plot")?"Slickshot Show-Off":mode.equals("external-foretell")?"Grizzly Bears":"Sarulf's Packmate";
  for(int i=0;i<6;i++){
   Card c=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name.equals("Slickshot Show-Off")?"Mountain":"Forest"),p);p.getZone(ZoneType.Battlefield).add(c);c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);
  }
  for(String n:new String[]{name,"Sphere of Resistance","Chiss-Goria, Forge Tyrant"}){
   if(n.equals("Sphere of Resistance")&&ForgeBridge.config.has("tax")&&ForgeBridge.config.get("tax").getAsInt()==0)continue;
   Card c=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(n),p);p.getZone(n.equals(name)?ZoneType.Hand:ZoneType.Battlefield).add(c);c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);
  }
  if(mode.equals("external-foretell")){
   Card c=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard("Dream Devourer"),p);p.getZone(ZoneType.Battlefield).add(c);c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());
  }
  ForgeBridge.game.getAction().checkStaticAbilities();
 });}
}
