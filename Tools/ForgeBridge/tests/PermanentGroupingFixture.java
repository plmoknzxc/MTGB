package mtgb.bridge;
import forge.game.card.Card;
import forge.game.card.CounterType;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

/** Snapshot metadata adapter, not a card-effect validation scenario. */
public final class PermanentGroupingFixture {
    static Card place(String name,Player player){
        Card c=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),player);
        player.getZone(ZoneType.Battlefield).add(c);c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);
        if(c.isPlaneswalker())c.setCounters(CounterType.getType("LOYALTY"),4);
        if(c.isBattle()){c.setCounters(CounterType.getType("DEFENSE"),5);c.setProtectingPlayer(ForgeBridge.game.getPlayers().get(1));}
        return c;
    }
    public static void main(String[] args)throws Exception{
        ForgeBridge.run(args,()->{
            Player one=ForgeBridge.game.getPlayers().get(0);
            for(String name:new String[]{"Grizzly Bears","Command Tower","Sol Ring","Ghostly Prison","Jace Beleren","Invasion of Zendikar"}){place(name,one);place(name,one);}
            Card unsafe=place("Sol Ring",one);unsafe.addRemembered(one);
            Card changed=place("Jace Beleren",one);changed.setCounters(CounterType.getType("LOYALTY"),3);
            Card protectedByOwner=place("Invasion of Zendikar",one);protectedByOwner.setProtectingPlayer(one);
            ForgeBridge.game.getAction().checkStaticAbilities();
            try{java.nio.file.Files.writeString(java.nio.file.Path.of(ForgeBridge.config.get("groupingOutput").getAsString()),new com.google.gson.Gson().toJson(one.getZone(ZoneType.Battlefield).getCards().stream().map(ForgeBridge::card).toList()));}
            catch(java.io.IOException e){throw new RuntimeException(e);}
        });
    }
}
