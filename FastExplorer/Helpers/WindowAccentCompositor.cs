using System;
using System.Runtime.InteropServices;

namespace FastExplorer.Helpers {
	public static class WindowAccentCompositor {
		[DllImport("user32.dll")]
		internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

		[StructLayout(LayoutKind.Sequential)]
		internal struct WindowCompositionAttributeData {
			public WindowCompositionAttribute Attribute;
			public IntPtr Data;
			public int SizeOfData;
		}

		internal enum WindowCompositionAttribute {
			WCA_ACCENT_POLICY = 19
		}

		internal enum AccentState {
			ACCENT_DISABLED = 0,
			ACCENT_ENABLE_GRADIENT = 1,
			ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
			ACCENT_ENABLE_BLURBEHIND = 3,
			ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
			ACCENT_INVALID_STATE = 5
		}

		[StructLayout(LayoutKind.Sequential)]
		internal struct AccentPolicy {
			public AccentState AccentState;
			public int AccentFlags;
			public int GradientColor;
			public int AnimationId;
		}

		public static void EnableBlur(IntPtr hwnd, int opacity = 64, uint color = 0x202020) {
			var accent = new AccentPolicy {
				AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
				GradientColor = unchecked(0x40202020)
			};
			SetAccentPolicy(hwnd, accent);
		}

		public static void DisableBlur(IntPtr hwnd) {
			var accent = new AccentPolicy {
				AccentState = AccentState.ACCENT_DISABLED
			};
			SetAccentPolicy(hwnd, accent);
		}

		private static void SetAccentPolicy(IntPtr hwnd, AccentPolicy accent) {
			var accentStructSize = Marshal.SizeOf(accent);
			var accentPtr = Marshal.AllocHGlobal(accentStructSize);
			Marshal.StructureToPtr(accent, accentPtr, false);

			var data = new WindowCompositionAttributeData {
				Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
				SizeOfData = accentStructSize,
				Data = accentPtr
			};

			_ = SetWindowCompositionAttribute(hwnd, ref data);
			Marshal.FreeHGlobal(accentPtr);
		}
	}
}
