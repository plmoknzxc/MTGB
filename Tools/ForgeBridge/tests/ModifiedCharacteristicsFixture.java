package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;
import java.util.Map;
import java.util.List;

/** Engine continuous effects, baseline projection and effect removal. */
public final class ModifiedCharacteristicsFixture {
    static Card place(String name,Player p) {
        Card c=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),p);
        p.getZone(ZoneType.Battlefield).add(c);c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);return c;
    }
    static Map<String,Object> data(Card c){return (Map<String,Object>)ForgeBridge.card(c);}
    static void check(boolean pass,String message){if(!pass)throw new AssertionError(message);}
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player p=ForgeBridge.game.getPlayers().get(0);
            Card normal=place("Grizzly Bears",p),up=place("Runeclaw Bear",p),down=place("Balduvian Bears",p),printed=place("Vampire Nighthawk",p);
            Card rancor=place("Rancor",p),weakness=place("Weakness",p),flying=place("Levitation",p);
            rancor.attachToEntity(up,null);weakness.attachToEntity(down,null);
            ForgeBridge.game.getAction().checkStaticAbilities();
            check(up.getNetPower()==4 && up.getNetToughness()==2,"Rancor must modify 2/2 to 4/2");
            check(down.getNetPower()==0 && down.getNetToughness()==1,"Weakness must modify 2/2 to 0/1");
            check((int)data(up).get("basePower")==2,"Baseline must remain 2");
            check(((List<String>)data(up).get("grantedKeywords")).containsAll(List.of("TRAMPLE","FLYING")),"Granted keywords missing");
            check(!((List<String>)data(printed).get("grantedKeywords")).contains("FLYING"),"Printed flying must not be marked granted");
            rancor.unattachFromEntity(up);weakness.unattachFromEntity(down);p.getZone(ZoneType.Battlefield).remove(flying);
            ForgeBridge.game.getAction().checkStaticAbilities();
            check(up.getNetPower()==2 && down.getNetPower()==2,"Stats did not restore");
            check(((List<String>)data(up).get("grantedKeywords")).isEmpty(),"Granted keywords did not restore");
            p.getZone(ZoneType.Battlefield).add(flying);rancor.attachToEntity(up,null);weakness.attachToEntity(down,null);
            ForgeBridge.game.getAction().checkStaticAbilities();
            System.err.println("PASS: boosted, reduced, unchanged, inherent/granted keywords, effect removal/restoration");
        });
    }
}
