package mtgb.bridge;
import forge.game.card.*;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

/** Setup only; actions and resolution use the real Forge game loop. */
public final class AdvancedInteractionFixture {
    static Card place(String name,Player player,ZoneType zone) {
        Card card=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),player);
        player.getZone(zone).add(card);card.setGameTimestamp(ForgeBridge.game.getNextTimestamp());card.setSickness(false);return card;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player one=ForgeBridge.game.getPlayers().get(0),two=ForgeBridge.game.getPlayers().get(1);
            String test=ForgeBridge.config.get("fixture").getAsString();
            for(String land:new String[]{"Island","Mountain","Forest"})for(int i=0;i<5;i++)place(land,one,ZoneType.Battlefield);
            if(test.startsWith("cast-")) {
                for(int i=0;i<5;i++){place("Plains",one,ZoneType.Battlefield);place("Swamp",one,ZoneType.Battlefield);}
                if(test.startsWith("cast-empower")) {
                    Card way=place("Way of the Pyromancer",one,ZoneType.Hand);
                    place("Way of the Pyromancer",one,ZoneType.Hand);
                    if(test.equals("cast-empower-multiple")) for(int i=0;i<2;i++) {
                        Card token=forge.game.card.token.TokenInfo.getProtoType("u_empower",way.getFirstSpellAbility(),one,false);
                        token.addType("Jace");token.setName("Jace Token");token.setTokenSpawningAbility(way.getFirstSpellAbility());
                        one.getZone(ZoneType.Battlefield).add(token);
                        token.setGameTimestamp(ForgeBridge.game.getNextTimestamp());
                        token.setCounters(CounterType.getType("LOYALTY"),i+2);
                    }
                }
                else if(test.equals("cast-adventure"))place("Bonecrusher Giant",one,ZoneType.Hand);
                else if(test.equals("cast-graveyard"))place("Think Twice",one,ZoneType.Graveyard);
                else if(test.equals("cast-exile"))place("Reckless Impulse",one,ZoneType.Hand);
                else if(test.equals("cast-prepare"))place("Emeritus of Woe",one,ZoneType.Hand);
                else if(test.equals("cast-omen")){place("Scavenger Regent",one,ZoneType.Hand);place("Grizzly Bears",two,ZoneType.Battlefield);}
                else if(test.startsWith("cast-mdfc")){place("Bala Ged Recovery",one,ZoneType.Hand);place("Grizzly Bears",one,ZoneType.Graveyard);}
                else if(test.equals("cast-spree")){place("Requisition Raid",one,ZoneType.Hand);place("Grizzly Bears",one,ZoneType.Battlefield);place("Tormod's Crypt",two,ZoneType.Battlefield);place("Glorious Anthem",two,ZoneType.Battlefield);}
            } else if(test.equals("fra")) {
                java.util.Set<String> checked=new java.util.HashSet<>();
                for(var printing:FModel.getMagicDb().getCommonCards().getAllCards())
                    if(java.util.Set.of("FRA","FRC").contains(printing.getEdition()) && checked.add(printing.getName()))
                        Card.fromPaperCard(printing,one);
                ForgeBridge.send(java.util.Map.of("type","series-audit","uniqueCards",checked.size()));
                place("No Admittance",one,ZoneType.Hand);
            } else if(test.equals("crew")) {
                place("Kylox's Voltstrider",one,ZoneType.Battlefield);place("Grizzly Bears",one,ZoneType.Battlefield);
            } else if(test.equals("statuses")) {
                Card creature=place("Vampire Nighthawk",one,ZoneType.Battlefield);
                creature.setCounters(CounterType.getType("P1P1"),2);creature.setCounters(CounterType.getType("STUN"),1);
                creature.setTapped(true);
            } else if(test.equals("graveyards")) {
                place("Agatha's Soul Cauldron",one,ZoneType.Battlefield);
                place("Grizzly Bears",one,ZoneType.Battlefield);
                place("Grizzly Bears",one,ZoneType.Graveyard);
                place("Grizzly Bears",two,ZoneType.Graveyard);
            } else if(test.equals("rooms")) {
                for(int i=0;i<6;i++)place("Plains",one,ZoneType.Battlefield);
                Card room=place("Dollmaker's Shop // Porcelain Gallery",one,ZoneType.Battlefield);
                room.setUnlockedRooms(java.util.EnumSet.of(forge.card.CardStateName.LeftSplit));room.updateRooms();
            } else if(test.equals("x")) {
                place("Walking Ballista",one,ZoneType.Hand);
            } else if(test.equals("evidence")) {
                place("Kylox's Voltstrider",one,ZoneType.Battlefield);
                place("Grizzly Bears",one,ZoneType.Graveyard);
                place("Hill Giant",one,ZoneType.Graveyard);
                place("Forest",one,ZoneType.Graveyard);
                place("Colossal Dreadmaw",one,ZoneType.Graveyard);
            } else if(test.equals("abilities")) {
                Card grist=place("Grist, the Hunger Tide",one,ZoneType.Battlefield);
                grist.setCounters(CounterType.getType("LOYALTY"),5);
            } else if(test.equals("damage")) {
                place("Colossal Dreadmaw",one,ZoneType.Battlefield);
                place("Grizzly Bears",two,ZoneType.Battlefield);place("Grizzly Bears",two,ZoneType.Battlefield);
            } else if(test.equals("storm")) {
                place("Ornithopter",one,ZoneType.Hand);place("Ornithopter",one,ZoneType.Hand);place("Grapeshot",one,ZoneType.Hand);
            } else if(test.equals("ninjutsu")) {
                place("Ornithopter",one,ZoneType.Battlefield);place("Moon-Circuit Hacker",one,ZoneType.Hand);
            } else if(test.equals("battle")) {
                place("Goblin Piker",one,ZoneType.Battlefield);
                Card battle=place("Invasion of Zendikar",one,ZoneType.Battlefield);
                battle.setProtectingPlayer(two);battle.setCounters(CounterType.getType("DEFENSE"),1);
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
