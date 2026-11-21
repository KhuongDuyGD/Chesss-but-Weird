using TMPro;

public static class CommunityTypography
{
    public static void Apply(TextMeshProUGUI label)
    {
        if (!label) return;
        label.font = ChessFontCatalog.TmpFont;
        label.richText = false;
    }
}
