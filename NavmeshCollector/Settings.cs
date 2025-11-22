using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.WPF;
using Mutagen.Bethesda.WPF.Reflection.Attributes;
using Noggog;
using Noggog.WPF;
using System.Collections.ObjectModel;

namespace NavmeshCollector
{
    public class Settings
    {
        public WorldspaceSelection WorldspaceSelection { get; set; } = new();
        public CellSettings CellSettings { get; set; } = new();
        public OverrideSettings OverrideSettings { get; set; } = new();
    }

    public class WorldspaceSelection
    {
        [MaintainOrder]
        [SettingName("Selected Worldspaces")]
        [Tooltip("Select specific worldspaces to include. If none are selected, all worldspaces will be included.")]
        public ObservableCollection<FormKey> SelectedWorldspaces { get; set; } = [];
    }

    public class CellSettings
    {
        [MaintainOrder]
        [SettingName("Interior Cells")]
        [Tooltip("Navmeshes in interior cell records will be included in the output.")]
        public bool InteriorCells { get; set; } = true;

        [MaintainOrder]
        [SettingName("Exterior Cells")]
        [Tooltip("Navmeshes in exterior cell records will be included in the output.")]
        public bool ExteriorCells { get; set; } = true;

        [MaintainOrder]
        [SettingName("Modded Cells")]
        [Tooltip("Navmeshes in cell records from non-Bethesda plugins will be included in the output.")]
        public bool ModdedCells { get; set; } = true;
    }

    public class OverrideSettings
    {
        [MaintainOrder]
        [SettingName("Single Records")]
        [Tooltip("Navmeshes in records from single plugins will be included in the output.")]
        public bool IncludeSingles { get; set; } = false;

        [MaintainOrder]
        [SettingName("Identical Records")]
        [Tooltip("Navmeshes in records from identical plugins will be included in the output.")]
        public bool IncludeIdenticals { get; set; } = false;

        [MaintainOrder]
        [SettingName("No Conflict Overrides")]
        [Tooltip("Winning navmeshes in records from plugins that override without conflict will be included in the output.")]
        public bool IncludeNoConflicts { get; set; } = false;

        [MaintainOrder]
        [SettingName("Bethesda Conflicts")]
        [Tooltip("Winning navmesh overrides in records with conflicts due to Bethesda plugins will be excluded from the output.")]
        public bool IncludeBethesdaConflicts { get; set; } = false;

        [MaintainOrder]
        [SettingName("Bethesda Overrides")]
        [Tooltip("Winning navmesh overrides originating from Bethesda plugins will be included in the output.")]
        public bool IncludeBethesdaOverrides { get; set; } = false;
    }
}