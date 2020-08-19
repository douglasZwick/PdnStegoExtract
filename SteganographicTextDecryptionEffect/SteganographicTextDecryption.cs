using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Reflection;
using System.Drawing.Text;
using System.Windows.Forms;
using System.IO.Compression;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using Registry = Microsoft.Win32.Registry;
using RegistryKey = Microsoft.Win32.RegistryKey;
using PaintDotNet;
using PaintDotNet.AppModel;
using PaintDotNet.Clipboard;
using PaintDotNet.IndirectUI;
using PaintDotNet.Collections;
using PaintDotNet.PropertySystem;
using PaintDotNet.Effects;
using ColorWheelControl = PaintDotNet.ColorBgra;
using AngleControl = System.Double;
using PanSliderControl = PaintDotNet.Pair<double,double>;
using FilenameControl = System.String;
using ReseedButtonControl = System.Byte;
using RollControl = System.Tuple<double, double, double>;
using IntSliderControl = System.Int32;
using CheckboxControl = System.Boolean;
using TextboxControl = System.String;
using DoubleSliderControl = System.Double;
using ListBoxControl = System.Byte;
using RadioButtonControl = System.Byte;
using MultiLineTextboxControl = System.String;

[assembly: AssemblyTitle("SteganographicTextDecryption plugin for Paint.NET")]
[assembly: AssemblyDescription("Extract Text selected pixels")]
[assembly: AssemblyConfiguration("extract text")]
[assembly: AssemblyCompany("Doug Zwick")]
[assembly: AssemblyProduct("SteganographicTextDecryption")]
[assembly: AssemblyCopyright("Copyright ©2020 by Doug Zwick")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("1.0.*")]

namespace SteganographicTextDecryptionEffect
{
    public class PluginSupportInfo : IPluginSupportInfo
    {
        public string Author
        {
            get
            {
                return base.GetType().Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright;
            }
        }

        public string Copyright
        {
            get
            {
                return base.GetType().Assembly.GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
            }
        }

        public string DisplayName
        {
            get
            {
                return base.GetType().Assembly.GetCustomAttribute<AssemblyProductAttribute>().Product;
            }
        }

        public Version Version
        {
            get
            {
                return base.GetType().Assembly.GetName().Version;
            }
        }

        public Uri WebsiteUri
        {
            get
            {
                return new Uri("https://www.getpaint.net/redirect/plugins.html");
            }
        }
    }

    [PluginSupportInfo(typeof(PluginSupportInfo), DisplayName = "Extract Text")]
    public class SteganographicTextDecryptionEffectPlugin : PropertyBasedEffect
    {
        public static string StaticName
        {
            get
            {
                return "Extract Text";
            }
        }

        public static Image StaticIcon
        {
            get
            {
                return null;
            }
        }

        public static string SubmenuName
        {
            get
            {
                return "Steganography";
            }
        }

        public SteganographicTextDecryptionEffectPlugin()
            : base(StaticName, StaticIcon, SubmenuName, new EffectOptions() { Flags = EffectFlags.Configurable | EffectFlags.SingleThreaded, RenderingSchedule = EffectRenderingSchedule.None })
        {
        }

        public enum PropertyNames
        {
            M_Modulus,
            M_Offset,
            M_Text
        }


        protected override PropertyCollection OnCreatePropertyCollection()
        {
            List<Property> props = new List<Property>();

            props.Add(new Int32Property(PropertyNames.M_Modulus, 7, 1, 1024));
            props.Add(new Int32Property(PropertyNames.M_Offset, 0, 0, 1024));
            props.Add(new StringProperty(PropertyNames.M_Text, "", 32767));

            return new PropertyCollection(props);
        }

        protected override ControlInfo OnCreateConfigUI(PropertyCollection props)
        {
            ControlInfo configUI = CreateDefaultConfigUI(props);

            configUI.SetPropertyControlValue(PropertyNames.M_Modulus, ControlInfoPropertyNames.DisplayName, "Modulus");
            configUI.SetPropertyControlValue(PropertyNames.M_Offset, ControlInfoPropertyNames.DisplayName, "Offset");
            configUI.SetPropertyControlValue(PropertyNames.M_Text, ControlInfoPropertyNames.DisplayName, "Output Text");
            configUI.SetPropertyControlType(PropertyNames.M_Text, PropertyControlType.TextBox);
            configUI.SetPropertyControlValue(PropertyNames.M_Text, ControlInfoPropertyNames.Multiline, true);

            return configUI;
        }

        protected override void OnCustomizeConfigUIWindowProperties(PropertyCollection props)
        {
            // Change the effect's window title
            props[ControlInfoPropertyNames.WindowTitle].Value = "Steganographic Text Decryption";
            // Add help button to effect UI
            props[ControlInfoPropertyNames.WindowHelpContentType].Value = WindowHelpContentType.PlainText;
            props[ControlInfoPropertyNames.WindowHelpContent].Value = "Extract Text v1.0\nCopyright ©2020 by Doug Zwick\nAll rights reserved.";
            base.OnCustomizeConfigUIWindowProperties(props);
        }

        protected override void OnSetRenderInfo(PropertyBasedEffectConfigToken token, RenderArgs dstArgs, RenderArgs srcArgs)
        {
            m_Modulus = token.GetProperty<Int32Property>(PropertyNames.M_Modulus).Value;
            m_Offset = token.GetProperty<Int32Property>(PropertyNames.M_Offset).Value;
            m_Text = token.GetProperty<StringProperty>(PropertyNames.M_Text).Value;

            base.OnSetRenderInfo(token, dstArgs, srcArgs);
        }

        protected override unsafe void OnRender(Rectangle[] rois, int startIndex, int length)
        {
            if (length == 0) return;
            for (int i = startIndex; i < startIndex + length; ++i)
            {
                Render(DstArgs.Surface,SrcArgs.Surface,rois[i]);
            }
        }

        #region User Entered Code
        // Name: Extract Text
        // Submenu: Steganography
        // Author: Doug Zwick
        // Title: Steganographic Text Decryption
        // Version: 0.1.0
        // Desc:
        // Keywords:
        // URL:
        // Help:
        #region UICode
        IntSliderControl m_Modulus = 7; // [1,1024] Modulus
        IntSliderControl m_Offset = 0; // [0,1024] Offset
        MultiLineTextboxControl m_Text = ""; // [32767] Output Text
        #endregion
        
        void Render(Surface dst, Surface src, Rectangle rect)
        {
            // Delete any of these lines you don't need
            Rectangle selection = EnvironmentParameters.SelectionBounds;
            int centerX = ((selection.Right - selection.Left) / 2) + selection.Left;
            int centerY = ((selection.Bottom - selection.Top) / 2) + selection.Top;
            ColorBgra primaryColor = EnvironmentParameters.PrimaryColor;
            ColorBgra secondaryColor = EnvironmentParameters.SecondaryColor;
            int brushWidth = (int)EnvironmentParameters.BrushWidth;
        
            ColorBgra currentPixel;
            for (int y = rect.Top; y < rect.Bottom; y++)
            {
                if (IsCancelRequested) return;
                for (int x = rect.Left; x < rect.Right; x++)
                {
                    currentPixel = src[x,y];
                    // TODO: Add pixel processing code here
                    // Access RGBA values this way, for example:
                    // currentPixel.R = primaryColor.R;
                    // currentPixel.G = primaryColor.G;
                    // currentPixel.B = primaryColor.B;
                    // currentPixel.A = primaryColor.A;
                    dst[x,y] = currentPixel;
                }
            }
        }
        
        #endregion
    }
}
