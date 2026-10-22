"""Register passive library observation at the exact native shuffle/draw boundary."""
import importlib.util,json,pathlib
ROOT=pathlib.Path(__file__).resolve().parents[2];HERE=ROOT/'Tools/ForgeBridge'
spec=importlib.util.spec_from_file_location('patches',HERE/'Apply-NativePatches.py');mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
path=HERE/'NativePatches.json';definitions=json.loads(path.read_text('utf8'))
fix=next(f for f in definitions['patches']if f['javaPath']=='forge/game/player/Player.java')
changes=[
dict(before='    public final void shuffle(final SpellAbility sa) {',after='''    // One isolated bridge process owns one game. Default null has no observer.
    public interface LibraryAudit {
        void shuffle(Player player, SpellAbility cause, boolean after);
        Object beginDraw(Player player, SpellAbility cause);
        void endDraw(Object context, CardCollectionView drawn);
    }
    private static LibraryAudit libraryAudit;
    public static void setLibraryAudit(LibraryAudit observer) { libraryAudit = observer; }
    private void auditShuffle(SpellAbility cause, boolean after) {
        if (libraryAudit != null) try { libraryAudit.shuffle(this, cause, after); }
        catch (RuntimeException error) { System.err.println("[shuffle-audit] observer failed: " + error); }
    }
    public final void shuffle(final SpellAbility sa) {
        auditShuffle(sa, false);'''),
dict(before='        getZone(ZoneType.Library).setCards(getController().cheatShuffle(list));',after='''        getZone(ZoneType.Library).setCards(getController().cheatShuffle(list));
        auditShuffle(sa, true);'''),
dict(before='''    private CardCollectionView doDraw(Map<Player, CardCollection> revealed, SpellAbility sa, Map<AbilityKey, Object> params, PlayerZone hand) {
        CardCollection drawn = new CardCollection();''',after='''    private CardCollectionView doDraw(Map<Player, CardCollection> revealed, SpellAbility sa, Map<AbilityKey, Object> params, PlayerZone hand) {
        CardCollection drawn = new CardCollection();
        final LibraryAudit observer = libraryAudit;
        Object audit = null;
        if (observer != null) try { audit = observer.beginDraw(this, sa); }
        catch (RuntimeException error) { System.err.println("[shuffle-audit] observer failed: " + error); }
        try {'''),
dict(before='''        return drawn;
    }

    public final void resetNumDrawnThisDrawStep()''',after='''        return drawn;
        } finally {
            if (observer != null) try { observer.endDraw(audit, drawn); }
            catch (RuntimeException error) { System.err.println("[shuffle-audit] observer failed: " + error); }
        }
    }

    public final void resetNumDrawnThisDrawStep()''')]
for change in changes:
 if change not in fix['changes']:fix['changes'].append(change)
if ' Passive library audit.' not in fix['reason']:
 fix['reason']+=' Passive library audit. Observe shuffle before/after and individual draw attempts without consuming random numbers or changing engine results.'
source=mod.patched_source(mod.reviewed_java_source(ROOT/fix['source'],fix),fix)
path.write_text(json.dumps(definitions,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
for folder in [ROOT/'ThirdParty/Forge/native-patches',ROOT/'.tools/native-patches/src']:
 target=folder/fix['javaPath'];target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(source.encode('utf8'))
print('PASS exact pinned Player source and passive observation patch')
