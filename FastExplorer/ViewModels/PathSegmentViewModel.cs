using System.Windows.Input;

namespace FastExplorer.ViewModels {
	public class PathSegmentViewModel : ViewModelBase {
		public string Name { get; }
		public string Path { get; }
		public ICommand NavigateCommand { get; }

		private bool _isDropTarget;
		public bool IsDropTarget {
			get => _isDropTarget;
			set => SetProperty(ref _isDropTarget, value);
		}

		public PathSegmentViewModel(string name, string path, ICommand navigateCommand) {
			Name = name;
			Path = path;
			NavigateCommand = navigateCommand;
		}
	}
}
