using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.IO;
using Wpf.Ui.Controls;

namespace OCIDE.Services
{
    public static class IconManager
    {
        public static object GetIcon(string fileName, bool isDirectory)
        {
            if (isDirectory)
            {
                return new SymbolIcon { Symbol = SymbolRegular.Folder24, FontSize = 16, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC")) };
            }

            string ext = System.IO.Path.GetExtension(fileName).ToLower();

            if (ext == ".html" || ext == ".htm")
            {
                // HTML5 Logo from SVG
                var canvas = new Canvas { Width = 512, Height = 512 };
                
                canvas.Children.Add(new System.Windows.Shapes.Path { Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E34F26")), Data = Geometry.Parse("M 108,475 L 72,71 L 440,71 L 404,475 L 256,512 Z") });
                canvas.Children.Add(new System.Windows.Shapes.Path { Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF652A")), Data = Geometry.Parse("M 256,481 L 376,447 L 404,131 L 256,131 Z") });
                canvas.Children.Add(new System.Windows.Shapes.Path { Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EBEBEB")), Data = Geometry.Parse("M 256,268 L 196,268 L 192,222 L 256,222 L 256,176 L 142,176 L 154,314 L 256,314 Z") });
                canvas.Children.Add(new System.Windows.Shapes.Path { Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EBEBEB")), Data = Geometry.Parse("M 256,386 L 205,373 L 202,336 L 156,336 L 163,408 L 256,433 Z") });
                canvas.Children.Add(new System.Windows.Shapes.Path { Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF")), Data = Geometry.Parse("M 256,176 L 256,222 L 365,222 L 370,176 Z") });
                canvas.Children.Add(new System.Windows.Shapes.Path { Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF")), Data = Geometry.Parse("M 256,268 L 256,314 L 312,314 L 306,373 L 256,386 L 256,433 L 349,408 L 361,268 Z") });

                return new Viewbox { Width = 16, Height = 16, Child = canvas, Stretch = Stretch.Uniform };
            }

            if (ext == ".js")
            {
                // Custom JS Logo from SVG
                var canvas = new Canvas { Width = 512, Height = 512 };
                
                canvas.Children.Add(new System.Windows.Shapes.Path { Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7DF1E")), Data = Geometry.Parse("M 0,0 L 512,0 L 512,512 L 0,512 Z") });
                canvas.Children.Add(new System.Windows.Shapes.Path { Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#000000")), Data = Geometry.Parse("M 290,250 L 330,250 L 330,450 L 190,450 L 190,370 L 230,370 L 230,410 L 290,410 Z") });
                canvas.Children.Add(new System.Windows.Shapes.Path { Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#000000")), Data = Geometry.Parse("M 370,250 L 470,250 L 470,290 L 410,290 L 410,330 L 470,330 L 470,450 L 370,450 L 370,410 L 430,410 L 430,370 L 370,370 Z") });

                return new Viewbox { Width = 16, Height = 16, Child = canvas, Stretch = Stretch.Uniform };
            }

            // Default SymbolIcons
            var symbol = ext switch
            {
                ".cs" => SymbolRegular.Code24,
                ".xaml" => SymbolRegular.WindowDevTools24,
                ".json" => SymbolRegular.DocumentData24,
                ".md" => SymbolRegular.DocumentText24,
                ".txt" => SymbolRegular.DocumentText24,
                ".xml" => SymbolRegular.DocumentData24,
                ".png" or ".jpg" or ".jpeg" or ".svg" => SymbolRegular.Image24,
                ".py" => SymbolRegular.Document24, // generic
                _ => SymbolRegular.Document24
            };

            return new SymbolIcon { Symbol = symbol, FontSize = 16, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC")) };
        }
    }
}
