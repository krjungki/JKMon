// Enabling WinForms for the tray icon makes several WPF type names ambiguous; resolve them once here.
// Color is deliberately absent so the GDI+ files can alias it themselves.
global using Brush = System.Windows.Media.Brush;
global using FontFamily = System.Windows.Media.FontFamily;
global using HorizontalAlignment = System.Windows.HorizontalAlignment;
global using Point = System.Windows.Point;
global using Size = System.Windows.Size;
global using VerticalAlignment = System.Windows.VerticalAlignment;
