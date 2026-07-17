using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GachaSummonRevealDemo : MonoBehaviour
{
    [SerializeField] private RectTransform revealRoot;
    [SerializeField] private Sprite previewRewardIcon;

    private GachaSummonRevealController revealController;

    private void Awake()
    {
        if (!revealRoot)
            revealRoot = transform as RectTransform;

        revealController = revealRoot.GetComponent<GachaSummonRevealController>();
        if (!revealController)
            revealController = revealRoot.gameObject.AddComponent<GachaSummonRevealController>();

        revealController.Initialize(revealRoot);
    }

    private void Start()
    {
        PlayPreview();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            PlayPreview();
    }

    public void PlayPreview()
    {
        if (!revealController || revealController.IsPlaying)
            return;

        GachaReward reward = new GachaReward(GachaRewardType.Diamond, 1200, 5, true);
        revealController.Play(reward, previewRewardIcon, null);
    }

    public static void BuildPreviewHud(RectTransform parent, GachaSummonRevealDemo demo)
    {
        GameObject labelObject = new GameObject("Preview Hint", typeof(RectTransform));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(parent, false);
        labelRect.anchorMin = new Vector2(0.5f, 0f);
        labelRect.anchorMax = new Vector2(0.5f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, 34f);
        labelRect.sizeDelta = new Vector2(720f, 48f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 24f;
        label.color = new Color(1f, 0.96f, 0.76f, 0.84f);
        label.text = "Click or press Space to replay the chess gacha reveal";
        label.raycastTarget = false;

        GameObject buttonObject = new GameObject("Replay Button", typeof(RectTransform));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(parent, false);
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 88f);
        buttonRect.sizeDelta = new Vector2(220f, 58f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(1f, 0.88f, 0.36f, 0.86f);
        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(demo.PlayPreview);

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(buttonRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 26f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.13f, 0.08f, 0.02f, 1f);
        text.text = "Replay";
    }
}
