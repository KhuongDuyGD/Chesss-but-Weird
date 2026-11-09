using UnityEngine;
using UnityEngine.UI;

/// <summary>Ink envelope/newspaper symbols; no font-dependent emoji or external textures.</summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public sealed class CommunityDestinationIcon : MaskableGraphic
{
    private bool mail;
    private bool gift;
    public void Configure(bool isMail) { mail = isMail; raycastTarget = false; color = SketchbookUI.Ink; SetVerticesDirty(); }
    public void ConfigureGift() { gift = true; raycastTarget = false; color = SketchbookUI.Ink; SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        void Line(float x1, float y1, float x2, float y2)
        {
            var rect = rectTransform.rect;
            var a = new Vector2(rect.xMin + x1 * rect.width, rect.yMin + y1 * rect.height);
            var b = new Vector2(rect.xMin + x2 * rect.width, rect.yMin + y2 * rect.height);
            var normal = (b - a).normalized; normal = new Vector2(-normal.y, normal.x) * 1.1f;
            int i = vh.currentVertCount;
            vh.AddVert(a - normal, color, Vector2.zero); vh.AddVert(a + normal, color, Vector2.zero);
            vh.AddVert(b + normal, color, Vector2.zero); vh.AddVert(b - normal, color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
        if (gift)
        {
            Line(.12f, .08f, .88f, .08f); Line(.88f, .08f, .88f, .65f); Line(.88f, .65f, .12f, .65f); Line(.12f, .65f, .12f, .08f);
            Line(.05f, .65f, .95f, .65f); Line(.05f, .53f, .95f, .53f); Line(.5f, .08f, .5f, .65f);
            Line(.5f, .65f, .2f, .82f); Line(.2f, .82f, .3f, .97f); Line(.3f, .97f, .5f, .65f);
            Line(.5f, .65f, .8f, .82f); Line(.8f, .82f, .7f, .97f); Line(.7f, .97f, .5f, .65f);
            return;
        }
        Line(.05f, .12f, .95f, .12f); Line(.95f, .12f, .95f, .85f); Line(.95f, .85f, .05f, .85f); Line(.05f, .85f, .05f, .12f);
        if (mail) { Line(.05f, .85f, .5f, .4f); Line(.5f, .4f, .95f, .85f); }
        else { Line(.2f, .66f, .8f, .66f); Line(.2f, .49f, .8f, .49f); Line(.2f, .31f, .8f, .31f); }
    }
}
