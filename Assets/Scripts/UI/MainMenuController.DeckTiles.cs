using System.Collections.Generic;
using UnityEngine;

public sealed partial class MainMenuController
{
    readonly Dictionary<(MainMenuCardTileZone,string),Queue<MainMenuCardTile>> reusableDeckTiles=new();
    void BeginDeckTileReuse()
    {
        reusableDeckTiles.Clear();
        foreach(var parent in new[]{mainDeckContent,sideboardContent}){
            if(parent==null)continue;
            foreach(var tile in parent.GetComponentsInChildren<MainMenuCardTile>()){
                var key=(tile.Zone,tile.Card.PrintingKey);
                if(!reusableDeckTiles.TryGetValue(key,out var queue))reusableDeckTiles[key]=queue=new();
                queue.Enqueue(tile);
            }
        }
        if(commandZoneDrop!=null){commandZoneDrop.gameObject.SetActive(false);commandZoneDrop.SetParent(null,false);Destroy(commandZoneDrop.gameObject);commandZoneDrop=null;}
    }
    bool TryReuseDeckTile(CardDefinition card,int count,MainMenuCardTileZone zone)
    {
        if(zone==MainMenuCardTileZone.Collection||!reusableDeckTiles.TryGetValue((zone,card.PrintingKey),out var queue)||queue.Count==0)return false;
        var tile=queue.Dequeue();tile.transform.SetAsLastSibling();
        tile.SetSingleCopyDisplay(singleDeckCards&&zone is MainMenuCardTileZone.MainDeck or MainMenuCardTileZone.Sideboard);tile.SetCount(count);
        var face=tile.transform.Find("CardFace")?.GetComponent<UnityEngine.UI.Image>();
        bool rebind=!ReferenceEquals(tile.Card,card);
        if(face!=null){
            // A reload can invalidate sprites while their tile survives. Reuse
            // only current art; otherwise replace it before the next paint.
            bool full=CardImageLibrary.TryLoadThumbnail(card,out var art);
            face.sprite=full?art:CardVisualStyle.LoadFrame(card);
            face.color=face.sprite!=null?Color.white:GetFallbackColor(card);
            foreach(string name in new[]{"FallbackText","FallbackOverlay"}){var label=tile.transform.Find(name);if(label!=null)label.gameObject.SetActive(!full);}
        }
        if(rebind)tile.Configure(this,card,zone);
        bool selected=ReferenceEquals(quantityCard,card)&&quantityZone==zone&&(!singleDeckCards||ReferenceEquals(quantityTile,tile));
        tile.SetQuantityControls(selected,CountTileCopies(card,zone),zone!=MainMenuCardTileZone.Commander&&DeckRepository.GetAllowedCopiesToAdd(currentDeck,card)>0);
        if(selected){quantityTile=tile;SetHighlightedTile(tile);}
        AddDeckRole(tile);ApplyLegality(tile);return true;
    }
    void FinishDeckTileReuse()
    {
        foreach(var queue in reusableDeckTiles.Values)while(queue.Count>0){var tile=queue.Dequeue();if(tile==quantityTile)quantityTile=null;tile.gameObject.SetActive(false);tile.transform.SetParent(null,false);Destroy(tile.gameObject);}
        reusableDeckTiles.Clear();
    }
}
