package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

/** Only arrange opening zones. All casting/payment/results use production Forge. */
public final class CastChoiceFamilyFixture {
    static void add(String name,Player p,ZoneType zone) {
        Card c=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),p);
        p.getZone(zone).add(c);c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player p=ForgeBridge.game.getPlayers().get(0),q=ForgeBridge.game.getPlayers().get(1);
            String family=ForgeBridge.config.get("session").getAsString().split("-")[0];
            String name="",land="Mountain";int mana=0;
            switch(family) {
            case "multikicker":name="Everflowing Chalice";mana=4;break;
            case "buyback":name="Whispers of the Muse";land="Island";mana=6;break;
            case "bargain":name="Torch the Tower";mana=1;add("Tormod's Crypt",p,ZoneType.Battlefield);add("Hill Giant",q,ZoneType.Battlefield);break;
            case "gift":name="Parting Gust";land="Plains";mana=2;add("Hill Giant",q,ZoneType.Battlefield);break;
            case "overload":name="Mizzium Mortars";mana=6;add("Hill Giant",q,ZoneType.Battlefield);add("Grizzly Bears",q,ZoneType.Battlefield);break;
            case "dash":name="Mardu Scout";mana=2;break;
            case "escalate":name="Collective Defiance";mana=4;add("Hill Giant",q,ZoneType.Battlefield);break;
            case "conspire":name="Gleeful Sabotage";land="Forest";mana=2;add("Grizzly Bears",p,ZoneType.Battlefield);add("Llanowar Elves",p,ZoneType.Battlefield);add("Tormod's Crypt",q,ZoneType.Battlefield);add("Sol Ring",q,ZoneType.Battlefield);break;
            case "casualty":name="Rob the Archives";mana=2;add("Grizzly Bears",p,ZoneType.Battlefield);break;
            case "squad":name="Sicarian Infiltrator";land="Island";mana=7;break;
            case "confluence":name="Mystic Confluence";land="Island";mana=5;break;
            case "xconfluence":name="Doomsday Confluence";land="Swamp";mana=7;break;
            case "profit":name="Cut of the Profits";land="Swamp";mana=4;add("Hill Giant",p,ZoneType.Battlefield);break;
            default:throw new IllegalArgumentException(family);
            }
            add(name,p,ZoneType.Hand);for(int i=0;i<mana;i++)add(land,p,ZoneType.Battlefield);
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
