# Shared fixture wiring: copy production presentation unchanged; use cached packages only.
function Add-BuffVfxFixture($sourceRoot,$auditRoot,$dependencies) {
    Copy-Item -LiteralPath "$sourceRoot/Assets/Scripts/Chess/Presentation/BuffVfx" -Destination "$auditRoot/Assets/Fixture" -Recurse
    Copy-Item -LiteralPath "$sourceRoot/Assets/Resources/BuffVfx" -Destination "$auditRoot/Assets/Resources" -Recurse
    foreach($package in @('com.unity.nuget.newtonsoft-json','com.unity.render-pipelines.universal','com.unity.render-pipelines.core','com.unity.render-pipelines.universal-config','com.unity.shadergraph','com.unity.burst','com.unity.collections','com.unity.mathematics','com.unity.searcher')) {
        $cached=Get-ChildItem -LiteralPath "$sourceRoot/Library/PackageCache" -Directory | Where-Object {$_.Name.StartsWith($package+'@')} | Select-Object -First 1
        if(!$cached){throw "Import $package in the main project first."}
        $dependencies[$package]='file:'+$cached.FullName.Replace('\','/')
    }
    foreach($module in @('particlesystem','terrain','terrainphysics','animation','xr','physics')){$dependencies['com.unity.modules.'+$module]='1.0.0'}
}
