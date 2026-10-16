using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UI = SketchbookUI;

public sealed class MatchFoundConfirmationView : MonoBehaviour
{
    private Button yes, no;
    private TextMeshProUGUI opponent, countdown, status;

    public void Build(UnityAction accept, UnityAction decline)
    {
        var root = (RectTransform)transform;
        var blocker = root.gameObject.AddComponent<Image>();
        blocker.color = new Color(.10f, .09f, .13f, .68f); blocker.raycastTarget = true;
        var panel = UI.Node(root, "Confirmation Panel", new Rect(0, 0, 960, 560));
        panel.gameObject.AddComponent<InventoryContentRootFitter>().Configure(960, 560, .68f);
        UI.Card(panel, "Paper", new Rect(0, 0, 960, 560), UI.Paper, 0, false);
        UI.Text(panel, "Found", "MATCH FOUND", new Rect(40, 25, 880, 42), 26, UI.Muted, TextAlignmentOptions.Center);
        UI.Text(panel, "Question", "Are you sure??", new Rect(40, 83, 880, 88), 60, UI.Ink, TextAlignmentOptions.Center);
        opponent = UI.Text(panel, "Opponent", "", new Rect(40, 183, 880, 52), 30, UI.Ink, TextAlignmentOptions.Center);
        countdown = UI.Text(panel, "Acceptance Countdown", "30", new Rect(40, 245, 880, 68), 48, UI.Ink, TextAlignmentOptions.Center);
        status = UI.Text(panel, "Acceptance Status", "", new Rect(40, 325, 880, 62), 26, UI.Muted, TextAlignmentOptions.Center, true);
        no = UI.Button(panel, "No", "NO", new Rect(70, 430, 370, 82), UI.Pink, () => { if (no.interactable) decline(); }, 32);
        yes = UI.Button(panel, "Yes", "YES", new Rect(520, 430, 370, 82), UI.Green, () => { if (yes.interactable) accept(); }, 32);
        gameObject.SetActive(false);
    }

    public void SetState(bool visible, string name, float seconds, bool accepted, bool busy, bool connected)
    {
        gameObject.SetActive(visible);
        if (!visible) { yes.interactable = no.interactable = false; return; }
        opponent.text = "Opponent: " + name;
        countdown.text = Mathf.CeilToInt(Mathf.Max(0, seconds)).ToString("00");
        status.text = accepted ? "Accepted! Waiting for your opponent..." : "Confirm before the countdown ends.";
        no.interactable = !busy && connected && seconds > 0;
        yes.interactable = !accepted && no.interactable;
    }
}
