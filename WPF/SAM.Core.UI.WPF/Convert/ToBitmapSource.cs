// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace SAM.Core.UI.WPF
{
    public static partial class Convert
    {
        /// <summary>
        /// Converts a <see cref="Bitmap"/> to a <see cref="BitmapSource"/>. WPF replacement for
        /// SAM.Core.Windows.Convert.ToBitmapSource (used for ribbon-button icons from resources).
        /// </summary>
        public static BitmapSource ToBitmapSource(this Bitmap bitmap)
        {
            if (bitmap == null)
            {
                return null;
            }

            IntPtr intPtr = bitmap.GetHbitmap();

            BitmapSource bitmapSource;
            try
            {
                bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(intPtr, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                Core.Modify.DeleteObject(intPtr);
            }

            return bitmapSource;
        }
    }
}
