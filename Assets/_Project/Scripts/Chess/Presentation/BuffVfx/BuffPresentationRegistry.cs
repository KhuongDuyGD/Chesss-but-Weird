using System;
using UnityEngine;

namespace ChessButWeird.BuffVfx
{
    [Serializable] public sealed class BuffVisualRecipe
    {
        public int id, group;
        public string key, glyph, prop;
        public bool implemented;
        public Color color;
        public float duration = 1.1f;
    }
    [Serializable] internal sealed class BuffVisualCatalog { public BuffVisualRecipe[] recipes; }
    public sealed class BuffPresentationRegistry
    {
        private readonly BuffVisualRecipe[] recipes = new BuffVisualRecipe[35];
        public int Count { get; private set; }
        public BuffPresentationRegistry(string json)
        {
            var source = JsonUtility.FromJson<BuffVisualCatalog>(json);
            if (source?.recipes == null || source.recipes.Length != 35) throw new FormatException("Expected all 35 stable buff IDs.");
            foreach (var recipe in source.recipes)
            {
                if (recipe.id < 0 || recipe.id >= 35 || recipes[recipe.id] != null ||
                    !Enum.TryParse(recipe.key, out AramBuffId id) || (int)id != recipe.id || recipe.group < 4 || recipe.group > 8)
                    throw new FormatException("Invalid buff visual recipe: " + recipe.key);
                recipes[recipe.id] = recipe; Count++;
            }
        }
        public BuffVisualRecipe Find(int id) => id >= 0 && id < recipes.Length ? recipes[id] : null;
        public static BuffPresentationRegistry Load()
        {
            var asset = Resources.Load<TextAsset>("BuffVfx/registry");
            if (!asset) throw new InvalidOperationException("Buff VFX registry missing from build.");
            return new BuffPresentationRegistry(asset.text);
        }
    }
    public enum BuffVisualPhase { Preview, Trigger, Resolve, Status, Remove }

    // Boundary object: adapters supply confirmed, viewer-safe facts; renderers have no gameplay reference.
    public sealed class BuffVisualEvent
    {
        public string Id;
        public int BuffId, ActorId;
        public PieceTeam Team;
        public BuffVisualPhase Phase = BuffVisualPhase.Resolve;
        public bool ViewerAuthorized;
        public Vector3 Source, Target;
        public Quaternion BoardRotation=Quaternion.identity;
        public Vector3[] Tiles;
        public Transform Actor;
        public string Outcome = "Resolved";
        public int? Amount;
        public BuffVisualChange[] Changes;
    }
    public struct BuffVisualChange
    {
        public int ActorId;public PieceTeam Team;public Vector3 From,To;public string FromKind,ToKind;public Transform Actor;
    }
    public struct BuffHudState
    {
        public int BuffId,Current,Target,Mask;public string Text,Unit;public PieceTeam Team;
    }
    public struct BuffPreviewPiece
    {
        public Transform Actor;public Vector3 Position;public PieceTeam Team;
        public BuffPreviewPiece(Transform actor,Vector3 position,PieceTeam team){Actor=actor;Position=position;Team=team;}
    }
    public struct BuffVisualStatus
    {
        public int BuffId, ActorId, Remaining, Percent;
        public PieceTeam Team;
        public Transform Actor;
        public BuffVisualStatus(int buff, int actorId, Transform actor, PieceTeam team, int remaining = -1, int percent = -1)
        { BuffId = buff; ActorId = actorId; Actor = actor; Team = team; Remaining = remaining; Percent = percent; }
        public long Key => ((long)ActorId << 6) | (uint)BuffId;
    }
    public struct BuffBoardVisual
    {
        public int BuffId,Cell,Remaining;
        public string Kind;
        public PieceTeam Team;
        public Vector3 Position;
        public Quaternion Rotation;
        public int Key=>(BuffId<<7)|(Cell<<1)|(Kind=="Warning"?1:0);
        public BuffBoardVisual(int buff,int cell,Vector3 position,string kind,PieceTeam team,int remaining=-1,Quaternion? rotation=null)
        {BuffId=buff;Cell=cell;Position=position;Kind=kind;Team=team;Remaining=remaining;Rotation=rotation??Quaternion.identity;}
    }
}
