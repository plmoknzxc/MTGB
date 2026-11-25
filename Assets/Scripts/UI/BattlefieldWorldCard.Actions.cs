using System;
using UnityEngine;

public enum BattlefieldWorldActionHint
{
    None,
    Activate,
    Attack,
    HandAvailable,
    Selected
}

public sealed partial class BattlefieldWorldCard
{
    static readonly int ActionHintProperty=Shader.PropertyToID("_ActionHint");
    static readonly int ActionBoundsProperty=Shader.PropertyToID("_ActionBounds");
    static readonly int ActionCornersProperty=Shader.PropertyToID("_ActionCorners");
    MeshRenderer actionHintSurface;
    public BattlefieldWorldActionHint ActionHint { get; private set; }

    void BuildActionHint()
    {
        actionHintSurface=Surface("Action hint",new Vector3(0,0,-.035f),Vector2.one,Color.white);
        actionHintSurface.enabled=false;
        actionHintSurface.GetPropertyBlock(properties);
        properties.SetFloat(ActionHintProperty,0);
        actionHintSurface.SetPropertyBlock(properties);
    }

    void BindActionHint(ForgeCardView value,bool inHand,BattlefieldWorldActionHint hint)
    {
        // Reused card meshes must drop the previous request's action first.
        ClearActionHint();
        if(value==null)return;
        if(inHand){
            if(value.faceDown||hint is not (BattlefieldWorldActionHint.HandAvailable or BattlefieldWorldActionHint.Selected))return;
        }else if(!string.Equals(value.zone,"Battlefield",StringComparison.OrdinalIgnoreCase)
            || hint is not (BattlefieldWorldActionHint.Activate or BattlefieldWorldActionHint.Attack or BattlefieldWorldActionHint.Selected))return;
        // Legal actions are supplied by the request owner, including actions
        // of face-down permanents. The hint carries no hidden card details.
        ActionHint=hint;
        actionHintSurface.GetPropertyBlock(properties);
        // Follow the rounded printed-stock silhouette and any selection rim.
        var outline=(Vector2)rim.transform.localScale;
        if(compactFace)outline=Vector2.Max(outline,(Vector2)compactBody.transform.localScale);
        var surfaceSize=outline+Vector2.one*.15f;
        actionHintSurface.transform.localPosition=new Vector3(0,rim.transform.localPosition.y,-.035f);
        actionHintSurface.transform.localScale=new Vector3(surfaceSize.x,surfaceSize.y,1);
        properties.SetVector(ActionBoundsProperty,new Vector4(surfaceSize.x,surfaceSize.y,outline.x,outline.y));
        properties.SetVector(ActionCornersProperty,new Vector4(.025f,0,0,0));
        properties.SetFloat(ActionHintProperty,(float)ActionHint);
        actionHintSurface.SetPropertyBlock(properties);
    }

    public void ClearActionHint()
    {
        ActionHint=BattlefieldWorldActionHint.None;
        if(actionHintSurface==null)return;
        actionHintSurface.enabled=false;
        actionHintSurface.GetPropertyBlock(properties);
        properties.SetFloat(ActionHintProperty,0);
        actionHintSurface.SetPropertyBlock(properties);
    }

    void SyncActionHintVisibility(bool visible)
    {
        actionHintSurface.enabled=visible&&ActionHint!=BattlefieldWorldActionHint.None;
    }
}
