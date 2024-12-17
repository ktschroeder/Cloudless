using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Media;
//using Rectangle = System.Windows.Shapes.Rectangle;

namespace SimpleImageViewer
{
    public partial class MainWindow : Window
    {
        private const int StarSize = 2;   // Diameter of each star
        
        private Canvas StarsCanvas;

        private Random _random = new Random();
        private bool isZen;
        private bool isWelcome = true;
        private DispatcherTimer _resizeStarTimer;

        private void InitializeZenMode()
        {
            //MyGrid.SizeChanged += (s, e) => { if (isZen) GenerateStars(); }; // TODO should wait until mouse up so we don't do this a bunch of  times
            _resizeStarTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400) // Adjust delay as needed
            };
            _resizeStarTimer.Tick += (s, e) =>
            {
                _resizeStarTimer.Stop();
                if (isZen) GenerateStars(); // Only regenerate stars once resizing stops
            };

            MyGrid.SizeChanged += (s, e) =>
            {
                ClearStars();
                _resizeStarTimer.Stop(); // Restart the timer on each size change
                _resizeStarTimer.Start();
            };
            _resizeStarTimer.Start();
        }

        private void Zen(bool includeInfoText)
        {
            RemoveZen(true);
            isZen = true;
            // via https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-animate-the-position-or-color-of-a-gradient-stop?view=netframeworkdesktop-4.8

            ImageDisplay.Visibility = Visibility.Collapsed;
            GradientMagic(); // Zen in context menu, also ability to unload image, possibly disable this
            GenerateStars();
            // Create the TextBlock for "No image is loaded" message
            if (includeInfoText)
            {
                if (!MyGrid.Children.Contains(NoImageMessage))
                {
                    MyGrid.Children.Add(NoImageMessage);
                    Canvas.SetZIndex(NoImageMessage, 99999);  // TODO lazy
                }
                    
            }
            else if (MyGrid.Children.Contains(NoImageMessage))
            {
                MyGrid.Children.Remove(NoImageMessage);
            }
        }

        private void RemoveZen(bool leaveInfo = false)
        {
            if (!isZen)
                return;

            if (!leaveInfo)
                MyGrid.Children.Remove(NoImageMessage);

            // Stop the active Storyboard, if any
            foreach (var resourceKey in Resources.Keys)
            {
                if (Resources[resourceKey] is Storyboard storyboard)
                {
                    storyboard.Stop(this);
                }
            }

            // Unregister all gradient stops and layers
            for (int layer = 0; layer <= 3; layer++)
            {
                for (int i = 0; i <= 3; i++)
                {
                    string gradientStopName = $"GradientStop{i}Layer{layer}";
                    if (this.FindName(gradientStopName) is GradientStop)
                    {
                        this.UnregisterName(gradientStopName);
                    }
                }

                string gradientLayerName = $"GradientLayer{layer}";
                if (this.FindName(gradientLayerName) is Rectangle)
                {
                    this.UnregisterName(gradientLayerName);
                }
            }

            // Clear the background brush (for layer 0)
            this.Background = new SolidColorBrush(new System.Windows.Media.Color() { ScA = 1 });

            // Remove all rectangles added to the Grid
            for (int i = MyGrid.Children.Count - 1; i >= 0; i--)
            {
                if (MyGrid.Children[i] is Rectangle)
                {
                    MyGrid.Children.RemoveAt(i);
                }
            }

            // Clear the storyboard children to release animations
            foreach (var resourceKey in Resources.Keys)
            {
                if (Resources[resourceKey] is Storyboard storyboard)
                {
                    storyboard.Stop(this);//
                    storyboard.Children.Clear();
                }
            }

            ClearStars();

            // Remove the canvas from the parent container
            if (MyGrid.Children.Contains(StarsCanvas))
            {
                MyGrid.Children.Remove(StarsCanvas);
            }

            // Set StarsCanvas to null to allow garbage collection
            StarsCanvas = null;

            if (currentlyDisplayedImagePath != null)
                ImageDisplay.Visibility = Visibility.Visible;

            isZen = false; // TODO check performance and that nothing is missed
        }

        private void Zen_Click(object sender, RoutedEventArgs e)
        {
            Zen(false);
        }


        private void ClearStars()
        {
            if (StarsCanvas == null) return;
            // clear animations to avoid memory leak
            foreach (UIElement child in StarsCanvas.Children)
            {
                if (child is FrameworkElement fe && fe.Tag is Storyboard storyboard)
                {
                    storyboard.Stop();
                    storyboard.Remove(); // Remove storyboard from the clock system
                    fe.BeginAnimation(UIElement.OpacityProperty, null); // Detach animations
                }
            }

            StarsCanvas.Children.Clear(); // Clear existing stars
        }

        private void GenerateStars()
        {
            if (StarsCanvas == null)
            {
                StarsCanvas = new Canvas
                {
                    Name = "StarsCanvas"
                };

            }
            else
            {
                ClearStars();
            }

            double width = MyGrid.ActualWidth;
            double height = MyGrid.ActualHeight;
            int starCount = 10 + (int)(width * height / 2500);  // 600 for 1920x1080 seemed good

            for (int i = 0; i < starCount; i++)
            {
                // Create a star (small circle)
                Ellipse star = new Ellipse
                {
                    Width = StarSize,
                    Height = StarSize,
                    Fill = Brushes.White,
                    Opacity = 0 // Start fully transparent
                };

                // Randomize initial position
                double startX = _random.NextDouble() * width;// StarsCanvas.ActualWidth;
                double startY = _random.NextDouble() * height;// StarsCanvas.ActualHeight; TODO

                Canvas.SetLeft(star, startX);
                Canvas.SetTop(star, startY);

                // Add the star to the canvas
                StarsCanvas.Children.Add(star);

                // Create animations for fade-in, movement, and fade-out
                const double AnimationDurationSeconds = 8;
                var withDelay = AnimationDurationSeconds + _random.NextDouble() * 10.0;
                var startingDelay = _random.NextDouble() * 25.0;
                var periodDelay = 0.5 + _random.NextDouble() * 8.0;
                DoubleAnimation fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(withDelay / 2.0)))
                {
                    BeginTime = TimeSpan.FromSeconds(periodDelay)
                };
                DoubleAnimation fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(withDelay / 2.0)))
                {
                    BeginTime = TimeSpan.FromSeconds(2*periodDelay + withDelay / 2.0) // Start fading out after fading in
                };

                // Apply animations
                Storyboard storyboard = new Storyboard
                {
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromSeconds(startingDelay)
                };

                Storyboard.SetTarget(fadeIn, star);
                Storyboard.SetTargetProperty(fadeIn, new PropertyPath(Ellipse.OpacityProperty));

                Storyboard.SetTarget(fadeOut, star);
                Storyboard.SetTargetProperty(fadeOut, new PropertyPath(Ellipse.OpacityProperty));

                storyboard.Children.Add(fadeIn);
                storyboard.Children.Add(fadeOut);

                star.Tag = storyboard;
                //Debug.WriteLine($"stars storyboard children: {storyboard.Children.Count}");
                // Start the animation
                storyboard.Begin(this, true);
            }

            if (!MyGrid.Children.Contains(StarsCanvas))
                MyGrid.Children.Add(StarsCanvas);
        }

        private void CreateMagicLayer(int layer, int gradientAngle, Storyboard storyboard)  // layer is 0 for background
        {
            // Create gradient stops for the brush.
            var tweak = (float)(_random.NextDouble() * 0.3 - 0.1);
            GradientStop stop0 = new GradientStop(System.Windows.Media.Color.FromScRgb(1F, 0.5F - tweak, 0F, 0.5F + tweak), 0.0);
            GradientStop stop1 = new GradientStop(System.Windows.Media.Color.FromScRgb(1F, 0F, 0F, 0.5F + tweak), 0.3);
            GradientStop stop2 = new GradientStop(System.Windows.Media.Color.FromScRgb(1F, 0.6F + tweak, 0F, 0.8F - tweak), 0.6);
            GradientStop stop3 = new GradientStop(System.Windows.Media.Color.FromScRgb(1F, 0F, 0.4F + tweak, 0.63F + tweak), 1.0);

            LinearGradientBrush gradientBrush = new LinearGradientBrush(
                new GradientStopCollection() { stop0, stop1, stop2, stop3 },
                gradientAngle
                );

            // Register a name for each gradient stop with the
            // page so that they can be animated by a storyboard.
            this.RegisterName("GradientStop0Layer" + layer, stop0);
            this.RegisterName("GradientStop1Layer" + layer, stop1);
            this.RegisterName("GradientStop2Layer" + layer, stop2);
            this.RegisterName("GradientStop3Layer" + layer, stop3);

            Func<int, int, Duration, double, double, DoubleAnimation> CreateOffsetAnimation = (layer, orderIndex, duration, baseFrom, baseTo) =>
            {
                DoubleAnimation offsetAnimation = new DoubleAnimation();
                offsetAnimation.From = baseFrom;
                offsetAnimation.To = baseTo;
                offsetAnimation.Duration = duration;
                offsetAnimation.AutoReverse = true;
                offsetAnimation.RepeatBehavior = RepeatBehavior.Forever;
                offsetAnimation.EasingFunction = new SineEase() { EasingMode = EasingMode.EaseInOut };
                Storyboard.SetTargetName(offsetAnimation, $"GradientStop{orderIndex}Layer{layer}");
                Storyboard.SetTargetProperty(offsetAnimation,
                    new PropertyPath(GradientStop.OffsetProperty));
                return offsetAnimation;
            };

            Func<string, Duration, System.Windows.Media.Color, TimeSpan, ColorAnimation> CreateOpacityOrColorAnimation = (targetName, duration, colorBy, beginTime) =>
            {
                ColorAnimation opacityOrColorAnimation = new ColorAnimation();
                opacityOrColorAnimation.By = colorBy;
                opacityOrColorAnimation.Duration = duration;
                opacityOrColorAnimation.AutoReverse = true;
                opacityOrColorAnimation.RepeatBehavior = RepeatBehavior.Forever;
                opacityOrColorAnimation.EasingFunction = new SineEase() { EasingMode = EasingMode.EaseInOut };
                opacityOrColorAnimation.BeginTime = beginTime;
                Storyboard.SetTargetName(opacityOrColorAnimation, targetName);
                Storyboard.SetTargetProperty(opacityOrColorAnimation,
                    new PropertyPath(GradientStop.ColorProperty));
                return opacityOrColorAnimation;
            };

            // We've intentionally skipped index 0 to not animate that gradient stop.
            var oa1 = CreateOffsetAnimation(layer, 1, TimeSpan.FromSeconds(15 + _random.NextDouble() * 15), 0.03, 0.30);
            var oa2 = CreateOffsetAnimation(layer, 2, TimeSpan.FromSeconds(15 + _random.NextDouble() * 15), 0.73, 0.37);
            var oa3 = CreateOffsetAnimation(layer, 3, TimeSpan.FromSeconds(15 + _random.NextDouble() * 15), 0.97, 0.8);
            storyboard.Children.Add(oa1);
            storyboard.Children.Add(oa2);
            storyboard.Children.Add(oa3);

            for (int i = 0; i <= 3; i++)
            {
                if (layer > 0 && _random.NextDouble() > 0.5)
                {
                    var opacityOA = CreateOpacityOrColorAnimation($"GradientStop{i}Layer{layer}", TimeSpan.FromSeconds(15 + _random.NextDouble() * 50), System.Windows.Media.Color.FromScRgb(-1.0F, 0F, 0F, 0F), TimeSpan.Zero);
                    storyboard.Children.Add(opacityOA);
                }
                else
                {
                    var tweak1 = (float)(0.1 + _random.NextDouble() * 0.25);
                    var tweak2 = (float)(0.1 + _random.NextDouble() * 0.25);
                    var tweak3 = (float)(0.1 + _random.NextDouble() * 0.25);
                    var colorOA = CreateOpacityOrColorAnimation($"GradientStop{i}Layer{layer}", TimeSpan.FromSeconds(10 + _random.NextDouble() * 40), System.Windows.Media.Color.FromScRgb(0F, tweak1, tweak2, tweak3), TimeSpan.Zero);
                    storyboard.Children.Add(colorOA);
                }
            }

            if (layer == 0)
            {
                this.Background = gradientBrush;
            }
            else
            {
                Rectangle rect = new Rectangle();
                rect.HorizontalAlignment = HorizontalAlignment.Stretch; // Expand to container width
                rect.VerticalAlignment = VerticalAlignment.Stretch;     // Expand to container height
                rect.Fill = gradientBrush;
                rect.Opacity = 0.32;

                if (layer > 2)
                {
                    rect.Opacity = 0;
                    this.RegisterName("GradientLayer" + layer, rect);
                    var layerOpacityOA = CreateOpacityOrColorAnimation($"GradientLayer{layer}", TimeSpan.FromSeconds(10 + _random.NextDouble() * 30), System.Windows.Media.Color.FromScRgb(0.4F, 0F, 0F, 0F), TimeSpan.FromSeconds((layer - 2) * 5));
                }
                MyGrid.Children.Add(rect);
            }
        }

        private void GradientMagic()
        {
            // Create a NameScope for the page so that
            // Storyboards can be used.
            NameScope.SetNameScope(this, new NameScope());

            Storyboard storyboard = new Storyboard();
            var baseRotation = (int)_random.NextInt64(360, 720);
            CreateMagicLayer(0, baseRotation + (int)_random.NextInt64(5, 55), storyboard);
            CreateMagicLayer(1, baseRotation + (int)_random.NextInt64(185, 235), storyboard);
            CreateMagicLayer(2, baseRotation + (int)_random.NextInt64(65, 115), storyboard);
            CreateMagicLayer(3, baseRotation + (int)_random.NextInt64(245, 295), storyboard);
            CreateMagicLayer(4, baseRotation + (int)_random.NextInt64(125, 175), storyboard);
            CreateMagicLayer(5, baseRotation + (int)_random.NextInt64(305, 355), storyboard);

            storyboard.Begin(this);
        }
    }
}
