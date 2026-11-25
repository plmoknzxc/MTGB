using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private sealed class ZonePose
    {
        public ForgeCardView card;
        public int player;
        public string zone;
        public Vector2 position, size;
        public float angle;
    }
    private sealed class ZoneFlight
    {
        public RectTransform root;
        public BattlefieldWorldCard world;
        public string zone;
        public int player;
    }
    private readonly Dictionary<int,ZonePose> zonePoses=new Dictionary<int,ZonePose>();
    private readonly Dictionary<int,ZoneFlight> zoneFlights=new Dictionary<int,ZoneFlight>();
    private RectTransform zoneMotionLayer;
    private int zoneMotionViewer, zoneMotionTurn;
    private bool hasPreviousZoneView;
    private long lastDeathNotice;
    private readonly HashSet<int> dyingCreatures = new();

    private void ResetZoneMotion()
    {
        ResetCombatMotion();
        foreach(var item in arenaCards.Values)
            if(item.world!=null)item.world.CancelStackLanding();
        foreach(var id in handStackFlights.Keys.ToArray())FinishHandStackFlight(id);
        hasPreviousZoneView=false;animatedPendingHand=0;
        lastDeathNotice=0;dyingCreatures.Clear();
        foreach(var flight in zoneFlights.Values) {
            if(flight.root==null)continue;
            ForgeUiMotion.On(flight.root).Cancel();flight.root.gameObject.SetActive(false);Destroy(flight.root.gameObject);
        }
        zoneFlights.Clear();zonePoses.Clear();zoneMotionViewer=0;zoneMotionTurn=0;
    }
    private void BeginZoneMotion()
    {
        if(view==null || zoneMotionViewer!=seat || view.turn<zoneMotionTurn)ResetZoneMotion();
        zoneMotionViewer=seat;zoneMotionTurn=view?.turn??0;
        // Capture the real old rendered positions before layout deletes/reparents cards.
        var surfaces=ZoneArenaSurfaces();
        foreach(var old in zonePoses.Values) {
            var source=ZoneSurface(old,surfaces);
            if(source!=null)ForgeUiMotion.Pose(source,surface,out old.position,out old.size,out old.angle);
        }
    }
    private Dictionary<string,RectTransform> ZoneArenaSurfaces(bool currentOnly=false)
    {
        var result=new Dictionary<string,RectTransform>();
        foreach(var pair in arenaCards) {
            if(currentOnly&&!retainedCards.Contains(pair.Key))continue;
            var item=pair.Value;
            if(item.root==null || !item.root.gameObject.activeInHierarchy)continue;
            result[item.root.name]=item.face;
            if(!string.IsNullOrEmpty(item.groupKey))result[item.groupKey]=item.face;
        }
        return result;
    }
    private RectTransform ZoneSurface(ZonePose pose,Dictionary<string,RectTransform> surfaces)
    {
        if(pose.zone=="Stack"){
            if(handStackFlights.TryGetValue(pose.card.id,out var arriving)&&arriving.ghost!=null)
                return arriving.ghost;
            var entry=stackCards.Values.FirstOrDefault(s=>s.source?.id==pose.card.id&&s.entry?.kind=="spell"&&!s.entry.copied);
            if(entry==null)return null;
            if(!stackCollapsed&&entry.tile.gameObject.activeInHierarchy)return entry.tile;
            return stackCollapseButton!=null&&stackCollapseButton.gameObject.activeInHierarchy
                ?(RectTransform)stackCollapseButton.transform:null;
        }
        if(pose.zone=="Battlefield" || pose.zone=="Hand") {
            if(surfaces.TryGetValue("Card_"+pose.card.id,out var exact))return exact;
            if(pose.zone=="Battlefield") {
                string row=(pose.player==seat?"Your":"Opponent")+(pose.card.creature?"Creatures":pose.card.land?"Lands":"Support");
                if(surfaces.TryGetValue(row+":"+ArenaPresentationGroupKey(pose.card),out var grouped))return grouped;
            }
            // Authorized hand identities may be known while the opponent fan shows backs.
            return pose.zone=="Hand"?(pose.player==DisplayedHandSeat?ownHand:otherHand):null;
        }
        string pile=pose.zone switch {"Exile"=>"放逐","Graveyard"=>"坟场","Command"=>"统帅区",_=>null};
        var target=pile==null?null:authored.Find((pose.player==seat?"ZonePiles_PlayerBottomLeft":"ZonePiles_OpponentTopRight")+"/Pile_"+pile) as RectTransform;
        return target!=null&&target.gameObject.activeInHierarchy?target:null;
    }
    private Dictionary<int,ZonePose> CurrentZonePoses()
    {
        var current=new Dictionary<int,ZonePose>();
        foreach(var p in view?.players??Array.Empty<ForgePlayerView>()) {
            void Add(ForgeCardView[] cards,string zone) {
                foreach(var c in cards??Array.Empty<ForgeCardView>()) {
                    // Only identities actually supplied to this viewer; never use counts to infer IDs.
                    if(c.id<=0)continue;
                    current[c.id]=new ZonePose { card=c,player=p.seat,zone=zone };
                }
            }
            Add(p.hand,"Hand");Add(p.graveyardCards,"Graveyard");Add(p.exileCards,"Exile");Add(p.commandCards??p.commanders,"Command");Add(p.battlefield,"Battlefield");
        }
        if(IsRedesignLayout){
            // Abilities and copies can point back to a permanent that never moved.
            foreach(var entry in view?.stackItems??Array.Empty<ForgeStackItem>()){
                var source=entry.source;
                if(entry.kind!="spell"||entry.copied||source==null||source.id<=0||current.ContainsKey(source.id))continue;
                current[source.id]=new ZonePose{card=source,player=entry.controllerSeat,zone="Stack"};
            }
        }
        return current;
    }
    private void CancelZoneFlight(int id)
    {
        if(!zoneFlights.TryGetValue(id,out var flight))return;
        if(flight.root!=null){ForgeUiMotion.On(flight.root).Cancel();flight.root.gameObject.SetActive(false);Destroy(flight.root.gameObject);}
        zoneFlights.Remove(id);
    }
    private void EndZoneMotion()
    {
        var current=CurrentZonePoses();var surfaces=ZoneArenaSurfaces(true);
        dyingCreatures.Clear();
        foreach(var notice in view?.notices??Array.Empty<ForgeNotice>())
        {
            if(notice.id<=lastDeathNotice)continue;
            lastDeathNotice=notice.id;
            if(!hasPreviousZoneView || notice.kind!="card-move" || notice.fromZone!="Battlefield" || notice.toZone!="Graveyard")continue;
            foreach(var card in notice.cards??Array.Empty<ForgeCardView>())
                if(zonePoses.TryGetValue(card.id,out var departed) && departed.zone=="Battlefield" && departed.card.creature)
                    dyingCreatures.Add(card.id);
        }
        foreach(int id in zoneFlights.Keys.Where(id=>!current.ContainsKey(id)).ToArray())CancelZoneFlight(id);
        foreach(var pair in current) {
            int id=pair.Key;var next=pair.Value;var destination=ZoneSurface(next,surfaces);
            if(destination!=null)ForgeUiMotion.Pose(destination,surface,out next.position,out next.size,out next.angle);
            bool moved=zonePoses.TryGetValue(id,out var old) && (old.zone!=next.zone || old.player!=next.player);
            if(!moved)continue; // Follow tracks a moving destination itself.
            if(IsRedesignLayout && old.zone=="Battlefield" && old.card.creature && (next.zone=="Graveyard" || dyingCreatures.Contains(id)))
            {
                dyingCreatures.Add(id);CancelZoneFlight(id);continue;
            }
            if(destination==null || next.zone=="Stack" || (id==castingCardId&&old.zone!="Stack")){CancelZoneFlight(id);continue;}
            if(old.zone=="Stack"&&old.size!=Vector2.zero&&next.zone=="Battlefield"&&worldTable!=null){
                var landed=arenaCards.Values.FirstOrDefault(c=>c.card?.id==id&&c.face==destination&&c.world!=null);
                if(landed!=null&&landed.representedCount==1){
                    CancelZoneFlight(id);
                    landed.world.StartStackLanding(surface.TransformPoint(old.position),surface.TransformVector(Vector3.right*old.size.x).magnitude);
                    continue;
                }
            }
            Sprite art=null;
            // Use the newly authorized face, including a back for face-down objects.
            TryBattlefieldArt(next.card,out art);
            if(art==null){CancelZoneFlight(id);continue;}
            if(!zoneFlights.TryGetValue(id,out var flight)) {
                if(old==null || old.size==Vector2.zero)continue;
                if(zoneMotionLayer==null) {
                    zoneMotionLayer=Rect(surface,"ZoneMotion",Vector2.zero,surface.rect.size);
                    var layer=zoneMotionLayer.gameObject.AddComponent<CanvasGroup>();layer.interactable=false;layer.blocksRaycasts=false;
                }
                // Flights belong to the battlefield layer, below plates, hover and decision windows.
                zoneMotionLayer.SetSiblingIndex(cardsRoot.GetSiblingIndex()+1);
                bool worldFlight=worldTable!=null&&old.zone!="Stack";
                var root=worldFlight?Rect(zoneMotionLayer,"ZoneFlight_"+id,old.position,old.size):Panel(zoneMotionLayer,"ZoneFlight_"+id,old.position,old.size,Color.white);
                root.localRotation=Quaternion.Euler(0,0,old.angle);
                var image=root.GetComponent<Image>();if(image!=null){image.raycastTarget=false;image.preserveAspect=true;}
                flight=new ZoneFlight{root=root,zone=next.zone,player=next.player};zoneFlights.Add(id,flight);
                if(worldFlight)flight.world=worldTable.Create(root,root,root.gameObject.AddComponent<ForgeBattlefieldCardPointer>());
            }
            if(flight.world!=null){
                flight.world.Bind(next.card,art,false,1,0,new Color(.3f,.34f,.36f),null,null,"");
                flight.world.SyncPose();
            }
            else flight.root.GetComponent<Image>().sprite=art;
            flight.zone=next.zone;flight.player=next.player;
            var captured=flight;
            ForgeUiMotion.On(flight.root).Follow(destination,.38f,()=> {
                // A replaced flight must not clear the newer authoritative move.
                if(zoneFlights.TryGetValue(id,out var active) && ReferenceEquals(active,captured))CancelZoneFlight(id);
            });
        }
        PresentCombatFeedback(current, surfaces);
        zonePoses.Clear();foreach(var pair in current)zonePoses.Add(pair.Key,pair.Value);
        hasPreviousZoneView=view?.players?.Length>=2;
    }
}
