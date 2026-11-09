using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessButWeird.BuffVfx
{
    [DisallowMultipleComponent]
    public sealed class BuffVfxOrchestrator : MonoBehaviour
    {
        public const int TransientBudget = 12, StatusBudget = 64;
        private readonly List<BuffEffectInstance> active = new List<BuffEffectInstance>(TransientBudget);
        private readonly Dictionary<int, Stack<BuffEffectInstance>> pool = new Dictionary<int, Stack<BuffEffectInstance>>();
        private readonly Dictionary<long, PieceStatusVisual> statuses = new Dictionary<long, PieceStatusVisual>();
        private readonly Stack<PieceStatusVisual> statusPool = new Stack<PieceStatusVisual>();
        private readonly List<PieceStatusVisual> retiring = new List<PieceStatusVisual>(StatusBudget);
        private readonly List<long> removals = new List<long>(StatusBudget);
        private readonly HashSet<long> desired = new HashSet<long>();
        private readonly List<BuffVisualStatus> orderedStatuses=new List<BuffVisualStatus>(StatusBudget);
        private readonly HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> seenOrder = new Queue<string>();
        private readonly Dictionary<int, int> stacks = new Dictionary<int, int>();
        private readonly Dictionary<int,int> stackTotals=new Dictionary<int,int>();
        private BuffVisualAssets assets;
        private BuffPresentationRegistry registry;
        private int created;
        private int statusCreated;
        private CameraCueCoordinator cameraCue;
        private BuffBoardLayer boardLayer;
        private BuffTacticalHud hud;
        private BuffInteractionPreview preview;
        public int PreviewTargets=>preview?.Targets??0;
        public int PreviewGhosts=>preview?.Ghosts??0;
        public int HudRows=>hud?.Rows??0;
        public float TileSize { get; private set; }
        private bool paused;
        public bool Paused { get=>paused; set {paused=value;hud?.Visible(!value);if(value){preview?.Clear();if(cameraCue)cameraCue.Cancel();}} }
        public float PlaybackSpeed { get; set; } = 1;
        public int ActiveCount => active.Count;
        public int ActiveStatuses => statuses.Count;
        public int ActiveBoardProps=>boardLayer?.ActiveCount??0;
        public int CreatedInstances => created;
        public int DroppedEvents { get; private set; }
        public BuffPresentationRegistry Registry => registry;
        public bool Ready => assets != null;
        public void Initialize(float tileSize)
        {
            if (assets != null) return;
            TileSize = Mathf.Max(.001f, tileSize);
            try
            {
                registry = BuffPresentationRegistry.Load(); assets = new BuffVisualAssets();
                cameraCue=gameObject.AddComponent<CameraCueCoordinator>();
                boardLayer=new BuffBoardLayer(transform,assets,TileSize);
                preview=new BuffInteractionPreview(transform,assets,TileSize);hud=new BuffTacticalHud(transform);
                for(int id=0;id<35;id++)
                {
                    if(registry.Find(id)?.implemented!=true)continue;
                    pool[id] = new Stack<BuffEffectInstance>();
                    pool[id].Push(Create(registry.Find(id)));
                }
            }
            catch(Exception ex) { Debug.LogWarning("[Buff VFX] Presentation unavailable: "+ex.Message); assets?.Dispose();assets=null;enabled=false; }
        }
        public GameObject CreateRifleView(Transform camera)
        {
            if(assets==null||!camera)return null;
            var view=assets.Prop("KingRifle",camera);if(!view)return null;
            view.transform.localPosition=new Vector3(.18f,-.2f,.5f)*TileSize;
            view.transform.localRotation=Quaternion.identity;view.transform.localScale=Vector3.one*TileSize*.65f;
            return view;
        }
        private BuffEffectInstance Create(BuffVisualRecipe recipe)
        {
            var instance = new BuffEffectInstance(transform, recipe, assets); created++;
            return instance;
        }
        private static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
        public bool Play(BuffVisualEvent cue)
        {
            if (assets == null || cue == null || !cue.ViewerAuthorized || !Finite(cue.Source) || !Finite(cue.Target) ||
                cue.Phase != BuffVisualPhase.Resolve && cue.Phase != BuffVisualPhase.Trigger && cue.Phase != BuffVisualPhase.Preview) return false;
            var recipe = registry.Find(cue.BuffId);
            if(cue.Changes!=null){if(cue.Changes.Length>64)return false;foreach(var change in cue.Changes)if(!Finite(change.From)||!Finite(change.To))return false;}
            if(cue.Tiles!=null){if(cue.Tiles.Length>(cue.BuffId==18?32:9))return false;foreach(var tile in cue.Tiles)if(!Finite(tile))return false;}
            if (recipe == null || !recipe.implemented || string.IsNullOrEmpty(cue.Id) || !seen.Add(cue.Id)) return false;
            seenOrder.Enqueue(cue.Id); if (seenOrder.Count > 256) seen.Remove(seenOrder.Dequeue());
            if (active.Count >= TransientBudget) { DroppedEvents++; return false; }
            if(!pool.TryGetValue(recipe.id,out var stack)){stack=new Stack<BuffEffectInstance>();pool[recipe.id]=stack;}
            // At most two live instances of each recipe; rapid queues stay bounded.
            int same = 0; foreach (var running in active) if (running.Recipe.id == recipe.id) same++;
            if (stack.Count == 0 && same >= 2) { DroppedEvents++; return false; }
            var effect = stack.Count > 0 ? stack.Pop() : Create(recipe);
            effect.Begin(cue, TileSize, UserSettings.Presentation.ReducedMotion, UserSettings.Presentation.Effects);
            boardLayer.Resolve(cue,UserSettings.Presentation.ReducedMotion?.32f:recipe.duration);
            if(recipe.id==4&&cue.Phase==BuffVisualPhase.Resolve)cameraCue.Pulse(TileSize*.012f);
            hud.Cue(cue);active.Add(effect); return true;
        }
        public void SyncStatuses(IReadOnlyList<BuffVisualStatus> snapshots)
        {
            if (assets == null) return;
            orderedStatuses.Clear();for(int i=0;i<snapshots.Count;i++)orderedStatuses.Add(snapshots[i]);
            orderedStatuses.Sort((a,b)=>StatusPriority(a.BuffId).CompareTo(StatusPriority(b.BuffId)));snapshots=orderedStatuses;
            desired.Clear(); stacks.Clear();stackTotals.Clear();
            for(int i=0;i<snapshots.Count;i++)
            {
                var state=snapshots[i];if(!state.Actor||!state.Actor.gameObject.activeInHierarchy||registry.Find(state.BuffId)?.implemented!=true||!desired.Add(state.Key))continue;
                stackTotals.TryGetValue(state.ActorId,out int count);stackTotals[state.ActorId]=count+1;
            }
            desired.Clear();
            for (int i = 0; i < snapshots.Count; i++)
            {
                var state = snapshots[i]; var recipe = registry.Find(state.BuffId);
                if (!state.Actor || !state.Actor.gameObject.activeInHierarchy || recipe == null || !recipe.implemented || !desired.Add(state.Key)) continue;
                if (!statuses.TryGetValue(state.Key, out var visual))
                {
                    if (statuses.Count >= StatusBudget) continue;
                    if(statusPool.Count==0&&statusCreated>=StatusBudget)continue;
                    if(statusPool.Count>0)visual=statusPool.Pop();else{visual=new PieceStatusVisual(transform,assets);statusCreated++;}
                    statuses.Add(state.Key, visual);
                }
                stacks.TryGetValue(state.ActorId, out int slot); stacks[state.ActorId] = slot + 1;
                visual.Apply(state, recipe, TileSize, slot,stackTotals[state.ActorId]);
            }
            removals.Clear();
            foreach (var pair in statuses) if (!desired.Contains(pair.Key)) removals.Add(pair.Key);
            foreach (long key in removals) { statuses[key].Expire(); retiring.Add(statuses[key]); statuses.Remove(key); }
        }
        private static int StatusPriority(int id)=>id==0?0:id==33?1:id==3?2:id==29?3:4;
        public void Advance(float seconds)
        {
            if (assets == null) return;
            foreach(var effect in active)effect.Playback(Paused,PlaybackSpeed*UserSettings.Presentation.AnimationSpeed);
            float step = Paused?0:UserSettings.Presentation.AnimationSpeed==0?10:Mathf.Clamp(seconds, 0, 1) * Mathf.Clamp(PlaybackSpeed, 0, 8) * UserSettings.Presentation.AnimationSpeed;
            boardLayer.Tick(step);
            hud.Tick(step);
            if(!Paused)for (int i = active.Count - 1; i >= 0; i--)
                if (!active[i].Tick(step)) { var finished = active[i]; active.RemoveAt(i); finished.Release(); pool[finished.Recipe.id].Push(finished); }
            removals.Clear();
            foreach (var pair in statuses) if (!pair.Value.Tick(step)) removals.Add(pair.Key);
            foreach (long key in removals) { statuses[key].Release(); statusPool.Push(statuses[key]); statuses.Remove(key); }
            for(int i=retiring.Count-1;i>=0;i--)if(!retiring[i].Tick(step)){retiring[i].Release();statusPool.Push(retiring[i]);retiring.RemoveAt(i);}
        }
        private void Update() => Advance(Time.unscaledDeltaTime);
        public void ClearVisuals(bool resetHistory = false)
        {
            if(cameraCue)cameraCue.Cancel();
            boardLayer?.Clear();
            hud?.Clear();preview?.Clear();
            foreach (var running in active) { running.Release(); pool[running.Recipe.id].Push(running); } active.Clear();
            foreach (var pair in statuses) { pair.Value.Release(); statusPool.Push(pair.Value); } statuses.Clear();
            foreach(var visual in retiring){visual.Release();statusPool.Push(visual);}retiring.Clear();
            if (resetHistory) { seen.Clear(); seenOrder.Clear(); }
        }
        private void OnDisable() => ClearVisuals();
        public void SyncBoard(IReadOnlyList<BuffBoardVisual> states)=>boardLayer?.Sync(states);
        public void SyncHud(IReadOnlyList<BuffHudState> states)=>hud?.Sync(states);
        public void FinishKingless()=>hud?.FinishKingless();
        public void SyncPreview(int buff,Vector3 source,IReadOnlyList<Vector3> allowed,Vector3? hovered=null,bool valid=true)=>preview?.Sync(buff,source,allowed,hovered,valid);
        public void SyncFormation(IReadOnlyList<BuffPreviewPiece> states)=>preview?.Formation(states);
        public void ClearPreview()=>preview?.Clear();
        private void OnDestroy() { ClearVisuals(); assets?.Dispose(); assets = null; }
    }
}
