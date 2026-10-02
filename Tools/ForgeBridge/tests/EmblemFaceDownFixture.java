package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

public final class EmblemFaceDownFixture {
    static Card place(String name, Player player, ZoneType zone) {
        Card c=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),player);
        player.getZone(zone).add(c);c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);return c;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()-> {
            Player p=ForgeBridge.game.getPlayers().get(0);
            for(String land:new String[]{"Forest","Island","Swamp"})for(int i=0;i<10;i++)place(land,p,ZoneType.Battlefield);
            place("Willbender",p,ZoneType.Hand);
            place("Greenbelt Radical",p,ZoneType.Hand);
            place("Manifest Dread",p,ZoneType.Hand);
            Card g=place("Garruk, Cursed Huntsman",p,ZoneType.Battlefield);
            g.setCounters(forge.game.card.CounterType.getType("LOYALTY"),7);
            Card source=place("Manifest Dread",p,ZoneType.Graveyard);
            Card bear=place("Grizzly Bears",p,ZoneType.Library);
            bear.manifest(p,source.getFirstSpellAbility(),forge.game.ability.AbilityKey.newMap());
            Card cloak=place("Runeclaw Bear",p,ZoneType.Hand);
            cloak.cloak(p,source.getFirstSpellAbility(),forge.game.ability.AbilityKey.newMap());
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
