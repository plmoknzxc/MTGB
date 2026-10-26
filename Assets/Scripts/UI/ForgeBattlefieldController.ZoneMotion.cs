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
        public string zone;
        public int player;
    }
    private readonly Dictionary<int,ZonePose> zonePoses=new Dictionary<int,ZonePose>();
    private readonly Dictionary<int,ZoneFlight> zoneFlights=new Dictionary<int,ZoneFlight>();
    private RectTransform zoneMotionLayer;
    private int zoneMotionViewer, zoneMotionTurn;

    private void ResetZoneMotion()
    {
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
    private Dictionary<string,RectTransform> ZoneArenaSurfaces()
    {
        var result=new Dictionary<string,RectTransform>();
        foreach(var item in arenaCards.Values) {
            if(item.root==null || !item.root.gameObject.activeInHierarchy)continue;
            result[item.root.name]=item.face;
            if(!string.IsNullOrEmpty(item.groupKey))result[item.groupKey]=item.face;
        }
        return result;
    }
    private RectTransform ZoneSurface(ZonePose pose,Dictionary<string,RectTransform> surfaces)
    {
        if(pose.zone=="Battlefield" || pose.zone=="Hand") {
            if(surfaces.TryGetValue("Card_"+pose.card.id,out var exact))return exact;
            if(pose.zone=="Battlefield") {
                string row=(pose.player==seat?"Your":"Opponent")+(pose.card.creature?"Creatures":pose.card.land?"Lands":"Support");
                if(surfaces.TryGetValue(row+":"+ArenaPresentationGroupKey(pose.card),out var grouped))return grouped;
            }
            // Authorized hand identities may be known while the opponent fan shows backs.
            return pose.zone=="Hand"?(pose.player==DisplayedHandSeat?ownHand:otherHand):null;
        }
        string pile=pose.zone=="Exile"?"放逐":pose.zone=="Graveyard"?"坟场":null;
        return pile==null?null:(RectTransform)Node((pose.player==seat?"ZonePiles_PlayerBottomLeft":"ZonePiles_OpponentTopRight")+"/Pile_"+pile);
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
            Add(p.hand,"Hand");Add(p.graveyardCards,"Graveyard");Add(p.exileCards,"Exile");Add(p.battlefield,"Battlefield");
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
        var current=CurrentZonePoses();var surfaces=ZoneArenaSurfaces();
        foreach(int id in zoneFlights.Keys.Where(id=>!current.ContainsKey(id)).ToArray())CancelZoneFlight(id);
        foreach(var pair in current) {
            int id=pair.Key;var next=pair.Value;var destination=ZoneSurface(next,surfaces);
            if(destination!=null)ForgeUiMotion.Pose(destination,surface,out next.position,out next.size,out next.angle);
            bool moved=zonePoses.TryGetValue(id,out var old) && (old.zone!=next.zone || old.player!=next.player);
            if(!moved && !zoneFlights.ContainsKey(id))continue;
            if(destination==null || id==castingCardId){CancelZoneFlight(id);continue;}
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
                var root=Panel(zoneMotionLayer,"ZoneFlight_"+id,old.position,old.size,Color.white);
                root.localRotation=Quaternion.Euler(0,0,old.angle);
                var image=root.GetComponent<Image>();image.raycastTarget=false;image.preserveAspect=true;
                flight=new ZoneFlight{root=root,zone=next.zone,player=next.player};zoneFlights.Add(id,flight);
            }
            flight.root.GetComponent<Image>().sprite=art;
            flight.zone=next.zone;flight.player=next.player;
            var captured=flight;
            ForgeUiMotion.On(flight.root).Follow(destination,.38f,()=> {
                // A replaced flight must not clear the newer authoritative move.
                if(zoneFlights.TryGetValue(id,out var active) && ReferenceEquals(active,captured))CancelZoneFlight(id);
            });
        }
        zonePoses.Clear();foreach(var pair in current)zonePoses.Add(pair.Key,pair.Value);
    }
}
