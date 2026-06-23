using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Cabo.Client.Platform
{
    public sealed class WindowsAspectRatioEnforcer : MonoBehaviour
    {
        const float TargetAspect = 16f / 9f;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        const int GwlWndProc = -4;
        const int WmSizing = 0x0214;
        const int WmszLeft = 1;
        const int WmszRight = 2;
        const int WmszTop = 3;
        const int WmszTopLeft = 4;
        const int WmszTopRight = 5;
        const int WmszBottom = 6;
        const int WmszBottomLeft = 7;
        const int WmszBottomRight = 8;

        delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        static WndProcDelegate _wndProcDelegate;
        static IntPtr _originalWndProc;
        static IntPtr _windowHandle;
        static bool _installed;

        void Start()
        {
            Install();
        }

        void OnDestroy()
        {
            Uninstall();
        }

        void OnApplicationQuit()
        {
            Uninstall();
        }

        static void Install()
        {
            if (_installed)
                return;

            _windowHandle = GetActiveWindow();
            if (_windowHandle == IntPtr.Zero)
                return;

            _wndProcDelegate = WndProc;
            _originalWndProc = SetWindowLongPtr(_windowHandle, GwlWndProc, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
            _installed = _originalWndProc != IntPtr.Zero;
        }

        static void Uninstall()
        {
            if (!_installed || _windowHandle == IntPtr.Zero || _originalWndProc == IntPtr.Zero)
                return;

            SetWindowLongPtr(_windowHandle, GwlWndProc, _originalWndProc);
            _installed = false;
            _windowHandle = IntPtr.Zero;
            _originalWndProc = IntPtr.Zero;
            _wndProcDelegate = null;
        }

        static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WmSizing && lParam != IntPtr.Zero)
            {
                var rect = Marshal.PtrToStructure<Rect>(lParam);
                ConstrainRect(ref rect, wParam.ToInt32());
                Marshal.StructureToPtr(rect, lParam, false);
                return new IntPtr(1);
            }

            return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
        }

        static void ConstrainRect(ref Rect rect, int edge)
        {
            int width = Math.Max(1, rect.Right - rect.Left);
            int height = Math.Max(1, rect.Bottom - rect.Top);

            if (edge == WmszLeft || edge == WmszRight)
            {
                height = Mathf.RoundToInt(width / TargetAspect);
                ResizeVerticalFromCenter(ref rect, height);
                return;
            }

            if (edge == WmszTop || edge == WmszBottom)
            {
                width = Mathf.RoundToInt(height * TargetAspect);
                ResizeHorizontalFromCenter(ref rect, width);
                return;
            }

            float widthDrivenHeight = width / TargetAspect;
            float heightDrivenWidth = height * TargetAspect;
            bool useWidth = Math.Abs(widthDrivenHeight - height) <= Math.Abs(heightDrivenWidth - width);

            if (useWidth)
            {
                height = Mathf.RoundToInt(widthDrivenHeight);
                if (edge == WmszTopLeft || edge == WmszTopRight)
                    rect.Top = rect.Bottom - height;
                else
                    rect.Bottom = rect.Top + height;
            }
            else
            {
                width = Mathf.RoundToInt(heightDrivenWidth);
                if (edge == WmszTopLeft || edge == WmszBottomLeft)
                    rect.Left = rect.Right - width;
                else
                    rect.Right = rect.Left + width;
            }
        }

        static void ResizeHorizontalFromCenter(ref Rect rect, int width)
        {
            int center = (rect.Left + rect.Right) / 2;
            rect.Left = center - width / 2;
            rect.Right = rect.Left + width;
        }

        static void ResizeVerticalFromCenter(ref Rect rect, int height)
        {
            int center = (rect.Top + rect.Bottom) / 2;
            rect.Top = center - height / 2;
            rect.Bottom = rect.Top + height;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll")]
        static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
#endif
    }
}
