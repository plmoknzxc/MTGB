// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import com.google.gson.*;
import forge.card.*;
import forge.card.mana.*;
import forge.deck.*;
import forge.game.*;
import forge.game.card.*;
import forge.game.keyword.*;
import forge.game.player.*;
import forge.item.PaperCard;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import java.util.*;

/** Build-time editor facets from the pinned engine. No translated-text heuristics. */
public final class DeckEditorCatalog {
    static final String[] COMPANIONS={"Gyruda, Doom of Depths","Jegantha, the Wellspring","Kaheera, the Orphanguard","Keruga, the Macrosage","Lurrus of the Dream-Den","Lutri, the Spellchaser","Obosh, the Preypiercer","Umori, the Collector","Yorion, Sky Nomad","Zirda, the Dawnwaker"};
    public static void main(String[] args)throws Exception {
        System.setOut(System.err);
        ForgeEnvironment.initialize(args[0]);
        var unique=new TreeMap<String,PaperCard>();
        for(var list:ForgeCatalog.printings().values())if(list.size()==1)unique.putIfAbsent(list.get(0).getName(),list.get(0));
        var registered=new RegisteredPlayer(new Deck("Editor facets"));
        registered.setPlayer(new ForgeBridge.BridgePlayer("Editor"));
        var game=new Match(new GameRules(GameType.Constructed),List.of(registered),"Editor facets").createGame();
        var player=game.getPlayers().get(0);
        Card[] sources=new Card[COMPANIONS.length];Companion[] keywords=new Companion[sources.length];
        for(int i=0;i<sources.length;i++){
            sources[i]=Card.fromPaperCard(unique.get(COMPANIONS[i]),player);
            keywords[i]=(Companion)sources[i].getKeywords(Keyword.COMPANION).iterator().next();
        }
        var eligible=new ArrayList<PaperCard>();
        for(var paper:unique.values())if(DeckFormat.Commander.isLegalCommander(paper.getRules()))eligible.add(paper);
        JsonObject result=new JsonObject();result.add("companions",new Gson().toJsonTree(COMPANIONS));
        JsonArray rows=new JsonArray();
        for(var paper:unique.values()){
            var rules=paper.getRules();var card=Card.fromPaperCard(paper,player);
            JsonObject row=new JsonObject();row.addProperty("name",paper.getName());
            row.addProperty("identity",rules.getColorIdentity().getColor());
            row.addProperty("commander",DeckFormat.Commander.isLegalCommander(rules));
            row.addProperty("brawl",DeckFormat.Brawl.isLegalCommander(rules));
            int types=0;for(var type:rules.getType().getCoreTypes())types|=1<<type.ordinal();
            row.addProperty("types",types);row.addProperty("land",rules.getType().isLand());
            int mask=0;
            for(int i=0;i<keywords.length;i++){
                var kw=keywords[i];boolean allowed;
                if(!kw.hasSpecialRestriction())allowed=card.isValid(kw.getDeckRestriction().split(","),player,sources[i],null);
                else allowed=!kw.getSpecialRules().equals("UniqueManaSymbols")||uniqueManaSymbols(card);
                if(allowed)mask|=1<<i;
            }
            row.addProperty("companions",mask);
            if(DeckFormat.Commander.isLegalCommander(rules)){
                JsonArray partners=new JsonArray();
                for(var other:eligible)if(!paper.getName().equals(other.getName())&&rules.canBePartnerCommanders(other.getRules()))partners.add(other.getName());
                if(!partners.isEmpty())row.add("partners",partners);
                List<DeckRuleColorIdentity> exceptions=new ArrayList<>();
                for(var rule:DeckRule.parseAll(paper))if(rule instanceof DeckRuleColorIdentity ci&&rule.isActiveFor(DeckSection.Commander))exceptions.add(ci);
                if(!exceptions.isEmpty()){
                    JsonArray exemptions=new JsonArray();
                    for(var other:unique.values())if(DeckFormat.allowsOffColorIdentity(exceptions,other.getRules())||DeckFormat.approvesAdditionalColor(exceptions,other.getRules(),rules.getColorIdentity().getColor()))exemptions.add(other.getName());
                    row.add("exemptions",exemptions);
                }
            }
            rows.add(row);
        }
        result.add("cards",rows);
        Path output=Path.of(args[1]);Files.createDirectories(output.toAbsolutePath().getParent());
        Files.writeString(output,new Gson().toJson(result),StandardCharsets.UTF_8);
        System.err.println("Editor facets: "+rows.size());ForgeEnvironment.UI.shutdownNow();System.exit(0);
    }
    // Same per-card test as Player.allCardsUniqueManaSymbols, including both split halves.
    static boolean uniqueManaSymbols(Card card){
        Set<ManaCostShard> colored=new HashSet<>();Set<Integer> generic=new HashSet<>();
        for(var state:card.isSplitCard()?EnumSet.of(CardStateName.LeftSplit,CardStateName.RightSplit):EnumSet.of(CardStateName.Original)){
            var cost=card.getState(state).getManaCost();
            for(var shard:cost)if(!colored.add(shard))return false;
            int number=cost.getGenericCost();if((number>0||cost.getCMC()==0)&&!generic.add(number))return false;
        }
        return true;
    }
}
