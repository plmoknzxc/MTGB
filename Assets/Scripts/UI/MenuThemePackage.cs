using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

internal sealed class MenuThemePackage
{
    internal static string Root => Path.Combine(PrintingKeyUtility.ApplicationRoot,"Themes");
    const string BundleFile="art.bundle";
    readonly string folder;
    readonly Dictionary<(string,Type),Object> loaded=new();
    static MenuThemePackage mounted;
    AssetBundle bundle;

    MenuThemePackage(string folder) { this.folder=folder; }

    internal static List<MenuThemeStyle> Discover(string root)
    {
        var result=new List<MenuThemeStyle>();
        try
        {
            Directory.CreateDirectory(root);
            foreach(var dir in Directory.GetDirectories(root).OrderBy(x=>x,StringComparer.Ordinal))
            {
                string manifest=Path.Combine(dir,"theme.json");
                if(!File.Exists(manifest))continue;
                try
                {
                    var style=new MenuThemeStyle {Id="",ChineseName="",EnglishName="",JapaneseName=""};
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(manifest),style);
                    if(string.IsNullOrWhiteSpace(style.Id)||string.IsNullOrWhiteSpace(style.ChineseName))
                        throw new InvalidDataException("需要 Id 和 ChineseName。");
                    var pack=new MenuThemePackage(dir);
                    var keys=new[]{style.Hall,style.DeckBackdrop,style.ReplayBackdrop,style.BattlefieldBackdrop,
                        style.DuelSeatBackdrop,style.BattlefieldFrameAtlas,style.HandFramePath,style.SeatJointPath,
                        style.JournalTabStrip,style.HeaderArt,style.DeckMaterial,style.ButtonSprite,style.ControlFont};
                    foreach(var key in keys.Distinct())
                        pack.CheckFile(key);
                    if(keys.Any(k=>k?.StartsWith("art/",StringComparison.Ordinal)==true)||style.Motion=="tidelights")
                    {
                        using var reader=new BinaryReader(File.OpenRead(Path.Combine(dir,BundleFile)));
                        if(System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8))!="UnityFS\0")
                            throw new InvalidDataException("资源包格式无效。");
                    }
                    style.Package=pack;
                    result.Add(style);
                }
                catch(Exception e){Debug.LogWarning($"跳过主题 {manifest}：{e.Message}");}
            }
        }
        catch(Exception e){Debug.LogWarning("无法读取主题目录："+e.Message);}
        return result;
    }

    string ImagePath(string key)
    {
        var path=Path.GetFullPath(Path.Combine(folder,key));
        if(!path.StartsWith(folder+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("图片必须位于主题目录内。");
        return path;
    }

    void CheckFile(string key)
    {
        if(string.IsNullOrEmpty(key))return;
        string file=key.StartsWith("art/",StringComparison.Ordinal)?Path.Combine(folder,BundleFile)
            : key.StartsWith("./",StringComparison.Ordinal)?ImagePath(key):null;
        if(file!=null&&!File.Exists(file))throw new FileNotFoundException("缺少 "+file);
    }

    internal string MusicUri(string key)
    {
        try
        {
            var file=ImagePath(key);
            if (Path.GetExtension(file).Equals(".ogg",StringComparison.OrdinalIgnoreCase) && File.Exists(file))
                return new Uri(file).AbsoluteUri;
        }
        catch(Exception e) when(e is IOException || e is ArgumentException || e is NotSupportedException || e is UnauthorizedAccessException || e is InvalidDataException)
        { Debug.LogWarning(e.Message); }
        return null;
    }

    AssetBundle Mount()
    {
        // Exiting Play Mode can unload the native bundle while the package is still mounted.
        if(mounted==this && bundle!=null)return bundle;
        mounted?.Unmount();
        mounted=this;
        bundle=AssetBundle.LoadFromFile(Path.Combine(folder,BundleFile));
        return bundle;
    }

    void Unmount()
    {
        if(bundle!=null)bundle.Unload(false);
        bundle=null;mounted=null;
    }

    internal Sprite[] Frames(string key) => Mount()?.LoadAssetWithSubAssets<Sprite>(key) ?? Array.Empty<Sprite>();

    internal T Load<T>(string key) where T:Object
    {
        if(!key.StartsWith("art/",StringComparison.Ordinal)&&!key.StartsWith("./",StringComparison.Ordinal))
            return Resources.Load<T>(key);
        var cacheKey=(key,typeof(T));
        if(loaded.TryGetValue(cacheKey,out var existing)&&existing!=null)return existing as T;
        Object asset=null;
        if(key.StartsWith("art/",StringComparison.Ordinal))asset=Mount()?.LoadAsset<T>(key);
        else if(typeof(T)==typeof(Texture2D))
        {
            var tex=new Texture2D(2,2,TextureFormat.RGBA32,false){name=Path.GetFileNameWithoutExtension(key),wrapMode=TextureWrapMode.Clamp};
            try
            {
                if(tex.LoadImage(File.ReadAllBytes(ImagePath(key)),true))asset=tex;
            }
            catch(Exception e) when(e is IOException || e is UnauthorizedAccessException){Debug.LogWarning(e.Message);}
            finally{if(asset==null){if(Application.isPlaying)Object.Destroy(tex);else Object.DestroyImmediate(tex);}}
        }
        else if(typeof(T)==typeof(Sprite))
        {
            var tex=Load<Texture2D>(key);
            if(tex!=null)asset=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(.5f,.5f));
        }
        loaded[cacheKey]=asset;
        return asset as T;
    }

    internal void Release()
    {
        if(mounted==this)Unmount();
        loaded.Clear();
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    static void BeforeReload() => UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += () => mounted?.Unmount();
#endif
}
