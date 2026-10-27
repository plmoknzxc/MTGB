package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;
/** Arrange only: attack-trigger exile, permission, announcement and payment remain native. */
public final class ArtifactPermissionFixture {
 static Card add(String name,Player p,ZoneType zone){Card c=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),p);p.getZone(zone).add(c);c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);return c;}
 public static void main(String[] args)throws Exception{ForgeBridge.run(args,()->{
  Player p=ForgeBridge.game.getPlayers().get(0);
  for(int i=0;i<3;i++)add("Mountain",p,ZoneType.Battlefield);
  for(int i=0;i<5;i++)add("Darksteel Relic",p,ZoneType.Battlefield);
  add("Chiss-Goria, Forge Tyrant",p,ZoneType.Battlefield);
  String variable=ForgeBridge.config.has("artifactCase")&&ForgeBridge.config.get("artifactCase").getAsString().equals("serpent")?"Stonecoil Serpent":"Walking Ballista";
  for(String name:new String[]{"Portal to Phyrexia","Wurmcoil Engine",variable,"Cityscape Leveler","Sol Ring"}){
   Card c=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),p);p.getZone(ZoneType.Library).add(c,0);c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());
  }
  ForgeBridge.game.getAction().checkStaticAbilities();
 });}
}
