using UnityEngine;
using UnityEngine.UI;

// A fixed illustration crop that blends into the adjacent text column.
// The alpha ramp uses the cropped texture UVs, so nothing behind the row is sampled.
public sealed class JournalIllustration : RawImage
{
    internal bool FadeLeft, Solid;
    internal bool FeatherPortrait;
    static readonly float[] portraitX={0,.22f,.46f,.76f,.90f,1};
    static readonly float[] portraitAlphaX={0,.26f,.94f,1,.55f,0};
    static readonly float[] portraitY={0,.19f,.95f,1};
    static readonly float[] portraitAlphaY={.08f,.88f,1,.05f};
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        if(Solid){base.OnPopulateMesh(mesh);return;}
        mesh.Clear();
        Rect r=GetPixelAdjustedRect(), uv=uvRect;
        if(FeatherPortrait){
            for(int y=0;y<portraitY.Length;y++){
                for(int x=0;x<portraitX.Length;x++){
                    float u=portraitX[x],v=portraitY[y];
                    Color tint=color;tint.a*=portraitAlphaX[x]*portraitAlphaY[y];
                    mesh.AddVert(new Vector2(r.xMin+r.width*u,r.yMin+r.height*v),tint,
                        new Vector2(uv.xMin+uv.width*u,uv.yMin+uv.height*v));
                    if(x==0||y==0)continue;
                    int i=mesh.currentVertCount-1,below=i-portraitX.Length;
                    mesh.AddTriangle(below-1,i-1,i);mesh.AddTriangle(below-1,i,below);
                }
            }
            return;
        }
        for(int column=0;column<4;column++){
            int stop=FadeLeft?3-column:column;
            float t=stop switch { 0=>0, 1=>.60f, 2=>.80f, _=>1 };
            Color tint=color;
            tint.a*=stop switch { 2=>.75f, 3=>0, _=>1f };
            float sample=FadeLeft?1-t:t;
            float x=r.xMin+r.width*sample,u=uv.xMin+uv.width*sample;
            mesh.AddVert(new Vector2(x,r.yMin),tint,new Vector2(u,uv.yMin));
            mesh.AddVert(new Vector2(x,r.yMax),tint,new Vector2(u,uv.yMax));
            if(column==0)continue;
            int i=2*column;
            mesh.AddTriangle(i-2,i-1,i+1);mesh.AddTriangle(i-2,i+1,i);
        }
    }
}
