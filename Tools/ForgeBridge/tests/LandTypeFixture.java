package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;
import java.util.*;

/** Read-only audit of the shipped engine and bridge, using existing card definitions. */
public final class LandTypeFixture {
    static Card add(String name,Player p) {
        Card c=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),p);
        p.getZone(ZoneType.Battlefield).add(c,0);
        c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);return c;
    }
    static Object describe(Card c) {
        Map<String,Object> row=new LinkedHashMap<>();
        row.put("name",c.getName());row.put("type",c.getType().toString());
        row.put("mana",c.getManaAbilities().stream().map(a->a.getManaPart().getOrigProduced()).toList());
        row.put("abilities",c.getSpellAbilities().stream().map(Object::toString).toList());
        row.put("wire",ForgeBridge.card(c));return row;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player p=ForgeBridge.game.getPlayers().get(0),op=ForgeBridge.game.getPlayers().get(1);
            var lands=List.of(add("Wooded Foothills",p),add("Forest",p),add("Dryad Arbor",p),add("Urza's Saga",p),add("Wooded Foothills",op));
            List<Card> effects=new ArrayList<>();
            for(var name:ForgeBridge.config.getAsJsonArray("effects"))effects.add(add(name.getAsString(),p));
            ForgeBridge.game.getAction().checkStaticAbilities(false);
            ForgeBridge.send(Map.of("type","land-audit","lands",lands.stream().map(LandTypeFixture::describe).toList(),"views",ForgeBridge.views()));
            for(Card c:effects){p.getZone(ZoneType.Battlefield).remove(c);p.getZone(ZoneType.Graveyard).add(c,0);}
            ForgeBridge.game.getAction().checkStaticAbilities(false);
            ForgeBridge.send(Map.of("type","land-restored","lands",lands.stream().map(LandTypeFixture::describe).toList(),"views",ForgeBridge.views()));
        });
    }
}
