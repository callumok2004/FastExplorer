using System.Windows.Input;

namespace FastExplorer.ViewModels {
	public class PathSegmentViewModel(string name, string path, ICommand navigateCommand) : ViewModelBase {
		public string Name { get; } = name;
		public string Path { get; } = path;
		public ICommand NavigateCommand { get; } = navigateCommand;

		private bool _isDropTarget;
		public bool IsDropTarget {
			get => _isDropTarget;
			set => SetProperty(ref _isDropTarget, value);
		}
	}
}
