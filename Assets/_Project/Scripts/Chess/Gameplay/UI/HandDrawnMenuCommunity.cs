using UnityEngine;
using UnityEngine.EventSystems;

public partial class HandDrawnMenuView
{
    private RectTransform communityScreen, communityReturnScreen;
    private CommunityMenuController communityController;
    private GameObject communityReturnFocus;
    private void BuildCommunityScreen()
    {
        communityScreen = CreateScreen("Community Mailbox and News");
        communityController = communityScreen.gameObject.AddComponent<CommunityMenuController>();
        communityController.Build(communityScreen, CloseCommunity, () => owner.LogoutToAuthentication());
    }
    private void ShowCommunity(bool mailbox)
    {
        if (currentScreen != communityScreen)
        {
            communityReturnScreen = currentScreen;
            communityReturnFocus = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        }
        TransitionToScreen(communityScreen, () => communityController.Open(mailbox));
    }
    private void CloseCommunity()
    {
        TransitionToScreen(communityReturnScreen ? communityReturnScreen : modeScreen, () =>
        {
            if (EventSystem.current && communityReturnFocus && communityReturnFocus.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(communityReturnFocus);
        });
    }
}
