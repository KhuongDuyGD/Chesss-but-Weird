using ChessButWeird.Settings;
using System.Globalization;

int checks=0;
void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
var registry=SettingsRegistry.Create();
var store=new MemoryStore();
var settings=new SettingsManager(registry,store);
Check(registry.Select(d=>d.Id).Distinct().Count()==registry.Count,"Duplicate IDs");
Check(registry.Select(d=>d.Category).Distinct().Count()==6,"Missing tabs");
Check(registry.All(d=>!d.Name.Contains("Board")),"Board customization leaked into Settings");
foreach(var definition in registry)
{
    Check(settings.Get(definition.Id)==definition.Default,"Missing settings must use defaults: "+definition.Id);
    Check(definition.Normalize(float.NaN)==definition.Default,"NaN should fall back: "+definition.Id);
    Check(definition.Normalize(float.PositiveInfinity)==definition.Default,"Infinity should fall back: "+definition.Id);
}
settings.Set("music",47);settings.Set("aa",5);settings.Set("shadows",3);settings.Set("ui_scale",150);settings.Flush();
var reloaded=new SettingsManager(registry,store);
Check(reloaded.Get("music")==47&&reloaded.Get("aa")==5&&reloaded.Get("shadows")==3&&reloaded.Get("ui_scale")==150,"Saved configuration round trip");
settings.Set("ui_scale",900);Check(settings.Get("ui_scale")==150,"Clamp upper range");
settings.Set("ui_scale",-10);Check(settings.Get("ui_scale")==80,"Clamp lower range");
settings.Set("tooltip_delay",.127f);Check(Math.Abs(settings.Get("tooltip_delay")-.15f)<.001f,"Slider step normalization");
settings.Set("aa",999);Check(settings.Get("aa")==0,"Unsupported AA rejected");
store.Write("music","corrupt");store.Write("sfx","NaN");store.Write("shadows","-99");
reloaded=new SettingsManager(registry,store);
Check(reloaded.Get("music")==80&&reloaded.Get("sfx")==100&&reloaded.Get("shadows")==0,"Corrupt config defaults");
CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("vi-VN");
settings.Set("animation_speed",1.25f);reloaded=new SettingsManager(registry,store);
Check(reloaded.Get("animation_speed")==1.25f,"Locale independent persistence");
settings.Set("music",32);settings.Set("aa",4);
int events=0;settings.Changed+=ids=>events++;
settings.ResetCategory(SettingsCategory.Audio);
Check(settings.Get("music")==80&&settings.Get("aa")==4,"Tab reset touched another category");
Check(events==1,"Tab reset must emit one batch");
settings.Reset("aa");Check(!settings.IsModified("aa"),"Individual reset");
settings.Set("effects",2);settings.Set("shake",65);settings.Set("flash",85);
var presentation=new PresentationPreferences(settings);
settings.Set("visual_chaos",1);
Check(presentation.Effects==0&&presentation.Shake==0&&presentation.Flash==0&&presentation.ReducedMotion,"Chaos effective override");
settings.Set("visual_chaos",0);
Check(presentation.Effects==2&&presentation.Shake==.65f&&presentation.Flash==.85f&&!presentation.ReducedMotion,"Chaos destroyed individual preferences");
settings.Set("disable_shake",1);Check(presentation.Shake==0&&settings.Get("shake")==65,"Shake override overwrote preference");
settings.Set("disable_shake",0);Check(presentation.Shake==.65f,"Shake restore");
settings.Set("reduce_flashing",1);Check(presentation.Flash==0,"Reduced flashing");
settings.Set("animation_speed",0);Check(presentation.AnimationDuration(5)==0,"Instant presentation");
settings.Set("animation_speed",.5f);Check(presentation.AnimationDuration(1)==2,"Slow cosmetic presentation");
settings.Set("buff_detail",2);Check(presentation.TooltipDetail==TooltipDetailLevel.Nerd,"Nerd tooltip preference");
settings.Set("advanced_tooltips",0);Check(presentation.TooltipDetail==TooltipDetailLevel.Simple&&settings.Get("buff_detail")==2,"Advanced override must retain detail");
var orbitDefault=settings.Get("key_orbit");
Check(!settings.TryRebind("key_orbit",settings.Get("key_settings"),out var conflict)&&conflict=="Open Settings"&&settings.Get("key_orbit")==orbitDefault,"Duplicate binding silently replaced");
Check(settings.TryRebind("key_orbit",Array.IndexOf(SettingsRegistry.BindingKeys,"Q"),out _),"Valid key binding rejected");
Check(!settings.TryRebind("key_orbit",999,out _),"Unknown key binding accepted");
settings.TryRebind("key_settings",Array.IndexOf(SettingsRegistry.BindingKeys,"Space"),out _);
settings.TryRebind("key_orbit",Array.IndexOf(SettingsRegistry.BindingKeys,"F10"),out _);
settings.Reset("key_settings");Check(settings.Get("key_settings")==Array.IndexOf(SettingsRegistry.BindingKeys,"Space"),"Individual key reset introduced a duplicate");
var duplicateStore=new MemoryStore();duplicateStore.Write("key_orbit","2");duplicateStore.Write("key_settings","2");
var duplicateLoaded=new SettingsManager(registry,duplicateStore);Check(duplicateLoaded.Get("key_orbit")!=duplicateLoaded.Get("key_settings"),"Corrupt duplicate saved bindings");
var customStore=new MemoryStore();customStore.Write("fps","75");
Check(new SettingsManager(SettingsRegistry.Create(75),customStore).Get("fps")==75,"Legacy custom FPS lost");
settings.ResetAll();Check(registry.All(d=>settings.Get(d.Id)==d.Default),"Reset All incomplete");
var applied=new List<DisplayConfiguration>();var saved=new List<DisplayConfiguration>();
var display=new DisplayConfirmation(applied.Add,saved.Add);
var before=new DisplayConfiguration(1920,1080,1);var next=new DisplayConfiguration(1280,720,3);
display.Begin(before,next,100);Check(display.Pending&&saved.Count==0&&applied.Last().Width==1280,"Unconfirmed display persisted");
display.Tick(114.99);Check(display.Pending,"Early timeout");display.Tick(115);
Check(!display.Pending&&saved.Count==0&&applied.Last().Width==1920,"Timeout did not revert");
display.Begin(before,next,200);display.Keep();display.Tick(300);
Check(!display.Pending&&saved.Count==1&&saved[0].Width==1280&&applied.Last().Width==1280,"Keep/timeout behavior");
display.Begin(before,next,400);display.Revert();display.Revert();Check(applied.Last().Width==1920&&saved.Count==1,"Close/focus rollback must be idempotent");
display.Begin(before,next,500);display.Begin(next,new DisplayConfiguration(1600,900,3),501);display.Revert();Check(applied.Last().Width==1920,"Nested display preview lost original rollback");
Console.WriteLine($"PASS {checks} settings assertions: defaults, range validation, persistence, locale, resets, reversible overrides, binding conflicts and display rollback.");

sealed class MemoryStore : ISettingsStore
{
    private readonly Dictionary<string,string> values=new();
    public string Read(string id)=>values.GetValueOrDefault(id);
    public void Write(string id,string value)=>values[id]=value;
    public void Flush(){}
}
