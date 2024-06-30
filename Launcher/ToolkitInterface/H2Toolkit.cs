﻿using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static ToolkitLauncher.ToolkitProfiles;

namespace ToolkitLauncher.ToolkitInterface
{
    public class H2Toolkit : ToolkitBase
    {

        public H2Toolkit(ProfileSettingsLauncher profile, string baseDirectory, Dictionary<ToolType, string> toolPaths) : base(profile, baseDirectory, toolPaths) { }

        protected virtual string sapienWindowClass
        {
            get => "Sapien";
        }

        override public async Task ImportBitmaps(string path, string type, string compression, bool debug_plate)
        {
            string bitmaps_command = "bitmaps";
            if (Profile.CommunityTools || Profile.IsMCC)
            {
                bitmaps_command = "bitmaps-with-type";
                await RunTool(ToolType.Tool, new List<string>() { bitmaps_command, path, type });
            }
            else
            {
                await RunTool(ToolType.Tool, new List<string>() { bitmaps_command, path });
            }
        }

        override public async Task ImportUnicodeStrings(string path)
        {
            await RunTool(ToolType.Tool, new List<string>() { "new-strings", path });
        }

        override public async Task ImportStructure(StructureType structure_command, string data_file, bool phantom_fix, bool release, bool useFast, bool autoFBX, ImportArgs import_args)
        {
            bool is_ass_file = data_file.ToLowerInvariant().EndsWith("ass");
            string command = is_ass_file ? "structure-new-from-ass" : "structure-from-jms";
            string use_release = release ? "yes" : "no";
            await RunTool(ToolType.Tool, new List<string>() { command, data_file, use_release });
        }

        public override async Task BuildCache(string scenario, CacheType cacheType, ResourceMapUsage resourceUsage, bool logTags, string cachePlatform, bool cacheCompress, bool cacheResourceSharing, bool cacheMultilingualSounds, bool cacheRemasteredSupport, bool cacheMPTagSharing)
        {
            await RunTool(ToolType.Tool, new List<string>() { "build-cache-file", scenario.Replace(".scenario", "") });
        }

        private static string GetLightmapQuality(LightmapArgs lightmapArgs)
        {
            return lightmapArgs.level_combobox.ToLower();
        }

        public override async Task BuildLightmap(string scenario, string bsp, LightmapArgs args, ICancellableProgress<int>? progress)
        {
            LogFolder = $"lightmaps_{Path.GetFileNameWithoutExtension(scenario)}";
            try
            {
                string quality = GetLightmapQuality(args);

                if (args.instanceCount > 1 && (Profile.IsMCC || Profile.CommunityTools)) // multi instance?
                {
                    if (progress is not null)
                        progress.MaxValue += 1 + args.instanceCount;

                    async Task RunInstance(int index)
                    {
                        if (index == 0 && !Profile.IsH2Codez()) // not needed for H2Codez
                        {
                            if (progress is not null)
                                progress.Status = "Delaying launch of zeroth instance";
                            await Task.Delay(1000 * 70, progress.GetCancellationToken());
                        }
                        Utility.Process.Result result = await RunLightmapWorker(
                            scenario,
                            bsp,
                            quality,
                            args.instanceCount,
                            index,
                            args.NoAssert,
                            progress.GetCancellationToken(),
                            args.outputSetting
                            );
                        if (result is not null && result.HasErrorOccured)
                            progress.Cancel($"Tool worker {index} has failed - exit code {result.ReturnCode}");
                        if (progress is not null)
                            progress.Report(1);
                    }

                    var instances = new List<Task>();
                    for (int i = args.instanceCount - 1; i >= 0; i--)
                    {
                        instances.Add(RunInstance(i));
                    }
                    if (progress is not null)
                        progress.Status = $"Running {args.instanceCount} instances";
                    await Task.WhenAll(instances);
                    if (progress is not null)
                        progress.Status = "Merging output";

                    if (progress.IsCancelled)
                        return;

                    await RunMergeLightmap(scenario, bsp, args.instanceCount, args.NoAssert);
                    if (progress is not null)
                        progress.Report(1);
                }
                else
                {
                    Debug.Assert(args.instanceCount == 1); // should be one, otherwise we got bad args
                    if (progress is not null)
                    {
                        progress.DisableCancellation();
                        progress.MaxValue += 1;
                    }
                    await RunTool((args.NoAssert && Profile.IsMCC) ? ToolType.ToolFast : ToolType.Tool, new() { "lightmaps", scenario, bsp, quality });
                    if (progress is not null)
                        progress.Report(1);
                }
            } finally
            {
                LogFolder = null;
            }
        }

