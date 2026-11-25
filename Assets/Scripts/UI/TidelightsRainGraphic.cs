using UnityEngine;
using UnityEngine.UI;

// Coordinates follow the approved 1536 × 864 illustration, measured from its upper left.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TidelightsRainGraphic : MaskableGraphic
{
    const int Width = 1536, Height = 864;
    static readonly Color32 RainColor = new Color32(210, 222, 239, 255);
    static readonly Color32 WaterColor = new Color32(192, 214, 234, 255);

    // Each drop has a painted rib tip, a landing depth and its own refill rate.
    // Where the canal is visible, a ring starts at precisely the end of the fall.
    struct RimDrop
    {
        public float landingY, driftSpeed, period, offset, ring;
        public RimDrop(int landingY, float driftSpeed, float period, float offset, float ring = 0)
        {
            this.landingY = landingY; this.driftSpeed = driftSpeed;
            this.period = period; this.offset = offset; this.ring = ring;
        }
    }
    static readonly RimDrop[] Drops = {
        new RimDrop(745,14f,3.31f,.18f,15), new RimDrop(724,11f,2.83f,1.41f,15),
        new RimDrop(676,4f,3.77f,.93f,7), new RimDrop(468,14f,3.14f,1.84f),
        new RimDrop(544,11f,3.62f,2.68f), new RimDrop(757,18f,3.95f,1.18f,22)
    };
    static readonly Vector2[] Water = {
        new Vector2(245,745), new Vector2(309,745), new Vector2(305,697), new Vector2(324,723),
        new Vector2(418,679), new Vector2(443,715), new Vector2(456,644),
        new Vector2(1470,686), new Vector2(1507,716), new Vector2(1456,743),
        new Vector2(1496,797), new Vector2(1474,839), new Vector2(1520,766)
    };
    static readonly Vector2[] RingPoints = Ellipse();
    // A cherry petal has a shallow notch between its two outer lobes.
    static readonly Vector2[] PetalOutline = {
        new Vector2(0,1), new Vector2(-.68f,.22f), new Vector2(-.84f,-.42f), new Vector2(-.52f,-.77f),
        new Vector2(-.08f,-.51f), new Vector2(.23f,-.92f), new Vector2(.67f,-.80f), new Vector2(.81f,-.28f)
    };
    Rect area;
    float seconds;
    TidelightsClip clip;
    internal void Bind(TidelightsClip source) => clip = source;

    public void SetTime(float value)
    {
        seconds = value;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        area = rectTransform.rect;
        if (area.width <= 0 || area.height <= 0) return;
        // The moving foreground is drawn in front of this layer, including between loose strands.
        for (int i = 0; i < 292; i++) RainStroke(mesh, i);
        for (int i = 0; i < Water.Length; i++)
        {
            float age = Mathf.Repeat(seconds + 2.7f * Noise((uint)i + 128), 1.58f + .91f * Noise((uint)i + 270));
            if (age < .82f) Ripple(mesh, Water[i], age / .82f, 12 + 9 * Noise((uint)i + 47));
        }
        if (clip != null)
            for (int i = 0; i < Drops.Length; i++) Runoff(mesh, i, Drops[i]);
        for (int i = 0; i < 3; i++) Petal(mesh, i);
    }

    static float Noise(uint k)
    {
        unchecked { k ^= k >> 16; k *= 0x7feb352du; k ^= k >> 15; k *= 0x846ca68bu; k ^= k >> 16; }
        return (k & 0x00ffffffu) / 16777216f;
    }

    void RainStroke(VertexHelper mesh, int index)
    {
        uint key = (uint)(index * 19);
        bool near = index % 4 == 0;
        float speed = (near ? 550 : 275) + Noise(key + 2) * (near ? 145 : 130);
        const float travel = Height + 98;
        float cycle = seconds * (speed / travel) + Noise(key + 3);
        int pass = Mathf.FloorToInt(cycle);
        float y = (cycle - pass) * travel - 28;
        float xBase = Noise(unchecked(key + (uint)pass * 397u));
        float slope = .13f + .09f * Noise(key + 4);
        float x = Mathf.Lerp(-190, Width, xBase) + y * slope;
        float length = (near ? 21 : 9) + Noise(key + 7) * (near ? 23 : 12);
        Vector2 head = new Vector2(x, y), tail = head - new Vector2(length * slope, length);
        if (head.y < 0 || tail.y > Height || tail.x > Width || head.x < 0) return;
        float opacity = (near ? .59f : .34f) * (.6f + .4f * Noise(key + 8)) * Shade(x);
        float radius = near ? .77f : .52f;
        // Keep the cool glint at the falling end and taper the trailing ink.
        Strip(mesh, tail, head, radius * .38f, radius, RainColor, opacity * .08f, opacity);
    }

    void Runoff(VertexHelper mesh, int index, RimDrop drop)
    {
        const float grow = .67f, halfGravity = 910f, neck = 2.6f;
        float age = Mathf.Repeat(seconds + drop.offset, drop.period);
        Vector2 tip = clip.RibAt(index, seconds);
        float alpha = .91f * Shade(tip.x);
        if (age < grow)
        {
            float size = Mathf.SmoothStep(0, 1, age / grow);
            Bead(mesh, tip + new Vector2(0, neck * size), 2.0f * size, 3.1f * size, alpha * size);
            return;
        }

        // Once the neck breaks, the world-space trajectory depends only on the release pose.
        float sinceRelease = age - grow;
        var origin = clip.RibAt(index, seconds - sinceRelease) + new Vector2(0, neck);
        float flight = Mathf.Sqrt((drop.landingY - origin.y) / halfGravity);
        if (sinceRelease < flight)
        {
            float t = sinceRelease / flight;
            var pos = origin + new Vector2(drop.driftSpeed * sinceRelease, halfGravity * sinceRelease * sinceRelease);
            if (drop.ring == 0) alpha *= Mathf.Clamp01((1 - t) * 11);
            Bead(mesh, pos, 2.0f - .65f * t, 3.1f + 4.9f * t, alpha);
        }
        else if (drop.ring > 0 && sinceRelease < flight + .92f)
        {
            var impact = new Vector2(origin.x + drop.driftSpeed * flight, drop.landingY);
            Ripple(mesh, impact, (sinceRelease - flight) / .92f, drop.ring);
        }
    }

    void Bead(VertexHelper mesh, Vector2 center, float w, float h, float alpha)
    {
        if (h < .2f) return;
        // The top point draws a neck, the broad lower edge catches the sky.
        Strip(mesh, center - new Vector2(0,h), center + new Vector2(0,.28f*h), .12f*w, w, WaterColor, alpha*.32f, alpha);
        Strip(mesh, center + new Vector2(0,.28f*h), center + new Vector2(0,h), w, w*.22f, WaterColor, alpha, alpha*.72f);
    }

    static Vector2[] Ellipse()
    {
        var points = new Vector2[21];
        for (int i = 0; i < points.Length; i++)
        {
            float a = i * Mathf.PI / 10;
            points[i] = new Vector2(Mathf.Cos(a), .19f * Mathf.Sin(a));
        }
        return points;
    }

    void Ripple(VertexHelper mesh, Vector2 center, float t, float radius)
    {
        float alpha = (1 - t) * Mathf.Clamp01(t * 15) * .82f * Shade(center.x);
        for (int echo = 0; echo < 2; echo++)
        {
            float progress = t - .16f * echo;
            if (progress <= 0) continue;
            float r = radius * Mathf.Sqrt(progress);
            float opacity = echo == 0 ? alpha : alpha * .49f * Mathf.Clamp01(progress * 14);
            for (int i = 0; i < RingPoints.Length - 1; i++)
            {
                if (i == 8 || i == 9 || i == 18) continue;
                Vector2 a = center + RingPoints[i] * r, b = center + RingPoints[i+1] * r;
                float v = opacity * (i < 10 ? 1 : .53f);
                Strip(mesh, a, b, .54f, .54f, WaterColor, v, v);
            }
        }
    }

    static float Shade(float x) => Mathf.Lerp(.60f, 1, Mathf.InverseLerp(270, 950, x));

    void Petal(VertexHelper mesh, int i)
    {
        float age = Mathf.Repeat(seconds + 3.9f * i, 11.7f);
        const float flight = 4.65f;
        if (age >= flight) return;
        float t = age / flight;
        // A branch at upper left, the far right bridge, and the open water below the menu.
        Vector2 from = i == 0 ? new Vector2(152, 11) : i == 1 ? new Vector2(1491, 238) : new Vector2(226, 641);
        float descent = i == 2 ? 100 : 158;
        Vector2 p = from + new Vector2(60*t + 24*Mathf.Sin(t*7 + i), descent*t - 7*Mathf.Sin(t*8));
        float alpha = Mathf.Clamp01(Mathf.Min(t, 1-t) * 8) * Shade(p.x);
        float turn = t * 10 + i, tilt = .55f * Mathf.Sin(turn * .63f) - .4f;
        Vector2 axis = new Vector2(Mathf.Cos(tilt), Mathf.Sin(tilt));
        Vector2 cross = new Vector2(-axis.y, axis.x);
        float scale = i == 1 ? 5.6f : 4.7f;
        float foreshortening = .28f + .72f * Mathf.Abs(Mathf.Cos(turn));
        int first = mesh.currentVertCount;
        var light = new Color32(226, 179, 195, (byte)(alpha * 218));
        var shade = new Color32(175, 121, 151, (byte)(alpha * 225));
        mesh.AddVert(Local(p), shade, Vector2.zero);
        for (int k = 0; k < PetalOutline.Length; k++)
        {
            Vector2 point = PetalOutline[k];
            Vector2 position = p + axis * (scale * point.x * foreshortening) + cross * (scale * point.y);
            mesh.AddVert(Local(position), k > 3 ? light : shade, Vector2.zero);
        }
        for (int k = 0; k < PetalOutline.Length; k++)
            mesh.AddTriangle(first, first + 1 + k, first + 1 + (k + 1) % PetalOutline.Length);
    }

    void Strip(VertexHelper mesh, Vector2 a, Vector2 b, float fromWidth, float toWidth, Color32 ink, float alphaA, float alphaB)
    {
        Vector2 dir = b - a;
        Vector2 side = new Vector2(-dir.y, dir.x) / Mathf.Max(.001f, dir.magnitude);
        int first = mesh.currentVertCount;
        ink.a = (byte)(Mathf.Clamp01(alphaA) * 255);
        mesh.AddVert(Local(a + side * fromWidth), ink, Vector2.zero);
        mesh.AddVert(Local(a - side * fromWidth), ink, Vector2.zero);
        ink.a = (byte)(Mathf.Clamp01(alphaB) * 255);
        mesh.AddVert(Local(b - side * toWidth), ink, Vector2.zero);
        mesh.AddVert(Local(b + side * toWidth), ink, Vector2.zero);
        mesh.AddTriangle(first, first + 1, first + 2);
        mesh.AddTriangle(first, first + 2, first + 3);
    }

    Vector3 Local(Vector2 p) => new Vector3(area.xMin + area.width * p.x / Width, area.yMax - area.height * p.y / Height, 0);
}
