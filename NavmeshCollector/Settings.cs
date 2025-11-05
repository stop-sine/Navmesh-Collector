using Mutagen.Bethesda.Synthesis.Settings;

namespace NavmeshCollector
{
    public class Settings
    {
        public CellSettings CellSettings { get; set; } = new();
        public OverrideSettings OverrideSettings { get; set; } = new();
    }

    public class CellSettings
    {
        [SynthesisOrder]
        [SynthesisSettingName("Include Interior Cells")]
        [SynthesisTooltip("Navmeshes in interior cell records will be included in the output.")]
        public bool InteriorCells { get; set; } = true;

        [SynthesisOrder]
        [SynthesisSettingName("Include Exterior Cells")]
        [SynthesisTooltip("Navmeshes in exterior cell records will be included in the output.")]
        public bool ExteriorCells { get; set; } = true;

        [SynthesisOrder]
        [SynthesisSettingName("Include Modded Cells")]
        [SynthesisTooltip("Navmeshes in cell records from non-Bethesda plugins will be included in the output.")]
        public bool ModdedCells { get; set; } = true;
    }

    public class OverrideSettings
    {
        [SynthesisOrder]
        [SynthesisSettingName("Include Single Records")]
        [SynthesisTooltip("Navmeshes in records from single plugins will be included in the output.")]
        public bool IncludeSingles { get; set; } = false;

        [SynthesisOrder]
        [SynthesisSettingName("Include Identical Records")]
        [SynthesisTooltip("Navmeshes in records from identical plugins will be included in the output.")]
        public bool IncludeIdenticals { get; set; } = false;

        [SynthesisOrder]
        [SynthesisSettingName("Include No Conflict Overrides")]
        [SynthesisTooltip("Winning navmeshes in records from plugins that override without conflict will be included in the output.")]
        public bool IncludeNoConflicts { get; set; } = false;

        [SynthesisOrder]
        [SynthesisSettingName("Include Bethesda Conflicts")]
        [SynthesisTooltip("Winning navmesh overrides in records with conflicts due to Bethesda plugins will be excluded from the output.")]
        public bool IncludeBethesdaConflicts { get; set; } = false;

        [SynthesisOrder]
        [SynthesisSettingName("Include Bethesda Overrides")]
        [SynthesisTooltip("Winning navmesh overrides originating from Bethesda plugins will be included in the output.")]
        public bool IncludeBethesdaOverrides { get; set; } = false;
    }
}