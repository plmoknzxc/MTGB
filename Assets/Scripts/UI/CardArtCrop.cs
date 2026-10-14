using UnityEngine;
using UnityEngine.UI;

// Fill a fixed UI frame without stretching its source. Crop only within the
// requested card region so printed rules cannot leak into an illustration.
public static class CardArtCrop
{
    public static void Illustration(RawImage image,Sprite sprite)
        => Fill(image,sprite,new Rect(.07f,.43f,.86f,.45f),1f);

    public static void Header(RawImage image,Sprite sprite)
        => Fill(image,sprite,new Rect(0,.45f,1,.55f));

    public static void Fill(RawImage image,Sprite sprite,Rect region,float verticalAnchor=.5f)
    {
        image.texture=sprite!=null?sprite.texture:null;
        image.uvRect=new Rect(0,0,1,1);
        if(sprite==null)return;
        var frame=image.rectTransform;
        // Include inherited nonuniform scale (e.g. an ability face in an order
        // tile). A rotation alone must not change the crop's aspect ratio.
        float width=frame.TransformVector(Vector3.right*frame.rect.width).magnitude;
        float height=frame.TransformVector(Vector3.up*frame.rect.height).magnitude;
        var r=sprite.rect;
        var crop=new Rect(r.x+r.width*region.x,r.y+r.height*region.y,r.width*region.width,r.height*region.height);
        if(width>0&&height>0&&crop.width>0&&crop.height>0){
            float aspect=width/height;
            if(crop.width/crop.height>aspect){float fitted=crop.height*aspect;crop.x+=(crop.width-fitted)*.5f;crop.width=fitted;}
            // UV starts at the bottom: anchor 1 preserves the illustration's
            // top edge; headers keep their centered crop.
            else {float fitted=crop.width/aspect;crop.y+=(crop.height-fitted)*Mathf.Clamp01(verticalAnchor);crop.height=fitted;}
        }
        image.uvRect=new Rect(crop.x/sprite.texture.width,crop.y/sprite.texture.height,crop.width/sprite.texture.width,crop.height/sprite.texture.height);
    }
}
