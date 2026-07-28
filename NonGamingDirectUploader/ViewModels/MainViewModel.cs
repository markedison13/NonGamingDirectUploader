using NonGamingDirectUploader.Models;
using NonGamingDirectUploader.ViewModels;

namespace NonGamingDirectUploader.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private UploaderType _activeUploader = UploaderType.Others;
        public UploaderType ActiveUploader
        {
            get => _activeUploader;
            set => Set(ref _activeUploader, value);
        }

        private BusinessLine _activeBusinessLine = BusinessLine.NonGaming;
        public BusinessLine ActiveBusinessLine
        {
            get => _activeBusinessLine;
            set => Set(ref _activeBusinessLine, value);
        }

        // ── NonGaming ────────────────────────────────────────────────────────
        public OthersViewModel OthersVM { get; } = new();
        public FnBViewModel FnBVM { get; } = new();
        public HotelViewModel HotelVM { get; } = new();
        public VisitationViewModel VisitationVM { get; } = new();

        // ── Gaming ───────────────────────────────────────────────────────────
        public MassViewModel MassVM { get; } = new();
        public VIPViewModel VIPVM { get; } = new();
        public JunketViewModel JunketVM { get; } = new();
    }
}