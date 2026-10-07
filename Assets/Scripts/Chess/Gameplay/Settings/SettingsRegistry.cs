using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessButWeird.Settings
{
    public static class SettingsRegistry
    {
        // Portable key names; Unity's Key enum is resolved only in the input adapter.
        public static readonly string[] BindingKeys = Enumerable.Range('A', 26).Select(i => ((char)i).ToString())
            .Concat(Enumerable.Range(1, 12).Select(i => "F" + i)).Concat(new[] { "Space", "Tab", "Backquote" }).ToArray();
        public static IReadOnlyList<SettingDefinition> Create(float? retainedFrameRate = null)
        {
            var list = new List<SettingDefinition>();
            Action<string,SettingsCategory,string,string,bool> toggle = (id,tab,name,detail,value) =>
                list.Add(new SettingDefinition(id,tab,name,detail,SettingType.Toggle,value?1:0));
            Action<string,SettingsCategory,string,string,float,float,float,float> slider = (id,tab,name,detail,value,min,max,step) =>
                list.Add(new SettingDefinition(id,tab,name,detail,SettingType.Slider,value,min,max,step));
            Action<string,SettingsCategory,string,string,float,string[],float[]> choice = (id,tab,name,detail,value,labels,values) =>
                list.Add(new SettingDefinition(id,tab,name,detail,SettingType.Dropdown,value,labels:labels,values:values));
            var g = SettingsCategory.General;
            choice("language",g,"Language","English is available. Vietnamese is still in development; missing translations use English.",0,SettingsLanguages.All.Select(l=>l.Label).ToArray(),SettingsLanguages.All.Select(l=>(float)l.Id).ToArray());
            slider("ui_scale",g,"UI Scale","Scales this menu and gameplay information. Long lists remain scrollable.",100,80,150,5);
            choice("tooltip_size",g,"Tooltip Size","Size of piece names and buff information.",1,new[]{"Small","Medium","Large"},null);
            slider("tooltip_delay",g,"Tooltip Delay","Wait before showing hover information, in seconds.",.25f,0,1,.05f);
            toggle("advanced_tooltips",g,"Show Advanced Tooltips","Allow the buff detail preference below to show additional mechanics.",true);
            toggle("confirm_quit",g,"Confirm Before Quit","Confirm before leaving the current match.",true);
            toggle("pause_unfocused",g,"Pause When Unfocused","Opens pause for local games only. Online clocks keep running.",true);
            var p = SettingsCategory.Gameplay;
            toggle("legal_moves",p,"Highlight Legal Moves","Show move hints for the selected piece. Online hints are advisory.",true);
            toggle("selected_piece",p,"Highlight Selected Piece","Lift the selected model slightly; does not affect selection.",true);
            toggle("ability_targets",p,"Show Ability Targets","Show target-square hints. Ability instructions and validation stay active.",true);
            choice("buff_detail",p,"Buff Tooltip Detail","Simple: essential text. Detailed: complete catalog mechanics. Nerd: also available public progress.",1,new[]{"Simple","Detailed","Nerd"},null);
            toggle("buff_source",p,"Show Buff Source","Include the owning side in buff hover information.",true);
            toggle("trigger_preview",p,"Show Trigger Progress","Include known public counters and readiness in Nerd buff details.",true);
            toggle("upgrade_rarity",p,"Show Buff Rarity","Include the catalog rarity label in buff hover information.",true);
            var v = SettingsCategory.Graphics;
            choice("graphics",v,"Graphics Preset","Texture filtering, mipmaps, reflections and level of detail. Explicit AA and shadows remain independent.",0,new[]{"Auto (recommended)","Low","Medium","High"},null);
            choice("aa",v,"Anti-Aliasing","Auto uses the existing hardware recommendation. MSAA falls back to SMAA on unsupported hardware.",0,new[]{"Auto","Off","FXAA","SMAA","MSAA 2x","MSAA 4x"},null);
            choice("shadows",v,"Shadow Quality","Off removes shadows. Higher tiers draw sharper shadows over a larger distance.",0,new[]{"Off","Low","Medium","High"},null);
            choice("effects",v,"Effects Quality","Controls summon spark count, glyphs, rays and halos. Core feedback remains readable.",2,new[]{"Low","Medium","High"},null);
            choice("animation_quality",v,"Animation Behavior","Flat removes the cosmetic move arc. Smooth keeps it. Match timing stays unchanged.",1,new[]{"Flat","Smooth"},null);
            choice("animation_speed",v,"Animation Speed","Piece movement presentation only; simulation, clocks, bot thinking and turn timing stay unchanged.",1,new[]{"0.5x","0.75x","1.0x","1.25x","1.5x","2.0x","3.0x","Instant"},new[]{.5f,.75f,1,1.25f,1.5f,2,3,0});
            slider("shake",v,"Screen Shake","Intensity of the summon reward reveal shake. Does not move the gameplay camera.",35,0,100,5);
            slider("flash",v,"Flash Intensity","Brightness of the summon reveal flash; accessibility overrides may suppress it.",100,0,100,5);
            toggle("vsync",v,"VSync","Synchronize to the display refresh rate. Overrides the frame rate limit while enabled.",false);
            choice("fps",v,"Frame Rate Limit","Auto uses the hardware recommendation. Unlimited may increase power usage.",0,new[]{"Auto","30 FPS","60 FPS","90 FPS","120 FPS","144 FPS","165 FPS","240 FPS","Unlimited"},new float[]{0,30,60,90,120,144,165,240,-1});
            var fps = list.Last();
            if (retainedFrameRate.HasValue && retainedFrameRate.Value >= 30 && retainedFrameRate.Value <= 240 && !fps.Values.Contains(retainedFrameRate.Value))
                list[list.Count-1] = new SettingDefinition(fps.Id,fps.Category,fps.Name,fps.Description,SettingType.Dropdown,0,
                    labels:fps.Labels.Concat(new[]{retainedFrameRate.Value+" FPS (saved)"}).ToArray(),values:fps.Values.Concat(new[]{retainedFrameRate.Value}).ToArray());
            var a = SettingsCategory.Audio;
            slider("master",a,"Master Volume","Overall volume of the game.",100,0,100,1);
            slider("music",a,"Music Volume","Menu, match and result music.",80,0,100,1);
            slider("sfx",a,"SFX Volume","Piece selection, moves, captures, alerts and match result sounds.",100,0,100,1);
            toggle("move_sounds",a,"Move Sounds","Play movement and castling audio.",true);
            toggle("capture_sounds",a,"Capture Sounds","Play capture audio.",true);
            toggle("mute_unfocused",a,"Mute When Unfocused","Mute game audio when another application has focus.",false);
            var c = SettingsCategory.Controls;
            slider("orbit_sensitivity",c,"Orbit Sensitivity","Mouse sensitivity for unlocked camera orbit; rifle aiming stays unchanged.",100,25,200,5);
            toggle("right_orbit",c,"Right Mouse Orbit","Hold right mouse and drag to rotate the unlocked camera.",true);
            toggle("middle_orbit",c,"Middle Mouse Orbit","Hold middle mouse and drag to rotate the unlocked camera.",true);
            toggle("wheel_zoom",c,"Mouse Wheel Zoom","Use the wheel to zoom the board camera. Menus still scroll normally.",true);
            list.Add(new SettingDefinition("key_orbit",c,"Toggle Camera Lock","Press to unlock orbit or restore the assigned-side view.",SettingType.Keybind,Array.IndexOf(BindingKeys,"Y"),labels:BindingKeys));
            list.Add(new SettingDefinition("key_settings",c,"Open Settings","Opens Settings from a match. Escape remains the universal back key.",SettingType.Keybind,Array.IndexOf(BindingKeys,"F10"),labels:BindingKeys));
            var x = SettingsCategory.Accessibility;
            toggle("large_text",x,"Large Text","Enlarge Settings text and gameplay hover information. UI Scale and Tooltip Size are in General.",false);
            toggle("contrast_ui",x,"High Contrast UI","Stronger text contrast in Settings and buff tooltips.",false);
            choice("colorblind",x,"Color Vision Support","Distinct move-hint colors. Text labels remain available; does not recolor your equipped cosmetics.",0,new[]{"Off","Protanopia","Deuteranopia","Tritanopia"},null);
            toggle("reduce_motion",x,"Reduce Motion","Remove piece arcs, hover wobble and rotating summon decorations.",false);
            toggle("reduce_flashing",x,"Reduce Flashing","Suppress the bright full-screen summon reveal flash.",false);
            toggle("disable_shake",x,"Disable Screen Shake","Overrides intensity without replacing your saved Screen Shake value.",false);
            toggle("visual_chaos",x,"Reduce Visual Chaos","Temporarily use low effects, flat motion, no shake and no flash. Individual preferences are preserved.",false);
            return list.AsReadOnly();
        }
    }
}
