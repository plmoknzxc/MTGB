using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public static partial class CardImageLibrary
{
    private static readonly Dictionary<string,CacheEntry> ThumbnailCache=new(StringComparer.OrdinalIgnoreCase);
    private const long FullBudget=128L*1024*1024, ThumbnailBudget=96L*1024*1024;
    private static long fullBytes,thumbnailBytes,cacheClock;
    private static float nextTrim;

    internal static void TrimCache()
    {
        if(fullBytes<=FullBudget&&thumbnailBytes<=ThumbnailBudget||Time.unscaledTime<nextTrim)return;
        nextTrim=Time.unscaledTime+.5f;
        TrimCacheCore();
    }

    // Keep captured LINQ state out of the every-frame budget check.
    private static void TrimCacheCore()
    {
        var protectedTextures=new HashSet<Texture>();
        // Cached dialogs may keep images while hidden. Protect their references
        // too, so reopening a retained surface never exposes a destroyed sprite.
        foreach(var graphic in UnityEngine.Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(graphic is Image image&&image.overrideSprite!=null)protectedTextures.Add(image.overrideSprite.texture);
            else if(graphic is RawImage raw&&raw.texture!=null)protectedTextures.Add(raw.texture);
        }
        var removed=new HashSet<Sprite>();
        Trim(Cache,ref fullBytes,FullBudget,protectedTextures,removed);
        Trim(ThumbnailCache,ref thumbnailBytes,ThumbnailBudget,protectedTextures,removed);
        foreach(var key in DisplayedFullCards.Where(p=>removed.Contains(p.Value)).Select(p=>p.Key).ToArray())
        {DisplayedFullCards.Remove(key);DisplayedFrames.Remove(key);}
    }

    private static void Trim(Dictionary<string,CacheEntry> cache,ref long bytes,long budget,HashSet<Texture> protectedTextures,HashSet<Sprite> removed)
    {
        if(bytes<=budget)return;
        foreach(var pair in cache.OrderBy(p=>p.Value.LastUse).ToArray())
        {
            var entry=pair.Value;
            if(entry.Sprite!=null&&protectedTextures.Contains(entry.Sprite.texture))continue;
            cache.Remove(pair.Key);bytes-=entry.Bytes;removed.Add(entry.Sprite);
            if(entry.Sprite!=null)
            {
                var texture=entry.Sprite.texture;UnityEngine.Object.Destroy(entry.Sprite);
                if(texture!=null)UnityEngine.Object.Destroy(texture);
            }
            if(bytes<=budget)break;
        }
    }

    private static Texture2D MakeThumbnail(Texture2D source)
    {
        const int width=256;
        int sourceWidth=source.width,sourceHeight=source.height;
        if(sourceWidth<=width)return source;
        int height=Mathf.Max(1,Mathf.RoundToInt(sourceHeight*(float)width/sourceWidth));
        if((SystemInfo.copyTextureSupport&UnityEngine.Rendering.CopyTextureSupport.RTToTexture)!=0)
            return MakeGpuThumbnail(source,width,height);
        var pixels=source.GetPixels32();var result=new Color32[width*height];
        float sx=(float)sourceWidth/width,sy=(float)sourceHeight/height;
        for(int y=0;y<height;y++)
        {
            float py=Mathf.Clamp((y+.5f)*sy-.5f,0,sourceHeight-1);
            int y0=(int)py,y1=Mathf.Min(y0+1,sourceHeight-1);float fy=py-y0;
            for(int x=0;x<width;x++)
            {
                float px=Mathf.Clamp((x+.5f)*sx-.5f,0,sourceWidth-1);
                int x0=(int)px,x1=Mathf.Min(x0+1,sourceWidth-1);float fx=px-x0;
                var top=Color32.Lerp(pixels[y0*sourceWidth+x0],pixels[y0*sourceWidth+x1],fx);
                var bottom=Color32.Lerp(pixels[y1*sourceWidth+x0],pixels[y1*sourceWidth+x1],fx);
                result[y*width+x]=Color32.Lerp(top,bottom,fy);
            }
        }
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,true)
        {name=source.name+"_Thumbnail",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp,mipMapBias=.3f};
        texture.SetPixels32(result);UnityEngine.Object.Destroy(source);
        return texture;
    }

    private static Texture2D MakeGpuThumbnail(Texture2D source,int width,int height)
    {
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,true)
        {name=source.name+"_Thumbnail",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp,mipMapBias=.3f};
        // Allocate the GPU levels and discard their CPU copy before copying.
        // Applying afterwards would overwrite the freshly generated GPU image.
        texture.Apply(false,true);
        var descriptor=new RenderTextureDescriptor(width,height)
        {graphicsFormat=texture.graphicsFormat,depthBufferBits=0,useMipMap=true,autoGenerateMips=false,mipCount=texture.mipmapCount};
        var temporary=RenderTexture.GetTemporary(descriptor);
        var previous=RenderTexture.active;bool srgb=GL.sRGBWrite;
        try
        {
            GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;
            Graphics.Blit(source,temporary);temporary.GenerateMips();
            Graphics.CopyTexture(temporary,texture);
            UnityEngine.Object.Destroy(source);return texture;
        }
        catch{UnityEngine.Object.Destroy(texture);throw;}
        finally
        {
            RenderTexture.active=previous;GL.sRGBWrite=srgb;
            RenderTexture.ReleaseTemporary(temporary);
        }
    }
}
