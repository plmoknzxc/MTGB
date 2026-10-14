package mtgb.bridge;

import forge.game.CardTraitBase;
import forge.game.card.Card;
import forge.game.card.CardState;
import forge.game.spellability.SpellAbility;
import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.util.*;

/** Read-only baseline/effective details; a duration never decides the tint. */
final class ForgeCharacteristicDetails {
    private static final Map<String,Field> FIELDS=new LinkedHashMap<>();
    private static final Field CDA_TYPES,CDA_COLORS;
    private static final Method COLOR_VALUE,COLOR_ADDITIONAL;
    static {
        try {for(String name:List.of("abilities","triggers","replacementEffects","staticAbilities")) {
            Field field=CardState.class.getDeclaredField(name);field.setAccessible(true);FIELDS.put(name,field);
        }
            CDA_TYPES=Card.class.getDeclaredField("changedCardTypesCharacterDefining");CDA_TYPES.setAccessible(true);
            CDA_COLORS=Card.class.getDeclaredField("changedCardColorsCharacterDefining");CDA_COLORS.setAccessible(true);
            Class<?> color=Class.forName("forge.game.card.Card$CardColor");
            COLOR_VALUE=color.getDeclaredMethod("color");COLOR_VALUE.setAccessible(true);
            COLOR_ADDITIONAL=color.getDeclaredMethod("additional");COLOR_ADDITIONAL.setAccessible(true);
        } catch(ReflectiveOperationException e){throw new ExceptionInInitializerError(e);}
    }
    static void add(Card card,Map<String,Object> result) {
        if(card.isFaceDown())return;
        var state=card.getCurrentState();
        result.put("originalName",state.getName());
        result.put("hasNonLegendaryCreatureNames",card.hasNonLegendaryCreatureNames());
        result.put("originalManaCost",state.getManaCost().toString());
        addIntrinsicCdaBaseline(card,result);
        result.put("currentColors",(int)card.getColor().getColor());
        result.put("hasColorBaseline",true);
        var starting=forge.game.keyword.Keyword.STARTING_INTENSITY;
        if(card.hasKeyword(starting) || card.getIntensity(false)!=0) {
            result.put("hasIntensity",true);
            result.put("intensity",card.getIntensity(true));
            result.put("originalIntensity",state.getIntrinsicKeywords().stream()
                .filter(k->k.getKeyword()==starting).mapToInt(k->k.getAmount()).sum());
        }
        var copyPrinted=card.isInZone(forge.game.zone.ZoneType.Battlefield) && (card.isCloned() || card.getCopiedPermanent()!=null || card.hasMergedCard())?copyPrintedState(card):null;
        var keywordBase=copyPrinted!=null?copyPrinted:state;
        // Copy-token prototypes already include AddKeywords. The actual copy target
        // distinguishes source characteristics from exceptions such as Kiki-Jiki's haste.
        var spawning=card.getTokenSpawningAbility();
        if(spawning==null&&card.getCopiedPermanent()!=null)spawning=card.getCopiedPermanent().getTokenSpawningAbility();
        if(copyPrinted==null&&card.isToken()&&card.getCopiedPermanent()!=null&&spawning!=null) {
            for(var target:spawning.getRootAbility().getTargets().getTargetCards()) {
                if(!target.isFaceDown()&&target.getName().equals(state.getName())) {keywordBase=target.getCurrentState();break;}
            }
        }
        var original=new ArrayList<>(keywordBase.getIntrinsicKeywords().stream().map(k->k.getOriginal()).distinct().sorted().toList());
        if(keywordBase==state&&card.isToken()&&spawning!=null&&spawning.hasParam("AddKeywords")&&card.getRules()!=null) {
            // Native token creation drops resolved targets; explicit copy exceptions
            // remain on the spawning ability. Do not recolor the same printed keyword.
            var face=card.isBackSide()?card.getRules().getOtherPart():card.getRules().getMainPart();
            var printed=new HashSet<String>();if(face!=null)for(String keyword:face.getKeywords())printed.add(keyword);
            for(String exception:spawning.getParam("AddKeywords").replaceFirst("^IfNew ","").split(" & "))
                if(!printed.contains(exception))original.remove(exception);
        }
        var current=new ArrayList<>(card.getUnhiddenKeywords().getValues().stream().map(k->k.getOriginal()).distinct().sorted().toList());
        // HIDDEN is Forge's keyword implementation category here. These exact
        // public restrictions still belong in the changed characteristics.
        // Keep other internal hidden keywords out of the presentation contract.
        for(String keyword:card.getHiddenExtrinsicKeywords())
            if(("CARDNAME can't attack.".equals(keyword) || "CARDNAME can't block.".equals(keyword))
                && !current.contains(keyword))current.add(keyword);
        result.put("originalKeywordDetails",original);result.put("currentKeywordDetails",current);
        // Preserve keyword identity for battlefield glyphs, including copy exceptions.
        var inherent=original.stream().map(k->forge.game.keyword.Keyword.getInstance(k).getKeyword().name()).collect(java.util.stream.Collectors.toSet());
        result.put("grantedKeywords",card.getKeywords().stream().map(k->k.getKeyword().name()).distinct().filter(k->!inherent.contains(k)).sorted().toList());
        if(card.isInZone(forge.game.zone.ZoneType.Battlefield) && (card.isCloned() || card.getCopiedPermanent()!=null || card.hasMergedCard())) {
            var printed=copyPrinted;
            {
                if(printed!=null) {
                    var before=new ArrayList<String>();var after=new ArrayList<String>();
                    try {for(var field:FIELDS.values())for(Object trait:(Iterable<?>)field.get(printed))addTrait(before,(CardTraitBase)trait,true,card.getName());}
                    catch(IllegalAccessException e){throw new IllegalStateException("Copied printed traits unavailable",e);}
                    for(var trait:card.getSpellAbilities())addTrait(after,trait,false,card.getName());
                    for(var trait:card.getTriggers())addTrait(after,trait,false,card.getName());
                    for(var trait:card.getReplacementEffects())addTrait(after,trait,false,card.getName());
                    for(var trait:card.getStaticAbilities())addTrait(after,trait,false,card.getName());
                    result.put("copyOriginalAbilityText",String.join("\n",new LinkedHashSet<>(before)));
                    result.put("copyCurrentAbilityText",String.join("\n",new LinkedHashSet<>(after)));
                    result.put("hasCopyAbilityChanges",!new HashSet<>(before).equals(new HashSet<>(after)));
                }
            }
        }
        if(!card.getChangedCardTraits().isEmpty() || !card.getChangedCardTraitsByText().isEmpty() || !card.getChangedTextColorWords().isEmpty() || !card.getChangedTextTypeWords().isEmpty()) {
            var before=new ArrayList<String>();var after=new ArrayList<String>();
            try {for(var field:FIELDS.values())for(Object trait:(Iterable<?>)field.get(state))addTrait(before,(CardTraitBase)trait,true,state.getName());}
            catch(IllegalAccessException e){throw new IllegalStateException("Characteristic baseline unavailable",e);}
            for(var trait:card.getSpellAbilities())addTrait(after,trait,false,card.getName());
            for(var trait:card.getTriggers())addTrait(after,trait,false,card.getName());
            for(var trait:card.getReplacementEffects())addTrait(after,trait,false,card.getName());
            for(var trait:card.getStaticAbilities())addTrait(after,trait,false,card.getName());
            result.put("originalAbilityText",String.join("\n",new LinkedHashSet<>(before)));
            result.put("currentAbilityText",String.join("\n",new LinkedHashSet<>(after)));
            result.put("hasAbilityChanges",!new HashSet<>(before).equals(new HashSet<>(after)));
        }
    }
    /** Read the native CDA layers without absorbing later type/color effects. */
    @SuppressWarnings("unchecked")
    private static void addIntrinsicCdaBaseline(Card card,Map<String,Object> result) {
        var state=card.getCurrentState();
        var intrinsicIds=new HashSet<Long>();
        for(var ability:card.getStaticAbilities())
            if(ability.isIntrinsic() && ability.isCharacteristicDefining() && ability.getHostCard()==card)
                intrinsicIds.add((long)ability.getId());
        try {
            var types=(com.google.common.collect.Table<Long,Long,forge.card.ICardChangedType>)CDA_TYPES.get(card);
            var intrinsicTypes=new ArrayList<forge.card.ICardChangedType>();
            for(var cell:types.cellSet())if(intrinsicIds.contains(cell.getColumnKey()))intrinsicTypes.add(cell.getValue());
            result.put("originalType",state.getType().getTypeWithChanges(intrinsicTypes).toString());
            int colors=state.getColor().getColor();
            var colorChanges=(com.google.common.collect.Table<Long,Long,Object>)CDA_COLORS.get(card);
            for(var cell:colorChanges.cellSet())if(intrinsicIds.contains(cell.getColumnKey())) {
                int mask=((forge.card.ColorSet)COLOR_VALUE.invoke(cell.getValue())).getColor();
                colors=(boolean)COLOR_ADDITIONAL.invoke(cell.getValue())?colors|mask:mask;
            }
            result.put("originalColors",colors);
        } catch(ReflectiveOperationException e){throw new IllegalStateException("Native CDA baseline unavailable",e);}
    }
    private static final Map<forge.item.PaperCard,Card> COPY_PRINTS=new HashMap<>();
    private static CardState copyPrintedState(Card card) {
        String image=card.getCurrentState().getImageKey();
        if(image==null)return null;
        boolean back=image.endsWith(forge.ImageKeys.BACKFACE_POSTFIX);
        String front=back?image.substring(0,image.length()-forge.ImageKeys.BACKFACE_POSTFIX.length()):image;
        var paper=forge.util.ImageUtil.getPaperCardFromImageKey(front);
        if(paper==null)return null;
        // Negative-ID UI prototypes omit rules traits. A detached explicit-ID
        // prototype builds them without allocating a game card ID or entering a zone.
        var prototype=COPY_PRINTS.computeIfAbsent(paper,p->forge.game.card.CardFactory.getCard(p,null,0,null));
        var printed=back?prototype.getState(forge.card.CardStateName.Backside):prototype.getCurrentState();
        for(var name:prototype.getStates()){var candidate=prototype.getState(name);if(candidate.getName().equals(card.getCurrentState().getName())){printed=candidate;break;}}
        return printed;
    }
    private static void addTrait(List<String> lines,CardTraitBase trait,boolean original,String name) {
        if(trait.isSecondary() || trait.getKeyword()!=null || trait instanceof SpellAbility sa && (sa.isSpell()&&sa.isBasicSpell() || sa.isCastFaceDown()))return;
        var params=original?trait.getOriginalMapParams():trait.getMapParams();
        String text=params.getOrDefault("TriggerDescription",params.getOrDefault("Description",params.getOrDefault("SpellDescription","")));
        if(trait instanceof SpellAbility sa)text=original?sa.getOriginalDescription():sa.getDescription();
        text=text.replace("CARDNAME",name).replace("NICKNAME",name).replace("\\n","\n").replace("\\r\\n","\n").trim();
        if(!text.isEmpty())lines.add(text);
    }
}
