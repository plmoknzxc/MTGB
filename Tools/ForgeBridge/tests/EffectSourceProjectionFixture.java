package mtgb.bridge;

import forge.game.ability.SpellAbilityEffect;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Direct projection adapters only. These are not spell/effect outcome tests. */
public final class EffectSourceProjectionFixture {
    @SuppressWarnings("unchecked") static Map<String,Object> project(Card c){return (Map<String,Object>)ForgeBridge.card(c);}
    static void check(boolean ok,String message){if(!ok)throw new AssertionError(message);}
    static Card effect(Card source,Player p,String name,String image){return SpellAbilityEffect.createEffect(null,source,p,name,image,p.getGame().getNextTimestamp());}
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player p=ForgeBridge.game.getPlayers().get(0);List<String> checked=new ArrayList<>();
            for(ZoneType zone:List.of(ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Hand,ZoneType.Library)){
                Card source=StateBranchFixture.place("Embolden",p,zone),eff=effect(source,p,source+"'s Effect",source.getImageKey());Map<String,Object> card=project(eff);boolean pub=zone==ZoneType.Battlefield||zone==ZoneType.Graveyard;
                check(card.containsKey("effectSourcePrintingKey")==pub,"Visibility "+zone+" "+card);
                if(pub){check("Embolden".equals(card.get("effectSourceName"))&&Boolean.TRUE.equals(card.get("genericEffectName")),"Generic name");check(((Number)card.get("id")).intValue()==eff.getId()&&eff.getId()!=source.getId()&&!card.containsKey("printingKey"),"Source identity assigned to effect");}
                else check(!card.containsKey("effectSourceName")&&!card.containsKey("effectSourceBackFace")&&!card.containsKey("genericEffectName"),"Hidden metadata");checked.add(zone.name());
            }
            Card source=StateBranchFixture.place("Embolden",p,ZoneType.Battlefield);
            check(!project(effect(source,p,"Custom effect", "unrelated-image")).containsKey("effectSourcePrintingKey"),"Mismatched image inherited");checked.add("image-mismatch");
            check(Boolean.FALSE.equals(project(effect(source,p,"Custom effect",source.getImageKey())).get("genericEffectName")),"Custom name flattened");checked.add("custom-name");
            check(!project(source).containsKey("effectSourcePrintingKey"),"Ordinary source changed");checked.add("ordinary-card");
            source.turnFaceDown(true);check(source.isFaceDown(),"Face-down fixture");check(!project(effect(source,p,"Face-down effect",source.getImageKey())).containsKey("effectSourcePrintingKey"),"Face-down source exposed");checked.add("face-down-source");
            try{Files.writeString(Path.of(ForgeBridge.config.get("output").getAsString()),ForgeBridge.JSON.toJson(Map.of("status","PASS","checks",checked,"kind","projection-adapter")));}catch(Exception ex){throw new RuntimeException(ex);}
        });
    }
}
