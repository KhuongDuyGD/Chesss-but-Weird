using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class AramBuffRuntime
{
    private void EnterRifle(PieceTeam team)
    {
        var king=game.AramKing(team);if(!king||completedTurns[Side(team)]<rifleReady[Side(team)])return;
        game.ClearSelection();rifleTeam=team;previousCamera=game.AramCamera;
        var go=new GameObject("One Man Army First Person",typeof(Camera));rifleCamera=go.GetComponent<Camera>();
        rifleCamera.CopyFrom(previousCamera);rifleCamera.tag="Untagged";rifleCamera.nearClipPlane=.03f;
        float tile=Vector3.Distance(game.AramTile(Vector2Int.zero),game.AramTile(Vector2Int.right));
        rifleCamera.transform.position=king.transform.position+Vector3.up*tile*.95f;
        rifleYaw=Home(team)==0?0:180;riflePitch=18;shotDeadline=Time.unscaledTime+15;
        previousCamera.enabled=false;
        previousCursorLock=Cursor.lockState;previousCursorVisible=Cursor.visible;
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        rifleModel=GameObject.CreatePrimitive(PrimitiveType.Cube);rifleModel.name="King Rifle";Destroy(rifleModel.GetComponent<Collider>());
        rifleModel.transform.SetParent(rifleCamera.transform,false);rifleModel.transform.localPosition=new Vector3(.18f,-.16f,.4f);
        rifleModel.transform.localScale=new Vector3(.07f,.08f,.45f);
        Say("Hold right mouse and move to aim. Left-click fires at the crosshair. Exit rifle saves your shot.");
    }

    private void TickRifle()
    {
        if(!rifleCamera)return;
        if(Time.unscaledTime>=shotDeadline){ExitRifle(true);return;}
        if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){ExitRifle(false);return;}
        if(Mouse.current==null)return;
        if(Mouse.current.rightButton.isPressed)
        { var delta=Mouse.current.delta.ReadValue();rifleYaw+=delta.x*.18f;riflePitch=Mathf.Clamp(riflePitch-delta.y*.18f,-80,80); }
        rifleCamera.transform.rotation=Quaternion.Euler(riflePitch,rifleYaw,0);
        if(!Mouse.current.leftButton.wasPressedThisFrame || (UnityEngine.EventSystems.EventSystem.current && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))return;
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
    }
}
