using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// A local procedural mesh: animation has no RNG and never determines rules.
// Coin sides and win/lose are distinct native outcomes, not interchangeable.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RandomResultGraphic : MaskableGraphic
{
    public bool Coin {get;set;}
    public string Outcome {get;set;}
    public float Progress {get;private set;}
    public void Animate(float progress){Progress=progress;SetVerticesDirty();}
    static readonly float Phi=(1+Mathf.Sqrt(5))/2;
    static readonly Vector3[] Points={new Vector3(-1,Phi,0),new Vector3(1,Phi,0),new Vector3(-1,-Phi,0),new Vector3(1,-Phi,0),new Vector3(0,-1,Phi),new Vector3(0,1,Phi),new Vector3(0,-1,-Phi),new Vector3(0,1,-Phi),new Vector3(Phi,0,-1),new Vector3(Phi,0,1),new Vector3(-Phi,0,-1),new Vector3(-Phi,0,1)};
    static readonly int[][] Faces={new[]{0,11,5},new[]{0,5,1},new[]{0,1,7},new[]{0,7,10},new[]{0,10,11},new[]{1,5,9},new[]{5,11,4},new[]{11,10,2},new[]{10,7,6},new[]{7,1,8},new[]{3,9,4},new[]{3,4,2},new[]{3,2,6},new[]{3,6,8},new[]{3,8,9},new[]{4,9,5},new[]{2,4,11},new[]{6,2,10},new[]{8,6,7},new[]{9,8,1}};
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();float r=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.5f;
        float tumble=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(Progress/.67f));
        if(Coin)DrawCoin(mesh,r,tumble);else DrawDie(mesh,r,tumble);
    }
    void Tri(VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Color tint)
    {
        int i=mesh.currentVertCount;tint.a*=color.a;
        mesh.AddVert(a,tint,Vector2.zero);mesh.AddVert(b,tint,Vector2.zero);mesh.AddVert(c,tint,Vector2.zero);mesh.AddTriangle(i,i+1,i+2);
    }
    void Disc(VertexHelper mesh,float r,float flatten,Color tint)
    {
        for(int i=0;i<64;i++) {
            float a=i*Mathf.PI*2/64,b=(i+1)*Mathf.PI*2/64;
            Tri(mesh,Vector2.zero,new Vector2(Mathf.Cos(a)*r*flatten,Mathf.Sin(a)*r),new Vector2(Mathf.Cos(b)*r*flatten,Mathf.Sin(b)*r),tint);
        }
    }
    void DrawCoin(VertexHelper mesh,float r,float tumble)
    {
        float turn=tumble*Mathf.PI*8,flatten=Mathf.Max(.065f,Mathf.Abs(Mathf.Cos(turn)));
        bool reverse=Outcome=="tails";
        var rim=new Color(.94f,.72f,.25f);var dark=new Color(.36f,.19f,.035f);var face=new Color(.73f,.46f,.12f);
        if(reverse){face=new Color(.65f,.74f,.82f);rim=new Color(.93f,.96f,1);dark=new Color(.19f,.27f,.34f);}
        Disc(mesh,r,flatten,dark);Disc(mesh,r*.96f,flatten,rim);Disc(mesh,r*.86f,flatten,dark);Disc(mesh,r*.8f,flatten,face);
        if(flatten<.16f)return;
        // A win is not proof of heads (the player may have called tails).
        // Show outcome marks for win/lose; localized heads/tails are engraved by the parent.
        if(Outcome=="win") {
            Stroke(mesh,new Vector2(-.42f,-.03f),new Vector2(-.12f,-.3f),r,flatten,new Color(.7f,1,.55f));
            Stroke(mesh,new Vector2(-.12f,-.3f),new Vector2(.42f,.36f),r,flatten,new Color(.7f,1,.55f));
        }else if(Outcome=="lose") {
            Stroke(mesh,new Vector2(-.3f,-.3f),new Vector2(.3f,.3f),r,flatten,new Color(1,.5f,.3f));
            Stroke(mesh,new Vector2(-.3f,.3f),new Vector2(.3f,-.3f),r,flatten,new Color(1,.5f,.3f));
        }
    }
    void Stroke(VertexHelper mesh,Vector2 from,Vector2 to,float r,float flatten,Color tint)
    {
        Vector2 d=(to-from).normalized,side=new Vector2(-d.y,d.x)*.065f;
        Vector2 Project(Vector2 p)=>new Vector2(p.x*flatten,p.y)*r;
        Tri(mesh,Project(from-side),Project(to-side),Project(to+side),tint);
        Tri(mesh,Project(from-side),Project(to+side),Project(from+side),tint);
    }
    void DrawDie(VertexHelper mesh,float r,float tumble)
    {
        Vector3 front=(Points[0]+Points[11]+Points[5]).normalized;
        var settled=Quaternion.FromToRotation(front,Vector3.forward);
        var spin=Quaternion.Euler(tumble*650,tumble*490,tumble*270)*settled;
        var vertices=Points.Select(p=>spin*p.normalized).ToArray();
        foreach(int faceIndex in Enumerable.Range(0,Faces.Length).OrderBy(i=>Faces[i].Sum(v=>vertices[v].z))) {
            var face=Faces[faceIndex];
            Vector3 a=vertices[face[0]],b=vertices[face[1]],c=vertices[face[2]];
            var normal=Vector3.Cross(b-a,c-a).normalized;if(normal.z<=0)continue;
            Vector2 Project(Vector3 p)=>new Vector2(p.x,p.y)*(r*3/(3-p.z*.3f));
            var pa=Project(a);var pb=Project(b);var pc=Project(c);var center=(pa+pb+pc)/3;
            float light=Mathf.Clamp01(Vector3.Dot(normal,new Vector3(-.3f,.5f,1).normalized)*.65f+.35f);
            Tri(mesh,pa,pb,pc,new Color(.055f,.09f,.13f));
            Tri(mesh,Vector2.Lerp(pa,center,.055f),Vector2.Lerp(pb,center,.055f),Vector2.Lerp(pc,center,.055f),Color.Lerp(new Color(.09f,.19f,.28f),new Color(.58f,.77f,.87f),light));
            // Face zero settles toward the viewer with the exact native value.
            // Other faces are decorative; they never produce a rule result.
            string value=faceIndex==0?Outcome:(faceIndex+1).ToString();
            if(faceIndex>0&&int.TryParse(Outcome,out int result)&&result>=1&&result<=20&&faceIndex+1==result)value="1";
            var localCenter=face.Select(i=>Points[i].normalized).Aggregate(Vector3.zero,(sum,p)=>sum+p)/3;
            var localNormal=localCenter.normalized;
            int top=face.OrderByDescending(i=>(settled*Points[i]).y).First();
            var up=(Points[top].normalized-localCenter).normalized;
            var right=Vector3.Cross(up,localNormal).normalized;
            float height=faceIndex==0?.40f:.32f,width=height*.52f,gap=height*.16f;
            float total=value.Length*width+(value.Length-1)*gap;
            for(int digit=0;digit<value.Length;digit++) {
                int n=value[digit]-'0';if(n<0||n>9)continue;
                int bits=DigitSegments[n];
                for(int segment=0;segment<7;segment++) {
                    if((bits&(1<<segment))==0)continue;
                    Vector2 from=Segments[segment*2],to=Segments[segment*2+1];
                    Vector3 Position(Vector2 p)=>spin*(localCenter+right*(digit*(width+gap)-total/2+p.x*width)+up*((p.y-.5f)*height));
                    Vector2 start=Project(Position(from)),end=Project(Position(to));
                    Vector2 side=Project(Position(from+new Vector2(-(to-from).y,(to-from).x).normalized*.095f))-start;
                    var ink=faceIndex==0?new Color(1,.93f,.66f):new Color(.025f,.065f,.1f);
                    Tri(mesh,start-side,end-side,end+side,ink);Tri(mesh,start-side,end+side,start+side,ink);
                }
            }
        }
    }
    // Top, upper right, lower right, bottom, lower left, upper left, middle.
    static readonly int[] DigitSegments={63,6,91,79,102,109,125,7,127,111};
    static readonly Vector2[] Segments={new Vector2(0,1),new Vector2(1,1),new Vector2(1,1),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(1,0),new Vector2(1,0),new Vector2(0,0),new Vector2(0,0),new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(0,1),new Vector2(0,.5f),new Vector2(1,.5f)};
}
