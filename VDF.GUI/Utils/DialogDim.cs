using System;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using VDF.GUI.Views;

namespace VDF.GUI.Utils {

	/// <summary>
	/// The mockup's backdrop, in the only shape a desktop app can have it: a dialog is
	/// its own top-level window, so there is no layer under it to darken - but the main
	/// window behind it can be, and that is what the eye reads as depth. Every window
	/// that carries this property dims the shell while it is open and restores it when
	/// it closes or hides; tracking visible dialogs keeps nested dialogs from clearing the
	/// dim too early.
	/// </summary>
	public static class DialogDim {
		public static readonly AttachedProperty<bool> AutoDimProperty =
			AvaloniaProperty.RegisterAttached<Window, bool>("AutoDim", typeof(DialogDim));

		public static bool GetAutoDim(Window w) => w.GetValue(AutoDimProperty);
		public static void SetAutoDim(Window w, bool value) => w.SetValue(AutoDimProperty, value);

		static readonly HashSet<Window> OpenDialogs = new();

		// Closing a window detaches its styles, and detaching this one drives AutoDim back
		// to false. So the Changed handler must never unsubscribe: the obvious shape of
		// this code (subscribe when true, unsubscribe when false) drops Closed an instant
		// before it is raised, the dialog is never removed, and the shell stays dimmed
		// for the rest of the session. The table keeps the subscribe from happening twice.
		static readonly ConditionalWeakTable<Window, object> Hooked = new();

		static DialogDim() {
			AutoDimProperty.Changed.Subscribe(e => {
				if (e.Sender is not Window w || w is MainWindow) return;
				if (!(e.NewValue.HasValue && e.NewValue.Value)) return;
				if (Hooked.TryGetValue(w, out _)) return;
				Hooked.Add(w, new object());
				w.Opened += OnOpened;
				w.Closed += OnClosed;
				w.PropertyChanged += (_, args) => {
					if (args.Property == Visual.IsVisibleProperty && !w.IsVisible) {
						w.Classes.Set("dialog-open", false);
						if (OpenDialogs.Remove(w)) Apply(OpenDialogs.Count > 0);
					}
				};
			});
		}

		static void OnOpened(object? sender, EventArgs e) {
			if (sender is not Window window) return;
			window.Classes.Set("dialog-open", true);
			if (OpenDialogs.Add(window)) Apply(true);
		}

		static void OnClosed(object? sender, EventArgs e) {
			if (sender is Window window && OpenDialogs.Remove(window)) Apply(OpenDialogs.Count > 0);
		}

		// Walked by hand rather than through ApplicationHelpers.MainWindowDataContext:
		// that getter casts a MainWindow it assumes exists, so in a headless session -
		// where every dialog in the test suites opens without a shell behind it - it
		// throws before there is anything to null-check. There is simply no shell to
		// dim there, which is what the chain below says.
		static void Apply(bool on) {
			var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
			if (lifetime?.MainWindow?.DataContext is ViewModels.MainWindowVM vm)
				vm.IsDialogDimmed = on;
		}
	}
}
