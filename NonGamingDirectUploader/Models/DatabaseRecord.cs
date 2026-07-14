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

    public enum UploaderType
    {
        Others,
        FnB,
        Hotel,
        Visitation
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
}
