using UnityEngine;
using UnityEngine.UI;

// Lives on the overlay. Neither the sparks nor their label participate in raycasts.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class BattlefieldDamageGraphic : MaskableGraphic
{
    public int Target { get; private set; }
    public bool Player { get; private set; }
    public bool LifeLoss { get; private set; }
    Text label;
    CanvasGroup fade;
    float age;
    long shownAmount;
    Vector2 origin;
    RectTransform follow;

    public void Initialize(Text number)
    {
        label = number;
        raycastTarget = label.raycastTarget = false;
        fade = gameObject.AddComponent<CanvasGroup>();
        fade.interactable = fade.blocksRaycasts = false;
    }

    public void Play(Vector2 point, RectTransform anchor, int target, bool player, bool loss, long amount,float contactDelay=0)
    {
        bool combine = player && gameObject.activeSelf && age < .25f && Target == target && Player == player && LifeLoss == loss;
        shownAmount = combine ? shownAmount + amount : amount;
        Target = target;
        Player = player;
        LifeLoss = loss;
        age = -contactDelay;
        origin = point;
        follow = anchor;
        gameObject.SetActive(true);
        label.gameObject.SetActive(loss || !player);
        label.text = (loss || !player ? "−" : "") + shownAmount;
        label.color = loss ? new Color(1, .66f, .65f) : new Color(1, .86f, .83f);
        label.rectTransform.anchoredPosition = loss ? Vector2.zero : new Vector2(25, 15);
        DrawFrame();
    }

    void LateUpdate()
    {
        age += Mathf.Min(Time.unscaledDeltaTime, .05f);
        if (age >= (LifeLoss ? .78f : .46f)) gameObject.SetActive(false);
        else DrawFrame();
    }

    void DrawFrame()
    {
        float t = Mathf.Clamp01(age / (LifeLoss ? .78f : .46f));
        if (follow != null && follow.gameObject.activeInHierarchy)
            origin = rectTransform.parent.InverseTransformPoint(follow.TransformPoint(follow.rect.center));
        fade.alpha = age<0 ? 0 : 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.47f, 1, t));
        rectTransform.anchoredPosition = origin + (LifeLoss ? new Vector2(53, -7 + 16 * (1-(1-t)*(1-t))) : Vector2.zero);
        if(!Player)label.rectTransform.anchoredPosition=new Vector2(25,15+12*t);
        label.rectTransform.localScale = Vector3.one * (1 + .12f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / .32f)));
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (LifeLoss) return;
        float t = Mathf.Clamp01(age / .34f);
        if (t >= 1) return;
        float spread = 1 - (1-t)*(1-t);
        if(!Player){
            // A brief contact flash on the card; the floating amount outlasts the light.
            float flash=Mathf.Clamp01(1-age/.19f);
            if(flash<=0)return;
            var hot=new Color(1,.97f,.90f,flash);
            var edge=new Color(.92f,.66f,.49f,0);
            mesh.AddVert(Vector2.zero,hot,Vector2.zero);
            for(int i=0;i<12;i++){
                float angle=(i*30+24)*Mathf.Deg2Rad;
                float radius=(i%2==0?(i%6==0?33:20):7)*(1.25f-flash*.48f);
                mesh.AddVert(new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,edge,Vector2.zero);
            }
            for(int i=0;i<12;i++)mesh.AddTriangle(0,1+i,1+(i+1)%12);
            return;
        }
        for (int i = 0; i < 6; i++) {
            float angle = (i * 60 + 19) * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var across = new Vector2(-direction.y, direction.x);
            float length = (i % 2 == 0 ? 21 : 13) * (1-t);
            var start = direction * ((Player ? 21 : 6) + spread * 23);
            var middle = start + direction * (length * .28f);
            int n = mesh.currentVertCount;
            mesh.AddVert(start, new Color(1, .96f, .88f), Vector2.zero);
            mesh.AddVert(middle + across * (2.6f * (1-t)), new Color(1, .77f, .62f), Vector2.zero);
            mesh.AddVert(start + direction * length, new Color(.91f, .41f, .37f), Vector2.zero);
            mesh.AddVert(middle - across * (2.6f * (1-t)), new Color(1, .77f, .62f), Vector2.zero);
            mesh.AddTriangle(n, n+1, n+2);
            mesh.AddTriangle(n, n+2, n+3);
        }
    }
}
