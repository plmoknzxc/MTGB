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
    static void save(String name,Map<String,Object> value) throws Exception {
        var folder=java.nio.file.Path.of(ForgeBridge.config.get("projectionOutput").getAsString());
        java.nio.file.Files.createDirectories(folder);
        java.nio.file.Files.writeString(folder.resolve(name+".json"),new com.google.gson.Gson().toJson(value));
    }
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
            Card named=place("Llanowar Elves",p),fixed=place("Elvish Mystic",p);
            long timestamp=ForgeBridge.game.getNextTimestamp();
            named.addChangedName("Silvercoat Lion",false,timestamp,0);
            named.addColor(forge.card.ColorSet.fromNames("Blue"),false,timestamp,null);
            named.addChangedManaCost(new forge.card.mana.ManaCost(new forge.card.mana.ManaCostParser("2 U")),false,timestamp,0);
            var granted=forge.game.ability.AbilityFactory.getAbility("AB$ Draw | Cost$ T | NumCards$ 1 | SpellDescription$ Draw a card.",named);
            named.addChangedCardTraits(List.of(granted),List.of(),List.of(),List.of(),trait->true,timestamp,0);
            named.addChangedCardKeywords(List.of("Ward:2"),List.of(),false,timestamp,null);
            fixed.addNewPT(1,5,timestamp,0);
            check("Llanowar Elves".equals(data(named).get("originalName"))&&"Silvercoat Lion".equals(data(named).get("name")),"Rename baseline lost");
            check(!data(named).get("originalColors").equals(data(named).get("currentColors")),"Color baseline lost");
            check(!data(named).get("originalManaCost").equals(data(named).get("manaCost")),"Cost baseline lost");
            check(Boolean.TRUE.equals(data(named).get("hasAbilityChanges")),"Nonkeyword changes missing");
            check(data(named).get("currentAbilityText").toString().contains("Draw a card"),"Gained draw missing");
            check(data(named).get("originalAbilityText").toString().contains("Add {G}"),"Lost original mana ability missing");
            check(Boolean.TRUE.equals(data(fixed).get("toughnessBaseChanged"))&&!Boolean.TRUE.equals(data(fixed).get("powerBaseChanged")),"Base P/T flags confused with boosts");
            try {save("changed",data(named));save("base-changed",data(fixed));}
            catch(Exception e){throw new RuntimeException(e);}
            fixed.addPTBoost(3,3,timestamp,0);
            check(fixed.getNetPower()==4&&fixed.getNetToughness()==8&&Boolean.TRUE.equals(data(fixed).get("toughnessBaseChanged")),"Mixed base rewrite and boost missing");
            try {save("base-plus-boost",data(fixed));}catch(Exception e){throw new RuntimeException(e);}
            fixed.removePTBoost(timestamp,0);
            named.removeChangedName(timestamp,0);named.removeColor(timestamp,0);named.removeChangedManaCost(timestamp,0);named.removeChangedCardTraits(timestamp,0);named.removeChangedCardKeywords(timestamp,0);fixed.removeNewPT(timestamp,0);
            check(named.getName().equals("Llanowar Elves")&&!Boolean.TRUE.equals(data(named).get("hasAbilityChanges"))&&!Boolean.TRUE.equals(data(fixed).get("toughnessBaseChanged")),"Details did not restore");
            try {save("restored",data(named));save("base-restored",data(fixed));}
            catch(Exception e){throw new RuntimeException(e);}
            Card hidden=place("Grizzly Bears",p);hidden.turnFaceDown(true);
            var hiddenData=data(hidden);
            for(String key:List.of("originalName","originalManaCost","originalAbilityText","originalKeywordDetails","currentKeywordDetails","originalColors"))
                check(!hiddenData.containsKey(key),"Face-down private baseline leaked: "+key);
            p.getZone(ZoneType.Battlefield).remove(hidden);
            // Reapply the same indefinite native modifiers for the presentation snapshot.
            named.addChangedName("Silvercoat Lion",false,timestamp,0);named.addColor(forge.card.ColorSet.fromNames("Blue"),false,timestamp,null);
            named.addChangedManaCost(new forge.card.mana.ManaCost(new forge.card.mana.ManaCostParser("2 U")),false,timestamp,0);
            named.addChangedCardTraits(List.of(granted),List.of(),List.of(),List.of(),trait->true,timestamp,0);named.addChangedCardKeywords(List.of("Ward:2"),List.of(),false,timestamp,null);fixed.addNewPT(1,5,timestamp,0);
            check(!Boolean.TRUE.equals(data(up).get("powerBaseChanged")),"Boost falsely marked base rewrite");
            System.err.println("PASS: static boosts/removal; native indefinite name/color/cost/trait/keyword and base P/T projection/restoration; face-down baselines withheld");
        });
    }
}
