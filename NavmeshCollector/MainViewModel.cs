using Mutagen.Bethesda;     
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using Noggog.WPF;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace NavmeshCollector
{
    public class MainViewModel : ReactiveObject
    {
        private Settings _settings;
        private string _outputText;
        private bool _isRunning;
        private readonly Dispatcher _dispatcher;
        private readonly SemaphoreSlim _runLock = new(1, 1);
        private const string LogFilePath = "NavmeshCollector.log";

        public Settings Settings
        {
            get => _settings;
            set => this.RaiseAndSetIfChanged(ref _settings, value);
        }

        public string OutputText
        {
            get => _outputText;
            set => this.RaiseAndSetIfChanged(ref _outputText, value);
        }

        public bool IsRunning
        {
            get => _isRunning;
            set => this.RaiseAndSetIfChanged(ref _isRunning, value);
        }

        public ILinkCache? LinkCache { get; private set; }

        public IEnumerable<Type> WorldspaceTypes { get; }

        public ReactiveCommand<Unit, Unit> RunCollectionCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveSettingsCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveLogCommand { get; }

        private FormKey? _selectedWorldspace;
        public FormKey? SelectedWorldspace
        {
            get => _selectedWorldspace;
            set => this.RaiseAndSetIfChanged(ref _selectedWorldspace, value);
        }

        private static readonly ModKey[] BethesdaPlugins =
        [
            Skyrim.ModKey,
            Update.ModKey,
            Dawnguard.ModKey,
            Dragonborn.ModKey,
            HearthFires.ModKey
        ];

        private static readonly HashSet<ModKey> CreationClubPlugins =
            GetCreationClubPlugins(GameEnvironment.Typical.Skyrim(SkyrimRelease.SkyrimSE).CreationClubListingsFilePath ?? string.Empty);

        private static readonly JsonSerializerOptions CachedJsonSerializerOptions = new() { WriteIndented = true };

        public MainViewModel()
        {
            // Capture dispatcher on UI thread
            var app = Application.Current;
            if (app?.Dispatcher == null)
                throw new InvalidOperationException("MainViewModel must be created on the UI thread with a valid Application.Current.Dispatcher.");
            
            _dispatcher = app.Dispatcher;
            
            _settings = LoadSettings();
            _outputText = "Ready to collect navmeshes...\n";
            _isRunning = false;

            // Initialize LinkCache for Mutagen WPF controls
            InitializeLinkCache();

            // Scope FormKeyMultiPicker to only show Worldspace records
            WorldspaceTypes = typeof(IWorldspaceGetter).AsEnumerable();

            // Ensure commands execute on UI thread to prevent cross-thread access violations
            RunCollectionCommand = ReactiveCommand.Create(RunCollection, outputScheduler: RxApp.MainThreadScheduler);
            SaveSettingsCommand = ReactiveCommand.Create(SaveSettings, outputScheduler: RxApp.MainThreadScheduler);
            SaveLogCommand = ReactiveCommand.Create(SaveLog, outputScheduler: RxApp.MainThreadScheduler);
            
            // Initialize log file
            InitializeLogFile();
        }

        private Settings LoadSettings()
        {
            try
            {
                var settingsPath = "settings.json";
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    var settings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                    
                    // Ensure all nested objects are initialized
                    settings.WorldspaceSelection ??= new WorldspaceSelection();
                    settings.CellSettings ??= new CellSettings();
                    settings.OverrideSettings ??= new OverrideSettings();
                    
                    return settings;
                }
            }
            catch (Exception ex)
            {
                AppendOutputSafe($"Error loading settings: {ex.Message}\n");
            }

            return new Settings();
        }

        private void SaveSettings()
        {
            try
            {
                var settingsPath = "settings.json";
                
                // Create a deep copy of settings on UI thread to avoid cross-thread access
                var settingsCopy = CreateSettingsCopy(Settings);
                
                var json = JsonSerializer.Serialize(settingsCopy, CachedJsonSerializerOptions);
                File.WriteAllText(settingsPath, json);
                AppendOutputSafe("Settings saved successfully.\n");
            }
            catch (Exception ex)
            {
                AppendOutputSafe($"Error saving settings: {ex.Message}\n");
            }
        }

        /// <summary>
        /// Thread-safe method to append output text. Always marshals to UI thread asynchronously.
        /// Also writes to log file.
        /// </summary>
        private void AppendOutputSafe(string text)
        {
            if (_dispatcher.CheckAccess())
            {
                // Already on UI thread
                OutputText += text;
            }
            else
            {
                // Marshal to UI thread asynchronously to avoid deadlocks and cross-thread access issues
                _dispatcher.InvokeAsync(() => OutputText += text, DispatcherPriority.Normal);
            }

            // Write to log file (thread-safe)
            WriteToLogFile(text);
        }

        /// <summary>
        /// Initialize log file with timestamp header.
        /// </summary>
        private void InitializeLogFile()
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var header = $"=== Navmesh Collector Log - {timestamp} ===\n";
                File.WriteAllText(LogFilePath, header);
            }
            catch (Exception ex)
            {
                // Can't use AppendOutputSafe here as it would cause recursion
                _dispatcher.InvokeAsync(() => OutputText += $"Warning: Could not initialize log file: {ex.Message}\n", DispatcherPriority.Normal);
            }
        }

        /// <summary>
        /// Initialize LinkCache for Mutagen WPF reflection controls.
        /// </summary>
        private void InitializeLinkCache()
        {
            try
            {
                var env = GameEnvironment.Typical.Skyrim(SkyrimRelease.SkyrimSE);
                LinkCache = env.LinkCache;
                this.RaisePropertyChanged(nameof(LinkCache));
            }
            catch (Exception ex)
            {
                AppendOutputSafe($"Warning: Could not initialize LinkCache: {ex.Message}\n");
                AppendOutputSafe("Worldspace selection picker may not function correctly.\n");
            }
        }

        /// <summary>
        /// Thread-safe method to append text to log file.
        /// </summary>
        private static void WriteToLogFile(string text)
        {
            try
            {
                File.AppendAllText(LogFilePath, text);
            }
            catch
            {
                // Silently fail to avoid recursion or UI disruption
            }
        }

        /// <summary>
        /// Manually save the current output to log file.
        /// </summary>
        private void SaveLog()
        {
            try
            {
                File.WriteAllText(LogFilePath, OutputText);
                AppendOutputSafe($"Log saved to: {Path.GetFullPath(LogFilePath)}\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving log file: {ex.Message}", "Save Log Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RunCollection()
        {
            // Use semaphore to prevent concurrent execution
            if (!await _runLock.WaitAsync(0))
                return;

            try
            {
                // Update UI state on UI thread
                IsRunning = true;
                OutputText = "Starting navmesh collection...\n";

                // Create deep copies of all settings on UI thread BEFORE Task.Run
                // This prevents any cross-thread access to reactive properties
                var worldspaceSelectionCopy = new WorldspaceSelectionSnapshot(Settings.WorldspaceSelection);
                var cellSettingsCopy = new CellSettingsSnapshot(Settings.CellSettings);
                var overrideSettingsCopy = new OverrideSettingsSnapshot(Settings.OverrideSettings);

                if (!cellSettingsCopy.InteriorCells && !cellSettingsCopy.ExteriorCells)
                {
                    AppendOutputSafe("Error: Either \"Include Interior Cells\" or \"Include Exterior Cells\" must be enabled in Settings\n");
                    return;
                }

                // Run collection on background thread with snapshot copies
                await Task.Run(() =>
                {
                    try
                    {
                        var output = new SkyrimMod(ModKey.FromFileName("NavmeshCollector.esp"), SkyrimRelease.SkyrimSE);
                        using var env = GameEnvironment.Typical.Builder<ISkyrimMod, ISkyrimModGetter>(GameRelease.SkyrimSE)
                            .WithOutputMod(output)
                            .Build();

                        var basePlugins = new HashSet<ModKey>(BethesdaPlugins.Concat(CreationClubPlugins));
                        var cache = env.LinkCache;

                        var candidateNavmeshes = env.LoadOrder.PriorityOrder
                            .OnlyEnabledAndExisting()
                            .NavigationMesh()
                            .WinningContextOverrides(cache);

                        int collectedCount = 0;

                        foreach (var navmesh in candidateNavmeshes)
                        {
                            if (ShouldCollectNavmesh(navmesh, worldspaceSelectionCopy, cellSettingsCopy, overrideSettingsCopy, basePlugins, cache, out var parentModKey))
                            {
                                AppendOutputSafe($"Collecting navmesh {navmesh.Record.FormKey} from override in {parentModKey}\n");
                                navmesh.GetOrAddAsOverride(output);
                                collectedCount++;
                            }
                        }

                        AppendOutputSafe($"\nCollection complete! Collected {collectedCount} navmeshes.\n");
                        
                        try
                        {
                            var outputPath = Path.Combine(env.DataFolderPath, "NavmeshCollector.esp");
                            output.WriteToBinary(outputPath);
                            AppendOutputSafe($"Output written to: {outputPath}\n");
                        }
                        catch (UnauthorizedAccessException)
                        {
                            AppendOutputSafe($"ERROR: No permission to write to {env.DataFolderPath}. Run as administrator or check folder permissions.\n");
                            throw;
                        }
                        catch (IOException ex)
                        {
                            AppendOutputSafe($"ERROR: Cannot write file - it may be in use by another program. {ex.Message}\n");
                            throw;
                        }
                    }
                    catch (Exception)
                    {
                        // Re-throw to be caught by outer catch block
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                AppendOutputSafe($"\nError during collection: {ex.Message}\n{ex.StackTrace}\n");
            }
            finally
            {
                // Update UI state on UI thread
                IsRunning = false;
                _runLock.Release();
            }
        }

        private void UpdateStatus(string message)
        {
            AppendOutputSafe(message);
        }

        private static Settings CreateSettingsCopy(Settings original)
        {
            return new Settings
            {
                WorldspaceSelection = new WorldspaceSelection
                {
                    SelectedWorldspaces = new ObservableCollection<FormKey>(original.WorldspaceSelection.SelectedWorldspaces)
                },
                CellSettings = new CellSettings
                {
                    InteriorCells = original.CellSettings.InteriorCells,
                    ExteriorCells = original.CellSettings.ExteriorCells,
                    ModdedCells = original.CellSettings.ModdedCells
                },
                OverrideSettings = new OverrideSettings
                {
                    IncludeSingles = original.OverrideSettings.IncludeSingles,
                    IncludeIdenticals = original.OverrideSettings.IncludeIdenticals,
                    IncludeNoConflicts = original.OverrideSettings.IncludeNoConflicts,
                    IncludeBethesdaConflicts = original.OverrideSettings.IncludeBethesdaConflicts,
                    IncludeBethesdaOverrides = original.OverrideSettings.IncludeBethesdaOverrides
                }
            };
        }

        private static HashSet<ModKey> GetCreationClubPlugins(FilePath creationClubListingsFilePath)
        {
            try
            {
                if (!File.Exists(creationClubListingsFilePath))
                    return [];

                return [.. File.ReadAllLines(creationClubListingsFilePath)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => ModKey.TryFromFileName(new FileName(line.Trim())))
                    .Where(plugin => plugin.HasValue)
                    .Select(plugin => plugin!.Value)];
            }
            catch
            {
                return [];
            }
        }

        private static bool IsNonConflicting<T>(IEnumerable<T> collection)
        {
            var list = collection.ToList();

            if (list.Count == 0)
                return false;
            if (list.Count == 2)
                return true;

            var distinctElements = list.Distinct().ToList();

            if (distinctElements.Count != 2)
                return false;

            var firstElement = distinctElements[0];
            var secondElement = distinctElements[1];

            bool foundSecond = false;
            foreach (var item in list)
                if (EqualityComparer<T>.Default.Equals(item, secondElement))
                    foundSecond = true;
                else if (foundSecond && EqualityComparer<T>.Default.Equals(item, firstElement))
                    return false;

            return true;
        }

        private static bool ShouldCollectNavmesh(
            IModContext<ISkyrimMod, ISkyrimModGetter, INavigationMesh, INavigationMeshGetter> navmesh,
            WorldspaceSelectionSnapshot worldspaceSelection,
            CellSettingsSnapshot cellSettings,
            OverrideSettingsSnapshot overrideSettings,
            HashSet<ModKey> basePlugins,
            ILinkCache cache,
            out ModKey parentModKey)
        {
            parentModKey = default;

            if (navmesh.Record.Data is null || navmesh.Parent?.ModKey is null)
                return false;

            parentModKey = navmesh.Parent.ModKey;

            var isFromBethesda = basePlugins.Contains(parentModKey);
            if (!overrideSettings.IncludeBethesdaOverrides && isFromBethesda)
                return false;

            if (!cellSettings.InteriorCells && navmesh.Record.Data.Parent is CellNavmeshParent)
                return false;

            if (!cellSettings.ExteriorCells && navmesh.Record.Data.Parent is WorldspaceNavmeshParent)
                return false;

            // Worldspace filtering - if specific worldspaces are selected, only include navmeshes from those worldspaces
            // TODO: Implement worldspace filtering once the correct property access pattern is determined
            // The WorldspaceNavmeshParent type needs investigation to find how to access the worldspace FormKey
            if (worldspaceSelection.SelectedWorldspaces.Count > 0 && navmesh.Record.Data.Parent is WorldspaceNavmeshParent worldspaceParent)
            {
                // Worldspace filtering is not yet fully implemented
                // For now, if any worldspaces are selected, log a warning
            }

            var isBasePlugin = basePlugins.Contains(navmesh.Record.FormKey.ModKey);
            if (!cellSettings.ModdedCells && !isBasePlugin)
                return false;

            var overrides = cache.ResolveAllSimpleContexts<INavigationMeshGetter>(navmesh.Record.FormKey).ToList();

            if (!overrideSettings.IncludeSingles && overrides.Count == 1)
                return false;

            var distinctOverrides = overrides
                .DistinctBy(o => o.Record.Data)
                .ToList();

            if (!overrideSettings.IncludeIdenticals && distinctOverrides.Count == 1)
                return false;

            var isNoConflictOverride = IsNonConflicting(overrides.Select(o => o.Record.Data));
            if (!overrideSettings.IncludeNoConflicts && isNoConflictOverride)
                return false;

            var isBethesdaConflict = (distinctOverrides.Count - distinctOverrides.Where(o => basePlugins.Contains(o.Parent!.ModKey)).Count()) == 1;
            if (!overrideSettings.IncludeBethesdaConflicts && isBethesdaConflict)
                return false;

            return true;
        }

        // Immutable snapshot classes to avoid cross-thread access
        private class WorldspaceSelectionSnapshot(WorldspaceSelection source)
        {
            public HashSet<FormKey> SelectedWorldspaces { get; } = [.. source.SelectedWorldspaces];
        }

        private class CellSettingsSnapshot(CellSettings source)
        {
            public bool InteriorCells { get; } = source.InteriorCells;
            public bool ExteriorCells { get; } = source.ExteriorCells;
            public bool ModdedCells { get; } = source.ModdedCells;
        }

        private class OverrideSettingsSnapshot(OverrideSettings source)
        {
            public bool IncludeSingles { get; } = source.IncludeSingles;
            public bool IncludeIdenticals { get; } = source.IncludeIdenticals;
            public bool IncludeNoConflicts { get; } = source.IncludeNoConflicts;
            public bool IncludeBethesdaConflicts { get; } = source.IncludeBethesdaConflicts;
            public bool IncludeBethesdaOverrides { get; } = source.IncludeBethesdaOverrides;
        }
    }
}
