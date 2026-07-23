using System.Collections.Generic;

namespace NonGamingDirectUploader.Models
{
    /// <summary>
    /// Maps each uploader module + property combination to the ONE specific
    /// file the automation reads from. Each entry is a fixed, known file path
    /// (typically re-generated/overwritten daily by another process, e.g.
    /// SAS) — not a folder to scan.
    ///
    /// EDIT EVERY PATH BELOW to the real file for each module/property.
    /// Any (module, property) pair left as "" is simply skipped by the
    /// automation — nothing breaks if some aren't ready yet.
    /// </summary>
    public static class AutomationConfig
    {
        private static readonly Dictionary<(UploaderType Module, PropertyType Property), string> _map = new()
        {
            { (UploaderType.Others, PropertyType.SEC), "U:\\FI-FP&A\\6. Users\\Angelo\\SAS\\ToUpload_SEC_OOD_VS.xlsx" },
            { (UploaderType.Others, PropertyType.SN),  "U:\\FI-FP&A\\6. Users\\Angelo\\SAS\\ToUpload_SN_OOD_VS.xlsx" },

            { (UploaderType.FnB,    PropertyType.SEC), "U:\\FI-FP&A\\6. Users\\Angelo\\SAS\\ToUpload_SEC_F&B_VS.xlsx" },
            { (UploaderType.FnB,    PropertyType.SN),  "U:\\FI-FP&A\\6. Users\\Angelo\\SAS\\ToUpload_SN_F&B_VS.xlsx" },

            { (UploaderType.Hotel,  PropertyType.SEC), "U:\\FI-FP&A\\6. Users\\Angelo\\SAS\\ToUpload_SEC_Hotel_VS.xlsx" },
            { (UploaderType.Hotel,  PropertyType.SN),  "U:\\FI-FP&A\\6. Users\\Angelo\\SAS\\ToUpload_SN_Hotel_VS.xlsx" },

            // Visitation is intentionally not included — the automation
            // does not process the Visitation module.
        };

        /// <summary>Resolves the configured file path for a module/property pair, if any.</summary>
        public static bool TryGetFilePath(UploaderType module, PropertyType property, out string path)
            => _map.TryGetValue((module, property), out path!);

        /// <summary>Resolves the configured file path, or "" if none is configured.</summary>
        public static string GetFilePathOrEmpty(UploaderType module, PropertyType property)
            => _map.TryGetValue((module, property), out var path) ? path : "";
    }
}