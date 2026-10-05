using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class AramBuffRuntime
{
    private void EnterRifle(PieceTeam team)
    {
        var king=game.AramKing(team);if(!king||completedTurns[Side(team)]<rifleReady[Side(team)])return;
        game.ClearSelection();rifleTeam=team;previousCamera=game.AramCamera;
        if(!previousCamera){Say("Gameplay camera unavailable; rifle shot retained.");return;}
        var go=new GameObject("One Man Army First Person",typeof(Camera));rifleCamera=go.GetComponent<Camera>();
        rifleCamera.CopyFrom(previousCamera);rifleCamera.tag="Untagged";rifleCamera.nearClipPlane=.03f;
        rifleCamera.rect=new Rect(0,0,1,1);
        float tile=Vector3.Distance(game.AramTile(Vector2Int.zero),game.AramTile(Vector2Int.right));
        rifleCamera.transform.position=king.transform.position+Vector3.up*tile*.95f;
        rifleYaw=Home(team)==0?0:180;riflePitch=18;shotDeadline=Time.unscaledTime+15;
        rifleCamera.transform.rotation=Quaternion.Euler(riflePitch,rifleYaw,0);
        previousCamera.enabled=false;
        previousCursorLock=Cursor.lockState;previousCursorVisible=Cursor.visible;
        rifleMouseLocked=true;rifleCursorSuspended=false;ApplyRifleCursor();
        rifleModel=GameObject.CreatePrimitive(PrimitiveType.Cube);rifleModel.name="King Rifle";Destroy(rifleModel.GetComponent<Collider>());
        rifleModel.transform.SetParent(rifleCamera.transform,false);rifleModel.transform.localPosition=new Vector3(.18f,-.16f,.4f);
        rifleModel.transform.localScale=new Vector3(.07f,.08f,.45f);
        Say("Move mouse to aim; left-click fires at the crosshair. ALT locks/unlocks the mouse. Escape exits and saves your shot.");
    }

    private void TickRifle()
    {
        if(!rifleCamera)return;
        if(Time.unscaledTime>=shotDeadline){ExitRifle(true);return;}
        if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){ExitRifle(false);return;}
        if(Keyboard.current!=null&&(Keyboard.current.leftAltKey.wasPressedThisFrame||Keyboard.current.rightAltKey.wasPressedThisFrame))
        {rifleMouseLocked=!rifleMouseLocked;ApplyRifleCursor();return;}
        if(Cursor.lockState!=CursorLockMode.Locked||rifleCursorSuspended)return;
        if(Mouse.current==null)return;
        { var delta=Mouse.current.delta.ReadValue();rifleYaw+=delta.x*.18f;riflePitch=Mathf.Clamp(riflePitch-delta.y*.18f,-80,80); }
        rifleCamera.transform.rotation=Quaternion.Euler(riflePitch,rifleYaw,0);
        // Locked FPS input fires through the centre crosshair, not the UI pointer.
        if(!Mouse.current.leftButton.wasPressedThisFrame)return;
        var ray=rifleCamera.ViewportPointToRay(new Vector3(.5f,.5f,0));
        if(Physics.Raycast(ray,out var hit,500f))
        {
            var victim=hit.collider.GetComponentInParent<ChessPiece>();
            if(victim&&victim.Team!=rifleTeam&&victim.Type!=PieceType.King&&victim.Type!=PieceType.Queen)
            {game.AramRemove(victim);Say("Rifle hit.");}
            else Say("Shot ended: invalid target.");
        }
        else Say("Shot missed.");
        ExitRifle(true);game.AramRefreshPosition();
    }

    private void ExitRifle(bool consume)
    {
        if(!rifleCamera)return;
        if(consume)rifleReady[Side(rifleTeam)]=completedTurns[Side(rifleTeam)]+3;
        if(previousCamera)previousCamera.enabled=true;
        Destroy(rifleCamera.gameObject);rifleCamera=null;rifleModel=null;
        Cursor.lockState=previousCursorLock;Cursor.visible=previousCursorVisible;
        rifleMouseLocked=false;rifleCursorSuspended=false;
    }

    private void ApplyRifleCursor()
    {Cursor.lockState=rifleMouseLocked&&!rifleCursorSuspended?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=Cursor.lockState!=CursorLockMode.Locked;}
    private void SetRifleCursorSuspended(bool suspended)
    {
        if(rifleCursorSuspended==suspended)return;
        rifleCursorSuspended=suspended;ApplyRifleCursor();
    }
}
