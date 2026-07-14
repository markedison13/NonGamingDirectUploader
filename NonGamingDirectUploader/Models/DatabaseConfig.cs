using System;
using System.Collections.Generic;

namespace NonGamingDirectUploader.Models
{
    /// <summary>
    /// Backend registry that maps each uploader module + property combination
    /// to its designated Access database file. Replaces manual "Browse for DB"
    /// selection — every module/property pair has a fixed, pre-assigned database.
    ///
    /// Update the paths below to match your environment (e.g. a shared network
    /// drive), or load them from an app.config / appsettings.json at startup.
    /// </summary>
    public static class DatabaseConfig
    {
        private static readonly Dictionary<(UploaderType Module, PropertyType Property), string> _map = new()
        {
            { (UploaderType.Others,     PropertyType.SEC), @"C:\Users\marktubes\Documents\PROJECTS\DATABASE DUMMY\SEC\NonGaming.accdb" },
            { (UploaderType.Others,     PropertyType.SN),  @"C:\Users\marktubes\Documents\PROJECTS\DATABASE DUMMY\SN\NonGaming.accdb" },

            { (UploaderType.FnB,        PropertyType.SEC), @"C:\Users\marktubes\Documents\PROJECTS\DATABASE DUMMY\SEC\NonGaming.accdb" },
            { (UploaderType.FnB,        PropertyType.SN),  @"C:\Users\marktubes\Documents\PROJECTS\DATABASE DUMMY\SN\NonGaming.accdb" },

            { (UploaderType.Hotel,      PropertyType.SEC), @"C:\Users\marktubes\Documents\PROJECTS\DATABASE DUMMY\SEC\NonGaming.accdb" },
            { (UploaderType.Hotel,      PropertyType.SN),  @"C:\Users\marktubes\Documents\PROJECTS\DATABASE DUMMY\SN\NonGaming.accdb" },

            { (UploaderType.Visitation, PropertyType.SEC), @"C:\Users\marktubes\Documents\PROJECTS\DATABASE DUMMY\SEC\Statistics.accdb" },
            { (UploaderType.Visitation, PropertyType.SN),  @"C:\Users\marktubes\Documents\PROJECTS\DATABASE DUMMY\SN\Statistics.accdb" },
        };

        /// <summary>Resolves the designated database path for a module/property pair.</summary>
        public static bool TryGetPath(UploaderType module, PropertyType property, out string path)
            => _map.TryGetValue((module, property), out path!);

        /// <summary>Resolves the designated database path, or "" if none is configured.</summary>
        public static string GetPathOrEmpty(UploaderType module, PropertyType property)
            => _map.TryGetValue((module, property), out var path) ? path : "";
    }
}
