package forge.game.event;

public record GameEventRandomLog(String message, forge.game.spellability.SpellAbility sourceAbility, forge.game.card.CardCollectionView removedFromMatch, boolean removeFromInventory) implements GameEvent {

    public GameEventRandomLog(String message) { this(message,null,null,false); }

    @Override
    public <T> T visit(IGameEventVisitor<T> visitor) {
        return visitor.visit(this);
    }
}
