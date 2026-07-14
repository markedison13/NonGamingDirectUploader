using NonGamingDirectUploader.Models;
using NonGamingDirectUploader.ViewModels;
using NonGamingDirectUploader.Models;

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

        public OthersViewModel OthersVM { get; } = new();
        public FnBViewModel FnBVM { get; } = new();
        public HotelViewModel HotelVM { get; } = new();
        public VisitationViewModel VisitationVM { get; } = new();
    }
}
