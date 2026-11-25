using System;
using System.Text.RegularExpressions;
using UnityEngine;

// Cost facts are independent of both Canvas and mesh rendering.
public sealed class BattlefieldWorldCosts
{
    public string printed,main,printedSecondary,secondary;
    public bool highTitle,rotated,printedIsSecondary;
    public bool MainChanged=>!Same(printed,printedIsSecondary?secondary:main);
    public bool SecondaryChanged=>!Same(printedSecondary,secondary);
    static bool Same(string a,string b)=>Regex.Replace(a??"",@"\s+","").Equals(Regex.Replace(b??"",@"\s+",""),StringComparison.OrdinalIgnoreCase);
    public string PatchKey=>printed+"/"+printedSecondary+"/"+highTitle+"/"+rotated;
    public Texture2D CreatePatch(Sprite art,bool second,out Rect bounds)
    {
        int count=Regex.Matches((second?printedSecondary:printed)??"",@"\{[^{}]+\}").Count;
        float left=second?Mathf.Max(.07f,.488f-count*.048f-.008f):Mathf.Max(.06f,.938f-count*(count>6?.05f:.056f)-.026f);
        bounds=second?new Rect(left,.329f,.488f-left,.044f):new Rect(left,highTitle?.918f:.89f,.938f-left,highTitle?.052f:.066f);
        if(rotated)bounds=second?new Rect(.043f,.963f-count*.035f-.023f,.086f,count*.035f+.011f):new Rect(.043f,.5f-count*.035f-.023f,.086f,count*.035f+.016f);
        return count>0?LegacyCardCost.Cover(art,ref bounds,rotated,second||highTitle?0:count):null;
    }
}