        private async Task RunMergeLightmap(string scenario, string bsp, int workerCount, bool useFast)
        {

            if (Profile.IsMCC)
            {
                await RunTool(useFast ? ToolType.ToolFast : ToolType.Tool, new List<string>()
                {
                    "lightmaps-farm-merge",
                    scenario,
                    bsp,
                    workerCount.ToString(),
                });
            }
            else // todo: Remove this code
            {
                await RunTool(ToolType.Tool, new List<string>()
                {
                    "lightmaps-master", // beware legacy code
                    scenario,
                    bsp,
                    "super",
                    workerCount.ToString(),
                });
            }
        }

        private async Task<Utility.Process.Result> RunLightmapWorker(string scenario, string bsp, string quality, int workerCount, int index, bool useFast, CancellationToken cancelationToken, OutputMode output)
        {
            this.LogFileSuffix = $"-{index}";
            if (Profile.IsMCC)
            {
                bool wereWeExperts = Profile.ElevatedToExpert;
                Profile.ElevatedToExpert = true;
                try
                {
                    return await RunTool(useFast ? ToolType.ToolFast : ToolType.Tool, new List<string>()
                    {
                        "lightmaps-farm-worker",
                        scenario,
                        bsp,
                        quality,
                        index.ToString(),
                        workerCount.ToString()
                    }, outputMode: output, lowPriority: index == 0, cancellationToken: cancelationToken);
                } finally
                {
                    Profile.ElevatedToExpert = wereWeExperts;
                }
            }
            else // todo: Remove this code
            {
                List<string> args = new List<string>()
                {
                    "lightmaps-slave", // the long legacy of h2codez
                    scenario,
                    bsp,
                    quality,
                    workerCount.ToString(),
                    index.ToString()
                };
                return await RunTool(ToolType.Tool, args, outputMode: output, cancellationToken: cancelationToken);
            }
        }

        /// <summary>
        /// Import a model
        /// </summary>
        /// <param name="path"></param>
        /// <param name="importType"></param>
        /// <returns></returns>
        public override async Task ImportModel(string path, ModelCompile importType, bool phantomFix, bool h2SelectionLogic, bool renderPRT, bool FPAnim, string characterFPPath, string weaponFPPath, bool accurateRender, bool verboseAnim, bool uncompressedAnim, bool skyRender, bool PDARender, bool resetCompression, bool autoFBX, bool genShaders)
        {
            if (importType.HasFlag(ModelCompile.render))
                await RunTool(ToolType.Tool, new List<string>() { "model-render", path });
            if (importType.HasFlag(ModelCompile.collision))
                await RunTool(ToolType.Tool, new List<string>() { "model-collision", path });
            if (importType.HasFlag(ModelCompile.physics))
                await RunTool(ToolType.Tool, new List<string>() { "model-physics", path });
            if (importType.HasFlag(ModelCompile.animations))
                await RunTool(ToolType.Tool, new List<string>() { "append-animations", path });
        }

        public override async Task ImportSound(string path, string platform, string bitrate, string ltf_path, string sound_command, string class_type, string compression_type, string custom_extension)
        {
            await RunTool(ToolType.Tool, new List<string>() { "import-lipsync", path, ltf_path });
        }

        public override bool IsMutexLocked(ToolType tool)
        {
            // todo(num0005) implement this
            return false;
        }

        public override string GetDocumentationName()
        {
            return "H2V";
        }
    }
}
