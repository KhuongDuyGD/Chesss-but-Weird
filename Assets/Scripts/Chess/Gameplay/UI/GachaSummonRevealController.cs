using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GachaSummonRevealController : MonoBehaviour
{
    private const string AssetFolder = "Assets/Materials/Gacha_menu";
    private const float DesignWidth = 1672f;
    private const float DesignHeight = 941f;

    private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    private readonly List<Sprite> runtimeSprites = new List<Sprite>();
    private readonly List<RevealSpark> sparks = new List<RevealSpark>();
    private readonly List<RectTransform> chessGlyphs = new List<RectTransform>();

    private RectTransform overlay;
    private CanvasGroup canvasGroup;
    private Image background;
    private Image summonRing;
    private Image auraRing;
    private Image rayImage;
    private Image cardFrame;
    private Image rewardIcon;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI detailLabel;
    private TextMeshProUGUI rarityLabel;
    private Coroutine revealRoutine;
    private Action finishedCallback;

    public bool IsPlaying => revealRoutine != null;

    public void Initialize(RectTransform parent)
    {
        if (!parent || overlay)
            return;

        LoadSprites();
        BuildOverlay(parent);
        overlay.gameObject.SetActive(false);
    }

    public void Play(GachaReward reward, Sprite icon, Action onFinished)
    {
        finishedCallback = onFinished;
        if (!overlay)
        {
            onFinished?.Invoke();
            return;
        }

        if (revealRoutine != null)
            StopCoroutine(revealRoutine);

        ConfigureReward(reward, icon);
        revealRoutine = StartCoroutine(PlayRoutine(reward));
    }

    private void BuildOverlay(RectTransform parent)
    {
        overlay = CreateRect(parent, "Gacha Summon Reveal", Vector2.zero, new Vector2(DesignWidth, DesignHeight));
        overlay.SetAsLastSibling();
        canvasGroup = overlay.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        background = overlay.gameObject.AddComponent<Image>();
        background.color = new Color(0.02f, 0.018f, 0.03f, 0f);

        rayImage = AddImage(overlay, "Crown Ray Burst", GetSprite("GachaRevealRay.png"), Vector2.zero, new Vector2(1120f, 1120f));
        rayImage.color = new Color(1f, 0.84f, 0.33f, 0f);

        auraRing = AddImage(overlay, "Summon Aura Ring", GetSprite("GachaRevealRing.png"), Vector2.zero, new Vector2(610f, 610f));
        auraRing.color = new Color(0.36f, 0.78f, 1f, 0f);

        summonRing = AddImage(overlay, "Summon Chess Ring", GetSprite("GachaRevealRing.png"), Vector2.zero, new Vector2(470f, 470f));
        summonRing.color = new Color(1f, 0.92f, 0.42f, 0f);

        for (int i = 0; i < 6; i++)
        {
            RectTransform glyph = AddImage(overlay, $"Chess Glyph {i + 1}", GetSprite($"GachaRevealGlyph{i + 1}.png"), Vector2.zero, new Vector2(76f, 76f)).rectTransform;
            chessGlyphs.Add(glyph);
        }

        for (int i = 0; i < 30; i++)
        {
            Image sparkImage = AddImage(overlay, $"Summon Spark {i + 1}", GetSprite("GachaRevealSpark.png"), Vector2.zero, new Vector2(22f, 22f));
            sparks.Add(new RevealSpark(sparkImage.rectTransform, sparkImage, i));
        }

        RectTransform card = CreateRect(overlay, "Reward Reveal Card", new Vector2(0f, -12f), new Vector2(310f, 410f));
        cardFrame = card.gameObject.AddComponent<Image>();
        cardFrame.sprite = GetSprite("GachaRevealCardFrame.png");
        cardFrame.type = Image.Type.Sliced;
        cardFrame.color = new Color(1f, 0.97f, 0.78f, 0f);

        rewardIcon = AddImage(card, "Reward Icon", null, new Vector2(0f, 42f), new Vector2(180f, 180f));
        rewardIcon.color = new Color(1f, 1f, 1f, 0f);

        rarityLabel = AddText(card, "Reward Rarity", new Vector2(0f, -106f), new Vector2(250f, 42f), 34f, new Color(0.15f, 0.1f, 0.03f, 0f));
        detailLabel = AddText(card, "Reward Detail", new Vector2(0f, -152f), new Vector2(250f, 54f), 32f, new Color(0.15f, 0.1f, 0.03f, 0f));
        titleLabel = AddText(overlay, "Reveal Title", new Vector2(0f, 318f), new Vector2(820f, 74f), 54f, new Color(1f, 0.94f, 0.62f, 0f));
        titleLabel.text = "SUMMON";
    }

    private void ConfigureReward(GachaReward reward, Sprite icon)
    {
        rewardIcon.sprite = icon;
        rewardIcon.enabled = icon;
        rarityLabel.text = new string('*', Mathf.Clamp(reward.rarity, 1, 5));
        detailLabel.text = reward.AmountText;
        titleLabel.text = reward.isTopReward || reward.rarity >= 5 ? "LEGENDARY SUMMON" : "SUMMON";

        Color cardColor = reward.isTopReward || reward.rarity >= 5
            ? new Color(1f, 0.86f, 0.22f, 1f)
            : reward.rarity >= 4 ? new Color(0.42f, 0.83f, 1f, 1f) : new Color(1f, 0.97f, 0.78f, 1f);
        cardFrame.color = WithAlpha(cardColor, 0f);
        summonRing.color = WithAlpha(cardColor, 0f);
        rayImage.color = WithAlpha(cardColor, 0f);
    }

    private IEnumerator PlayRoutine(GachaReward reward)
    {
        overlay.gameObject.SetActive(true);
        overlay.SetAsLastSibling();
        ResetVisuals();

        float duration = reward.isTopReward || reward.rarity >= 5 ? 3.15f : 2.55f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float charge = Smooth01(Mathf.InverseLerp(0f, 0.42f, t));
            float burst = Smooth01(Mathf.InverseLerp(0.36f, 0.68f, t));
            float reveal = Smooth01(Mathf.InverseLerp(0.56f, 0.86f, t));
            float exit = Smooth01(Mathf.InverseLerp(0.88f, 1f, t));

            background.color = new Color(0.02f, 0.018f, 0.03f, Mathf.Lerp(0f, 0.84f, Mathf.Min(charge, 1f - exit * 0.42f)));
            canvasGroup.alpha = 1f - exit;

            AnimateRings(elapsed, charge, burst, exit);
            AnimateCard(reveal, burst, exit);
            AnimateLabels(reveal, exit);
            AnimateGlyphs(elapsed, charge, burst, exit);
            AnimateSparks(elapsed, charge, burst, reveal, exit);
            yield return null;
        }

        overlay.gameObject.SetActive(false);
        revealRoutine = null;
        Action callback = finishedCallback;
        finishedCallback = null;
        callback?.Invoke();
    }

    private void ResetVisuals()
    {
        canvasGroup.alpha = 1f;
        background.color = new Color(0.02f, 0.018f, 0.03f, 0f);
        SetGraphicAlpha(rayImage, 0f);
        SetGraphicAlpha(auraRing, 0f);
        SetGraphicAlpha(summonRing, 0f);
        SetGraphicAlpha(cardFrame, 0f);
        SetGraphicAlpha(rewardIcon, 0f);
        SetGraphicAlpha(titleLabel, 0f);
        SetGraphicAlpha(detailLabel, 0f);
        SetGraphicAlpha(rarityLabel, 0f);
    }

    private void AnimateRings(float elapsed, float charge, float burst, float exit)
    {
        float pulse = 0.5f + Mathf.Sin(elapsed * 13.5f) * 0.5f;
        auraRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.74f, 1.18f, charge);
        summonRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.42f, 1.08f + pulse * 0.035f, burst);
        rayImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.45f, 1.1f, burst);
        auraRing.rectTransform.localRotation = Quaternion.Euler(0f, 0f, elapsed * -38f);
        summonRing.rectTransform.localRotation = Quaternion.Euler(0f, 0f, elapsed * 72f);
        rayImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, elapsed * 18f);

        SetGraphicAlpha(auraRing, (0.18f + pulse * 0.16f) * charge * (1f - exit));
        SetGraphicAlpha(summonRing, Mathf.Lerp(0f, 0.92f, burst) * (1f - exit));
        SetGraphicAlpha(rayImage, Mathf.Lerp(0f, 0.38f, burst) * (1f - exit));
    }

    private void AnimateCard(float reveal, float burst, float exit)
    {
        RectTransform card = cardFrame.rectTransform;
        float pop = Mathf.Sin(Mathf.Clamp01(reveal) * Mathf.PI);
        card.localScale = new Vector3(Mathf.Lerp(0.18f, 1f, reveal), Mathf.Lerp(0.96f, 1f + pop * 0.08f, reveal), 1f);
        card.anchoredPosition = new Vector2(0f, Mathf.Lerp(-42f, -12f, reveal));
        card.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-8f, 0f, reveal));

        float alpha = Mathf.Clamp01(reveal * 1.5f) * (1f - exit);
        SetGraphicAlpha(cardFrame, alpha);
        SetGraphicAlpha(rewardIcon, alpha);
    }

    private void AnimateLabels(float reveal, float exit)
    {
        float alpha = Smooth01(Mathf.InverseLerp(0.15f, 0.9f, reveal)) * (1f - exit);
        SetGraphicAlpha(titleLabel, alpha);
        SetGraphicAlpha(detailLabel, alpha);
        SetGraphicAlpha(rarityLabel, alpha);
        titleLabel.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, alpha);
    }

    private void AnimateGlyphs(float elapsed, float charge, float burst, float exit)
    {
        for (int i = 0; i < chessGlyphs.Count; i++)
        {
            RectTransform glyph = chessGlyphs[i];
            float angle = elapsed * (42f + i * 6f) + i * 60f;
            float radius = Mathf.Lerp(88f, 306f, burst);
            Vector2 position = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
            glyph.anchoredPosition = position;
            glyph.localRotation = Quaternion.Euler(0f, 0f, -angle + elapsed * 80f);
            glyph.localScale = Vector3.one * Mathf.Lerp(0.62f, 1f, charge);
            Image image = glyph.GetComponent<Image>();
            SetGraphicAlpha(image, Mathf.Lerp(0f, 0.76f, charge) * (1f - exit));
        }
    }

    private void AnimateSparks(float elapsed, float charge, float burst, float reveal, float exit)
    {
        for (int i = 0; i < sparks.Count; i++)
            sparks[i].Animate(elapsed, charge, burst, reveal, exit);
    }

    private Image AddImage(RectTransform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        RectTransform rect = CreateRect(parent, name, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    private TextMeshProUGUI AddText(RectTransform parent, string name, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        RectTransform rect = CreateRect(parent, name, position, size);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(RectTransform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private void LoadSprites()
    {
        LoadSpriteToCache("GachaRevealRing.png");
        LoadSpriteToCache("GachaRevealSpark.png");
        LoadSpriteToCache("GachaRevealRay.png");
        LoadSpriteToCache("GachaRevealCardFrame.png");
        for (int i = 1; i <= 6; i++)
            LoadSpriteToCache($"GachaRevealGlyph{i}.png");
    }

    private void LoadSpriteToCache(string fileName)
    {
        sprites[fileName] = LoadSprite(fileName);
    }

    private Sprite GetSprite(string fileName)
    {
        if (sprites.TryGetValue(fileName, out Sprite sprite))
            return sprite;
        sprite = LoadSprite(fileName);
        sprites[fileName] = sprite;
        return sprite;
    }

    private Sprite LoadSprite(string fileName)
    {
        string path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), AssetFolder, fileName));
        if (!File.Exists(path))
            return null;

        byte[] bytes = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
        {
            Destroy(texture);
            return null;
        }

        texture.name = Path.GetFileNameWithoutExtension(fileName);
        texture.filterMode = FilterMode.Bilinear;
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
        runtimeSprites.Add(sprite);
        return sprite;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < runtimeSprites.Count; i++)
            if (runtimeSprites[i])
                Destroy(runtimeSprites[i].texture);
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static void SetGraphicAlpha(Graphic graphic, float alpha)
    {
        if (!graphic)
            return;

        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    private readonly struct RevealSpark
    {
        private readonly RectTransform rect;
        private readonly Image image;
        private readonly float offset;
        private readonly float speed;
        private readonly float radius;
        private readonly float size;

        public RevealSpark(RectTransform rect, Image image, int index)
        {
            this.rect = rect;
            this.image = image;
            offset = index * 47.7f;
            speed = 56f + (index % 7) * 14f;
            radius = 118f + (index % 11) * 22f;
            size = 10f + (index % 5) * 5f;
        }

        public void Animate(float elapsed, float charge, float burst, float reveal, float exit)
        {
            float orbit = elapsed * speed + offset;
            float drift = Mathf.Sin(elapsed * 2.7f + offset) * 38f;
            float activeRadius = Mathf.Lerp(32f, radius + drift, Mathf.Max(charge, burst));
            rect.anchoredPosition = new Vector2(Mathf.Cos(orbit * Mathf.Deg2Rad), Mathf.Sin(orbit * Mathf.Deg2Rad)) * activeRadius;
            rect.sizeDelta = Vector2.one * Mathf.Lerp(size * 0.45f, size, burst);
            rect.localRotation = Quaternion.Euler(0f, 0f, orbit * 1.8f);
            float twinkle = 0.45f + Mathf.Sin(elapsed * 12f + offset) * 0.55f;
            float alpha = (0.18f + twinkle * 0.72f) * Mathf.Max(charge, reveal) * (1f - exit);
            SetGraphicAlpha(image, alpha);
        }
    }
}
