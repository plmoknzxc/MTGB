using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MainMenuController
{
    private static void BindTileArt(Image face,CardDefinition card,bool fullSize=false)
    {
        Sprite art;
        bool hasArt=fullSize ? CardImageLibrary.TryLoadFullCard(card,out art) : CardImageLibrary.TryLoadThumbnail(card,out art);
        face.sprite=hasArt ? art : CardVisualStyle.LoadFrame(card);
        face.color=face.sprite==null ? GetFallbackColor(card) : Color.white;
        var label=face.transform.Find("FallbackText")?.GetComponent<Text>();
        if (!hasArt)
        {
            if(label==null)
            {
                label=AddText(face.transform,"FallbackText","",14,Color.white,TextAnchor.MiddleCenter);
                Stretch(label.rectTransform,8,8,8,8);
            }
            label.text=GamePreferences.CardName(card)+"\n"+card.manaCost;
        }
        if(label!=null) label.gameObject.SetActive(!hasArt);
    }

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
        if(face!=null) BindTileArt(face,card);
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
