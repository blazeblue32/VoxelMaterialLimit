using System;
using HarmonyLib;
using VoxelMaterialLimit;
using VRage.Plugins;
// Define assembly version when compiled by Pulsar
#if !LOCAL_BUILD
using System.Reflection;

[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

#endif

namespace ClientPlugin
{
    // Pulsar entry point. Loads once at game start and applies the shader changes in Shared/.
    public class Plugin : IPlugin
    {
        readonly Harmony harmony = new Harmony("VoxelMaterialLimit");
        int overlayAttempts = 0;
        bool overlayDone;

        public void Init(object gameInstance)
        {
            ShaderLimitPatch.Apply(harmony);
            TryOverlay();
        }

        // The compatibility layer's assembly may load after this plugin, so look for it again for a short while.
        public void Update()
        {
            if (!overlayDone && overlayAttempts < 600) TryOverlay();
        }

        void TryOverlay()
        {
            overlayAttempts++;
            Type compiler = ShaderOverlay.FindCompiler();
            if (compiler == null) return;
            string shaders;
            try { shaders = ShaderLimitPatch.ShadersPath(); }
            catch (Exception) { return; } // paths not ready yet; try again on a later frame
            overlayDone = true;
            ShaderOverlay.Apply(harmony, compiler, shaders);
        }

        public void Dispose()
        {
            // Do not UnpatchAll here: it may break other plugins.
        }
    }
}
