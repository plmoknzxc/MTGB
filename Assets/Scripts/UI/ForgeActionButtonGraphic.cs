using UnityEngine;
using UnityEngine.UI;

// Chamfered action face shared by the phase, payment and selection controls.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ForgeActionButtonGraphic : MaskableGraphic
{
    public bool Secondary;
    MenuThemeStyle theme;
    bool advance;
    internal void ApplyTheme(MenuThemeStyle value,bool phaseAdvance=false)
    {
        if(theme==value && advance==phaseAdvance)return;
        theme=value;advance=phaseAdvance;
        SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();var r=rectTransform.rect;float cut=8;
        if(r.width<3 || r.height<3)return;
        Vector2[] corners;
        if(theme!=null && advance){
            float divider=r.xMax-38;
            Color fill=Color.Lerp(theme.SectionFill,theme.ActionFill,.80f);
            Color end=Color.Lerp(theme.ActionFill,theme.ActionEdge,.16f);
            void Band(float left,float right,float bottom,float top,Color l,Color h){
                int i=mesh.currentVertCount;
                mesh.AddVert(new Vector2(left,bottom),l*color,Vector2.zero);
                mesh.AddVert(new Vector2(left,top),l*color,Vector2.zero);
                mesh.AddVert(new Vector2(right,top),h*color,Vector2.zero);
                mesh.AddVert(new Vector2(right,bottom),h*color,Vector2.zero);
                mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
            }
            Color lip=Color.Lerp(fill,theme.ActionEdge,.68f);
            Band(r.xMin,divider,r.yMin,r.yMax,fill,theme.ActionFill);
            Band(divider,r.xMax,r.yMin,r.yMax,end,end);
            Band(divider,divider+1,r.yMin+8,r.yMax-8,lip,lip);
            Band(r.xMin,r.xMax,r.yMax-1,r.yMax,lip,lip);
            return;
        }else{
            corners=new[]{new Vector2(r.xMin+cut,r.yMax),new Vector2(r.xMax,r.yMax),new Vector2(r.xMax,r.yMin+cut),new Vector2(r.xMax-cut,r.yMin),new Vector2(r.xMin,r.yMin),new Vector2(r.xMin,r.yMax-cut)};
        }
        Color upper=Secondary?new Color(.38f,.54f,.77f):new Color(.953f,.816f,.584f);
        Color lower=Secondary?new Color(.18f,.30f,.48f):new Color(.729f,.533f,.282f);
        if(theme!=null){upper=Secondary?theme.ControlHover:theme.ActionHover;lower=Secondary?theme.ControlFill:theme.ActionFill;}
        Color Tint(Vector2 p){float t=Mathf.Clamp01((p.x-r.xMin)/r.width*.85f+(r.yMax-p.y)/r.height*.15f);return Color.Lerp(upper,lower,t)*color;}
        mesh.AddVert(r.center,Tint(r.center),Vector2.zero);
        foreach(var p in corners)mesh.AddVert(p,Tint(p),Vector2.zero);
        for(int i=0;i<corners.Length;i++)mesh.AddTriangle(0,1+i,1+(i+1)%corners.Length);
        if(theme==null)return;
        Color rim=Secondary?theme.ControlEdge:theme.ActionEdge;
        int first=mesh.currentVertCount;
        foreach(var p in corners){
            mesh.AddVert(p,rim*color,Vector2.zero);
            mesh.AddVert(r.center+Vector2.Scale(p-r.center,new Vector2(1-3/r.width,1-3/r.height)),rim*color,Vector2.zero);
        }
        for(int i=0;i<corners.Length;i++){
            int a=first+2*i,b=first+2*((i+1)%corners.Length);
            mesh.AddTriangle(a,b,b+1);mesh.AddTriangle(a,b+1,a+1);
        }
    }
}
