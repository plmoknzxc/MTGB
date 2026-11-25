using System;
using System.IO;
using UnityEngine;

// Immutable mesh and pose table exported from the approved Cubism model.
internal sealed class TidelightsClip
{
    const string Path = "art/";
    internal const int RibCount = 6;
    static readonly System.Collections.Generic.Dictionary<MenuThemeStyle,TidelightsClip> clips = new();
    MenuThemeStyle owner;
    int users;
    internal readonly Texture2D Atlas, Scenery;
    internal readonly Shader Shader;
    internal readonly Vector2 Size;
    internal readonly UIVertex[] Vertices;
    internal readonly int[] Indices;
    internal readonly Vector4[] Body, Hair;
    readonly Vector2[] ribs;
    internal readonly int Fps;

    TidelightsClip(BinaryReader r, Texture2D atlas, Texture2D scenery, Shader shader)
    {
        if (r.ReadUInt32() != 0x314D4454) throw new InvalidDataException("Tidelights mesh version");
        Atlas = atlas;
        Scenery = scenery;
        Shader = shader;
        Size = new Vector2(r.ReadInt32(), r.ReadInt32());
        int vertices = r.ReadInt32(), indices = r.ReadInt32(), frames = r.ReadInt32();
        Fps = r.ReadInt32();
        long remaining = (long)vertices * 60 + (long)indices * 2 + (long)frames * (6 + 2 * RibCount) * 4;
        if (Size.x <= 0 || Size.y <= 0 || vertices <= 0 || vertices > 65535 || indices <= 0 ||
            indices % 3 != 0 || frames <= 0 || Fps <= 0 || remaining != r.BaseStream.Length-r.BaseStream.Position)
            throw new InvalidDataException("Tidelights mesh dimensions");
        Vertices = new UIVertex[vertices];
        Indices = new int[indices];
        Body = new Vector4[frames];
        Hair = new Vector4[frames];
        ribs = new Vector2[frames * RibCount];
        for (int i = 0; i < vertices; i++)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = ReadPair(r);
            vertex.uv0 = ReadPair(r);
            vertex.uv1 = ReadFour(r);
            vertex.uv2 = ReadFour(r);
            vertex.uv3 = new Vector4(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), 0);
            Vertices[i] = vertex;
        }
        for (int i = 0; i < indices; i++) Indices[i] = r.ReadUInt16();
        for (int i = 0; i < frames; i++)
        {
            Body[i] = ReadFour(r);
            Hair[i] = ReadPair(r);
            for (int j = 0; j < RibCount; j++) ribs[i * RibCount+j] = ReadPair(r);
        }
    }

    static Vector2 ReadPair(BinaryReader r) => new Vector2(r.ReadSingle(), r.ReadSingle());
    static Vector4 ReadFour(BinaryReader r) => new Vector4(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

    internal int FrameAt(double seconds)
    {
        int n = (int)(Math.Floor(seconds * Fps) % Body.Length);
        return n < 0 ? n + Body.Length : n;
    }

    internal Vector2 RibAt(int rib, double seconds) => ribs[FrameAt(seconds) * RibCount + rib];

    internal static TidelightsClip Acquire(MenuThemeStyle theme)
    {
        if (!clips.TryGetValue(theme,out var shared))
        {
            var data = theme.Load<TextAsset>(Path + "TideIdle");
            var atlas = theme.Load<Texture2D>(Path + "TideIdleAtlas");
            var scenery = theme.Load<Texture2D>(Path + "TideRainScenery");
            var shader = theme.Load<Shader>(Path + "TideIdleMesh");
            try
            {
                if (data != null && atlas != null && scenery != null && shader != null && shader.isSupported)
                {
                    using var reader = new BinaryReader(new MemoryStream(data.bytes, false));
                    shared = new TidelightsClip(reader, atlas, scenery, shader) { owner = theme };
                    clips[theme] = shared;
                }
            }
            catch (IOException e) { Debug.LogWarning(e.Message); }
            finally
            {
                if (data != null) Resources.UnloadAsset(data);
                if (shared == null)
                {
                    if (atlas != null) Resources.UnloadAsset(atlas);
                    if (scenery != null) Resources.UnloadAsset(scenery);
                }
            }
        }
        if (shared != null) shared.users++;
        return shared;
    }

    internal void Release()
    {
        if (--users != 0) return;
        clips.Remove(owner);
        Resources.UnloadAsset(Atlas);
        Resources.UnloadAsset(Scenery);
    }
}
