using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NonGamingDirectUploader.Models
{
    public class OtherRecord
    {
        public DateTime DTE { get; set; }
        public string DeptType { get; set; } = "";
        public string DeptDesc { get; set; } = "";
        public double Revenue { get; set; }
        public double Comp { get; set; }
    }

    public class FnBRecord
    {
        public DateTime DTE { get; set; }
        public string OutletCode { get; set; } = "";
        public string OutletName { get; set; } = "";
        public double FoodRevenue { get; set; }
        public double BevRevenue { get; set; }
        public double Covers { get; set; }
        public double Comp { get; set; }
    }

    public class HotelRecord
    {
        public DateTime DTE { get; set; }
        public string RoomType { get; set; } = "";
        public double RoomsOccupied { get; set; }
        public double RoomsAvailable { get; set; }
        public double RoomRevenue { get; set; }
        public double ADR { get; set; }
    }

    public class VisitationRecord
    {
        public DateTime DTE { get; set; }
        public string Category { get; set; } = "";
        public double Visitors { get; set; }
        public double PatronCount { get; set; }
        public double GrossRevenue { get; set; }
    }

    /// <summary>
    /// Every module handled by the app, across both business lines. Adding
    /// Mass/VIP/Junket here — rather than a separate parallel type — lets
    /// them reuse the same UploaderViewModel base, DatabaseService,
    /// AutomationService, etc. as the NonGaming modules automatically.
    /// </summary>
    public enum UploaderType
    {
        // NonGaming
        Others,
        FnB,
        Hotel,
        Visitation,

        // Gaming
        Mass,
        VIP,
        Junket
    }

    public enum UploadMode
    {
        Daily,
        Monthly
    }

    public enum PropertyType
    {
        SEC,
        SN
    }

    /// <summary>Which top-level side of the app a module belongs to.</summary>
    public enum BusinessLine
    {
        NonGaming,
        Gaming
    }

    public static class UploaderTypeExtensions
    {
        public static BusinessLine GetBusinessLine(this UploaderType type) => type switch
        {
            UploaderType.Others or UploaderType.FnB or UploaderType.Hotel or UploaderType.Visitation
                => BusinessLine.NonGaming,
            UploaderType.Mass or UploaderType.VIP or UploaderType.Junket
                => BusinessLine.Gaming,
            _ => BusinessLine.NonGaming
        };
    }
}