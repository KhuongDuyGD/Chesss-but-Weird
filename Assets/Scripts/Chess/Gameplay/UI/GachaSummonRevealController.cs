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
    private Image topTierHalo;
    private Image topTierFlash;
    private Image cardFrame;
    private Image rewardIcon;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI detailLabel;
    private TextMeshProUGUI rarityLabel;
    private TextMeshProUGUI sequenceLabel;
    private TextMeshProUGUI skipButtonLabel;
    private Button skipButton;
    private Coroutine revealRoutine;
    private Action finishedCallback;
    private RevealPalette currentPalette = RevealPalette.Standard;
    private bool currentTopTier;
    private bool skipRequested;

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
        PlaySequence(new List<GachaReward> { reward }, value => icon, onFinished);
    }

    public void PlaySequence(List<GachaReward> rewards, Func<GachaReward, Sprite> iconResolver, Action onFinished)
    {
        finishedCallback = onFinished;
        if (!overlay)
        {
            onFinished?.Invoke();
            return;
        }

        if (rewards == null || rewards.Count == 0)
        {
            onFinished?.Invoke();
            return;
        }

        if (revealRoutine != null)
            StopCoroutine(revealRoutine);

        skipRequested = false;
        revealRoutine = StartCoroutine(PlaySequenceRoutine(new List<GachaReward>(rewards), iconResolver));
    }

    public void RequestSkip()
    {
        skipRequested = true;
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

        topTierFlash = AddImage(overlay, "Top Tier Flash", null, Vector2.zero, new Vector2(DesignWidth, DesignHeight));
        topTierFlash.preserveAspect = false;
        topTierFlash.color = new Color(1f, 0.94f, 0.64f, 0f);

        rayImage = AddImage(overlay, "Crown Ray Burst", GetSprite("GachaRevealRay.png"), Vector2.zero, new Vector2(1120f, 1120f));
        rayImage.color = new Color(1f, 0.84f, 0.33f, 0f);

        topTierHalo = AddImage(overlay, "Top Tier Halo", GetSprite("GachaRevealRing.png"), Vector2.zero, new Vector2(760f, 760f));
        topTierHalo.color = new Color(1f, 0.48f, 0.95f, 0f);

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
        sequenceLabel = AddText(overlay, "Reveal Sequence", new Vector2(0f, 252f), new Vector2(360f, 42f), 28f, new Color(1f, 0.96f, 0.76f, 0f));
        titleLabel = AddText(overlay, "Reveal Title", new Vector2(0f, 318f), new Vector2(820f, 74f), 54f, new Color(1f, 0.94f, 0.62f, 0f));
        titleLabel.text = "SUMMON";
        BuildSkipButton();
    }

    private void BuildSkipButton()
    {
        RectTransform buttonRect = CreateRect(overlay, "Skip Reveal Button", new Vector2(684f, 362f), new Vector2(142f, 54f));
        Image image = buttonRect.gameObject.AddComponent<Image>();
        image.color = new Color(0.05f, 0.045f, 0.07f, 0.72f);
        image.raycastTarget = true;

        Outline outline = buttonRect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.86f, 0.35f, 0.62f);
        outline.effectDistance = new Vector2(2f, -2f);

        skipButton = buttonRect.gameObject.AddComponent<Button>();
        skipButton.transition = Selectable.Transition.None;
        skipButton.targetGraphic = image;
        skipButton.onClick.AddListener(RequestSkip);
        skipButtonLabel = AddText(buttonRect, "Skip Text", Vector2.zero, new Vector2(130f, 42f), 24f, new Color(1f, 0.96f, 0.76f, 1f));
        skipButtonLabel.text = "SKIP";
    }

    private void ConfigureReward(GachaReward reward, Sprite icon, int index, int total)
    {
        currentTopTier = reward.isTopReward || reward.rarity >= 5;
        currentPalette = RevealPalette.ForReward(reward);
        rewardIcon.sprite = icon;
        rewardIcon.enabled = icon;
        rarityLabel.text = new string('*', Mathf.Clamp(reward.rarity, 1, 5));
        detailLabel.text = reward.AmountText;
        sequenceLabel.text = total > 1 ? $"{index + 1}/{total}" : string.Empty;
        titleLabel.text = currentTopTier ? "TOP TIER SUMMON" : reward.rarity >= 4 ? "RARE SUMMON" : "SUMMON";

        cardFrame.color = WithAlpha(currentPalette.card, 0f);
        summonRing.color = WithAlpha(currentPalette.card, 0f);
        auraRing.color = WithAlpha(currentPalette.aura, 0f);
        rayImage.color = WithAlpha(currentPalette.ray, 0f);
        topTierHalo.color = WithAlpha(currentPalette.halo, 0f);
        topTierFlash.color = WithAlpha(currentPalette.flash, 0f);
        titleLabel.color = WithAlpha(currentPalette.text, 0f);
        sequenceLabel.color = WithAlpha(currentPalette.text, 0f);
    }

    private IEnumerator PlaySequenceRoutine(List<GachaReward> rewards, Func<GachaReward, Sprite> iconResolver)
    {
        overlay.gameObject.SetActive(true);
        overlay.SetAsLastSibling();

        for (int i = 0; i < rewards.Count; i++)
        {
            if (skipRequested)
                break;

            GachaReward reward = rewards[i];
            Sprite icon = iconResolver != null ? iconResolver(reward) : null;
            ConfigureReward(reward, icon, i, rewards.Count);
            ResetVisuals(i > 0);
            yield return PlayRewardRoutine(reward, i, rewards.Count);

            if (!skipRequested && i < rewards.Count - 1)
                yield return HoldBetweenRewards(0.12f);
        }

        overlay.gameObject.SetActive(false);
        revealRoutine = null;
        skipRequested = false;
        Action callback = finishedCallback;
        finishedCallback = null;
        callback?.Invoke();
    }

    private IEnumerator PlayRewardRoutine(GachaReward reward, int index, int total)
    {
        bool finalReward = index >= total - 1;
        bool topTier = reward.isTopReward || reward.rarity >= 5;
        float duration = topTier ? (total > 1 ? 2.25f : 2.85f) : total > 1 ? (reward.rarity >= 4 ? 1.12f : 0.92f) : 2.35f;
        float elapsed = 0f;
        float startBackgroundAlpha = index > 0 ? (topTier ? 0.88f : 0.82f) : 0f;
        float targetBackgroundAlpha = topTier ? 0.92f : 0.84f;
        while (elapsed < duration)
        {
            if (skipRequested || Input.GetKeyDown(KeyCode.Escape))
                break;

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float charge = Smooth01(Mathf.InverseLerp(0f, 0.42f, t));
            float burst = Smooth01(Mathf.InverseLerp(0.36f, 0.68f, t));
            float reveal = Smooth01(Mathf.InverseLerp(0.56f, 0.86f, t));
            float exit = Smooth01(Mathf.InverseLerp(0.88f, 1f, t));
            float overlayExit = finalReward ? exit : 0f;
            float itemExit = exit;

            background.color = new Color(0.02f, 0.018f, 0.03f, Mathf.Lerp(startBackgroundAlpha, targetBackgroundAlpha, charge) * (1f - overlayExit * 0.42f));
            canvasGroup.alpha = finalReward ? 1f - overlayExit : 1f;

            AnimateRings(elapsed, charge, burst, itemExit);
            AnimateTopTier(elapsed, charge, burst, reveal, itemExit);
            AnimateCard(reveal, burst, itemExit);
            AnimateLabels(reveal, itemExit);
            AnimateGlyphs(elapsed, charge, burst, itemExit);
            AnimateSparks(elapsed, charge, burst, reveal, itemExit);
            yield return null;
        }
    }

    private IEnumerator HoldBetweenRewards(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (skipRequested || Input.GetKeyDown(KeyCode.Escape))
                break;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void ResetVisuals(bool keepBackground)
    {
        canvasGroup.alpha = 1f;
        background.color = new Color(0.02f, 0.018f, 0.03f, keepBackground ? 0.82f : 0f);
        SetGraphicAlpha(topTierFlash, 0f);
        SetGraphicAlpha(rayImage, 0f);
        SetGraphicAlpha(topTierHalo, 0f);
        SetGraphicAlpha(auraRing, 0f);
        SetGraphicAlpha(summonRing, 0f);
        SetGraphicAlpha(cardFrame, 0f);
        SetGraphicAlpha(rewardIcon, 0f);
        SetGraphicAlpha(titleLabel, 0f);
        SetGraphicAlpha(sequenceLabel, 0f);
        SetGraphicAlpha(detailLabel, 0f);
        SetGraphicAlpha(rarityLabel, 0f);
        RectTransform card = cardFrame.rectTransform;
        card.localScale = Vector3.one;
        card.localRotation = Quaternion.identity;
    }

    private void AnimateRings(float elapsed, float charge, float burst, float exit)
    {
        float topBoost = currentTopTier ? 1.22f : 1f;
        float pulse = 0.5f + Mathf.Sin(elapsed * (currentTopTier ? 18.5f : 13.5f)) * 0.5f;
        auraRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.74f, 1.18f * topBoost, charge);
        summonRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.42f, 1.08f * topBoost + pulse * 0.04f, burst);
        rayImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.45f, currentTopTier ? 1.34f : 1.1f, burst);
        auraRing.rectTransform.localRotation = Quaternion.Euler(0f, 0f, elapsed * (currentTopTier ? -62f : -38f));
        summonRing.rectTransform.localRotation = Quaternion.Euler(0f, 0f, elapsed * (currentTopTier ? 112f : 72f));
        rayImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, elapsed * (currentTopTier ? 32f : 18f));

        SetGraphicColorAlpha(auraRing, currentPalette.aura, (0.2f + pulse * 0.2f) * charge * (1f - exit));
        SetGraphicColorAlpha(summonRing, currentPalette.card, Mathf.Lerp(0f, currentTopTier ? 1f : 0.92f, burst) * (1f - exit));
        SetGraphicColorAlpha(rayImage, currentPalette.ray, Mathf.Lerp(0f, currentTopTier ? 0.64f : 0.38f, burst) * (1f - exit));
    }

    private void AnimateTopTier(float elapsed, float charge, float burst, float reveal, float exit)
    {
        if (!currentTopTier)
        {
            SetGraphicAlpha(topTierHalo, 0f);
            SetGraphicAlpha(topTierFlash, 0f);
            return;
        }

        float shimmer = 0.5f + Mathf.Sin(elapsed * 24f) * 0.5f;
        topTierHalo.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.34f, 1.18f + shimmer * 0.08f, burst);
        topTierHalo.rectTransform.localRotation = Quaternion.Euler(0f, 0f, elapsed * -148f);
        SetGraphicColorAlpha(topTierHalo, currentPalette.halo, Mathf.Lerp(0f, 0.72f, burst) * (1f - exit));

        float flash = Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(0.12f, 0.92f, burst)) * Mathf.PI);
        float revealGlint = Mathf.Sin(Mathf.Clamp01(reveal) * Mathf.PI) * 0.18f;
        SetGraphicColorAlpha(topTierFlash, currentPalette.flash, (flash * 0.34f + revealGlint) * (1f - exit));
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
        SetGraphicAlpha(sequenceLabel, Mathf.Min(alpha, 0.84f));
        SetGraphicAlpha(detailLabel, alpha);
        SetGraphicAlpha(rarityLabel, alpha);
        titleLabel.rectTransform.localScale = Vector3.one * Mathf.Lerp(currentTopTier ? 0.78f : 0.9f, currentTopTier ? 1.08f : 1f, alpha);
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
            glyph.localScale = Vector3.one * Mathf.Lerp(0.62f, currentTopTier ? 1.16f : 1f, charge);
            Image image = glyph.GetComponent<Image>();
            SetGraphicColorAlpha(image, currentPalette.spark, Mathf.Lerp(0f, currentTopTier ? 0.92f : 0.76f, charge) * (1f - exit));
        }
    }

    private void AnimateSparks(float elapsed, float charge, float burst, float reveal, float exit)
    {
        for (int i = 0; i < sparks.Count; i++)
            sparks[i].Animate(elapsed, charge, burst, reveal, exit, currentPalette.spark, currentTopTier ? 1.45f : 1f);
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

    private static void SetGraphicColorAlpha(Graphic graphic, Color baseColor, float alpha)
    {
        if (!graphic)
            return;

        baseColor.a = alpha;
        graphic.color = baseColor;
    }

    private readonly struct RevealPalette
    {
        public static readonly RevealPalette Standard = new RevealPalette(
            new Color(1f, 0.97f, 0.78f, 1f),
            new Color(0.36f, 0.78f, 1f, 1f),
            new Color(1f, 0.84f, 0.33f, 1f),
            new Color(1f, 0.96f, 0.7f, 1f),
            new Color(1f, 0.94f, 0.62f, 1f),
            new Color(1f, 0.48f, 0.95f, 1f),
            new Color(1f, 0.94f, 0.64f, 1f));

        public readonly Color card;
        public readonly Color aura;
        public readonly Color ray;
        public readonly Color spark;
        public readonly Color text;
        public readonly Color halo;
        public readonly Color flash;

        private RevealPalette(Color card, Color aura, Color ray, Color spark, Color text, Color halo, Color flash)
        {
            this.card = card;
            this.aura = aura;
            this.ray = ray;
            this.spark = spark;
            this.text = text;
            this.halo = halo;
            this.flash = flash;
        }

        public static RevealPalette ForReward(GachaReward reward)
        {
            if (reward.isTopReward || reward.rarity >= 5)
            {
                return new RevealPalette(
                    new Color(1f, 0.78f, 0.08f, 1f),
                    new Color(0.35f, 1f, 0.94f, 1f),
                    new Color(1f, 0.37f, 0.94f, 1f),
                    new Color(1f, 0.95f, 0.52f, 1f),
                    new Color(1f, 0.91f, 0.28f, 1f),
                    new Color(0.95f, 0.22f, 1f, 1f),
                    new Color(1f, 0.84f, 0.34f, 1f));
            }

            if (reward.rarity >= 4)
            {
                return new RevealPalette(
                    new Color(0.42f, 0.83f, 1f, 1f),
                    new Color(0.42f, 0.98f, 1f, 1f),
                    new Color(0.38f, 0.58f, 1f, 1f),
                    new Color(0.7f, 0.96f, 1f, 1f),
                    new Color(0.72f, 0.94f, 1f, 1f),
                    new Color(0.36f, 0.78f, 1f, 1f),
                    new Color(0.58f, 0.86f, 1f, 1f));
            }

            return Standard;
        }
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

        public void Animate(float elapsed, float charge, float burst, float reveal, float exit, Color color, float intensity)
        {
            float orbit = elapsed * speed * intensity + offset;
            float drift = Mathf.Sin(elapsed * 2.7f + offset) * 38f;
            float activeRadius = Mathf.Lerp(32f, radius * intensity + drift, Mathf.Max(charge, burst));
            rect.anchoredPosition = new Vector2(Mathf.Cos(orbit * Mathf.Deg2Rad), Mathf.Sin(orbit * Mathf.Deg2Rad)) * activeRadius;
            rect.sizeDelta = Vector2.one * Mathf.Lerp(size * 0.45f, size * intensity, burst);
            rect.localRotation = Quaternion.Euler(0f, 0f, orbit * 1.8f);
            float twinkle = 0.45f + Mathf.Sin(elapsed * 12f + offset) * 0.55f;
            float alpha = (0.18f + twinkle * 0.72f) * Mathf.Max(charge, reveal) * (1f - exit);
            SetGraphicColorAlpha(image, color, Mathf.Clamp01(alpha * intensity));
        }
    }
}
