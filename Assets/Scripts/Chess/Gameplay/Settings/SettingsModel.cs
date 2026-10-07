using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ChessButWeird.Settings
{
    public enum SettingsCategory { General, Gameplay, Graphics, Audio, Controls, Accessibility }
    public enum SettingType { Toggle, Slider, Dropdown, Keybind }
    public enum TooltipDetailLevel { Simple, Detailed, Nerd }

    public sealed class SettingDefinition
    {
        public string Id { get; }
        public SettingsCategory Category { get; }
        public string Name { get; }
        public string Description { get; }
        public SettingType Type { get; }
        public float Default { get; }
        public float Minimum { get; }
        public float Maximum { get; }
        public float Step { get; }
        public IReadOnlyList<string> Labels { get; }
        public IReadOnlyList<float> Values { get; }
        public SettingDefinition(string id, SettingsCategory category, string name, string description,
            SettingType type, float defaultValue, float minimum = 0, float maximum = 1, float step = 1,
            string[] labels = null, float[] values = null)
        {
            Id = id; Category = category; Name = name; Description = description; Type = type;
            Minimum = minimum; Maximum = maximum; Step = step;
            Labels = Array.AsReadOnly(labels ?? Array.Empty<string>());
            Values = Array.AsReadOnly(values ?? Enumerable.Range(0, Labels.Count).Select(i => (float)i).ToArray());
            Default = Normalize(defaultValue);
        }
        public float Normalize(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return Default;
            if (Type == SettingType.Dropdown || Type == SettingType.Keybind)
                return Values.Contains(value) ? value : Default;
            value = Math.Max(Minimum, Math.Min(Maximum, value));
            return Step > 0 ? (float)(Minimum + Math.Round((value - Minimum) / Step) * Step) : value;
        }
        public int OptionIndex(float value)
        { for (int i = 0; i < Values.Count; i++) if (Values[i] == value) return i; return 0; }
    }

    public interface ISettingsStore
    {
        string Read(string id);
        void Write(string id, string value);
        void Flush();
    }

    /// <summary>No Unity, account, match or inventory dependencies. One writer for user preferences.</summary>
    public sealed class SettingsManager
    {
        private readonly ISettingsStore store;
        private readonly Dictionary<string, SettingDefinition> definitions;
        private readonly Dictionary<string, float> values = new Dictionary<string, float>();
        public IReadOnlyList<SettingDefinition> Definitions { get; }
        public event Action<IReadOnlyList<string>> Changed;
        public SettingsManager(IEnumerable<SettingDefinition> registry, ISettingsStore storage)
        {
            store = storage;
            Definitions = Array.AsReadOnly(registry.ToArray());
            definitions = Definitions.ToDictionary(d => d.Id);
            foreach (var definition in Definitions)
            {
                float value;
                values[definition.Id] = float.TryParse(store.Read(definition.Id), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out value) ? definition.Normalize(value) : definition.Default;
                if (definition.Type == SettingType.Keybind)
                {
                    var occupied = Definitions.Where(d => d.Type == SettingType.Keybind && d.Id != definition.Id && values.ContainsKey(d.Id))
                        .Select(d => values[d.Id]).ToArray();
                    if (occupied.Contains(values[definition.Id]))
                        values[definition.Id] = !occupied.Contains(definition.Default) ? definition.Default : definition.Values.First(v => !occupied.Contains(v));
                }
            }
        }
        public float Get(string id) => values[id];
        public bool Enabled(string id) => Get(id) > .5f;
        public SettingDefinition Definition(string id) => definitions[id];
        public bool IsModified(string id) => Get(id) != definitions[id].Default;
        public void Set(string id, float value)
        {
            value = definitions[id].Normalize(value);
            if (definitions[id].Type == SettingType.Keybind && Definitions.Any(d => d.Type == SettingType.Keybind && d.Id != id && Get(d.Id) == value))
                throw new ArgumentException("This key is already assigned to another action.", nameof(value));
            if (!Write(id, value)) return;
            Changed?.Invoke(new[] { id });
        }
        private bool Write(string id, float value)
        {
            value = definitions[id].Normalize(value);
            if (values[id] == value) return false;
            values[id] = value;
            store.Write(id, value.ToString("R", CultureInfo.InvariantCulture));
            return true;
        }
        public void Reset(string id)
        {
            if (definitions[id].Type == SettingType.Keybind) TryRebind(id, definitions[id].Default, out _);
            else Set(id, definitions[id].Default);
        }
        public void ResetCategory(SettingsCategory category) => ResetMany(Definitions.Where(d => d.Category == category));
        public void ResetAll() => ResetMany(Definitions);
        private void ResetMany(IEnumerable<SettingDefinition> items)
        {
            var changed = new List<string>();
            foreach (var item in items) if (Write(item.Id, item.Default)) changed.Add(item.Id);
            if (changed.Count > 0) Changed?.Invoke(changed);
            Flush();
        }
        public void Flush() => store.Flush();
        public bool TryRebind(string id, float key, out string conflict)
        {
            conflict = null;
            if (definitions[id].Type != SettingType.Keybind || !definitions[id].Values.Contains(key)) return false;
            foreach (var definition in Definitions)
                if (definition.Type == SettingType.Keybind && definition.Id != id && Get(definition.Id) == key)
                { conflict = definition.Name; return false; }
            Set(id, key); return true;
        }
    }

    /// <summary>Effective presentation overrides never rewrite the player's underlying values.</summary>
    public sealed class PresentationPreferences
    {
        private readonly SettingsManager settings;
        public PresentationPreferences(SettingsManager source) { settings = source; }
        public bool ReducedMotion => settings.Enabled("reduce_motion") || settings.Enabled("visual_chaos");
        public float Flash => settings.Enabled("reduce_flashing") || settings.Enabled("visual_chaos") ? 0 : settings.Get("flash") / 100;
        public float Shake => settings.Enabled("disable_shake") || ReducedMotion ? 0 : settings.Get("shake") / 100;
        public int Effects => settings.Enabled("visual_chaos") ? 0 : (int)settings.Get("effects");
        public float AnimationSpeed => settings.Get("animation_speed");
        public float AnimationDuration(float duration) => AnimationSpeed == 0 ? 0 : duration / AnimationSpeed;
        public float TooltipScale => settings.Get("tooltip_size") == 0 ? .85f : settings.Get("tooltip_size") == 2 ? 1.2f : 1;
        public TooltipDetailLevel TooltipDetail => settings.Enabled("advanced_tooltips")
            ? (TooltipDetailLevel)(int)settings.Get("buff_detail") : TooltipDetailLevel.Simple;
    }

    public readonly struct DisplayConfiguration
    {
        public readonly int Width, Height, Mode;
        public DisplayConfiguration(int width, int height, int mode) { Width = width; Height = height; Mode = mode; }
    }
    /// <summary>Persist only confirmed display modes. Roll back on timeout, close, or loss of focus.</summary>
    public sealed class DisplayConfirmation
    {
        private readonly Action<DisplayConfiguration> apply, persist;
        private DisplayConfiguration previous, proposed;
        private double deadline;
        public bool Pending { get; private set; }
        public DisplayConfirmation(Action<DisplayConfiguration> applyDisplay, Action<DisplayConfiguration> save)
        { apply = applyDisplay; persist = save; }
        public void Begin(DisplayConfiguration current, DisplayConfiguration next, double now)
        { var rollback = Pending ? previous : current; Revert(); previous = rollback; proposed = next; deadline = now + 15; Pending = true; apply(next); }
        public int SecondsRemaining(double now) => Math.Max(0, (int)Math.Ceiling(deadline - now));
        public void Tick(double now) { if (Pending && now >= deadline) Revert(); }
        public void Keep() { if (!Pending) return; Pending = false; persist(proposed); }
        public void Revert() { if (!Pending) return; Pending = false; apply(previous); }
    }
}
