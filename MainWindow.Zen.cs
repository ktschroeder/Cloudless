using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Media;
using Rectangle = System.Windows.Shapes.Rectangle;
using Brushes = System.Windows.Media.Brushes;

namespace Cloudless
{
    public partial class MainWindow : Window
    {
        private const int StarSize = 2;
        
        private Canvas? StarsCanvas;

        private Random _random = new Random();
        private bool isZen;
        private int staticStarSession = -1;  // smelly technique for determining in star child generation whether they should proceed (if in old session, then no).
        private bool isWelcome = true;
        private DispatcherTimer? _resizeStarTimer;
        private DispatcherTimer? _shootingStarTimer;
        //private int brushKey = 0;
        //private Storyboard? orchStoryboard;

        private List<GradientStopContext> gradientStopContexts = new List<GradientStopContext>();
        private int magicLayersCreated = 0;
        private const int CONCURRENT_ZEN_LAYERS = 4;

        private void InitializeZenMode()
        {
            _resizeStarTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _resizeStarTimer.Tick += (s, e) =>
            {
                _resizeStarTimer.Stop();
                if (isZen) {
                    GenerateStars();
                }; // Only regenerate stars once resizing stops
            };

            MyGrid.SizeChanged += (s, e) =>
            {
                ClearStars();
                _resizeStarTimer.Stop(); // Restart the timer on each size change
                _resizeStarTimer.Start();
            };
            _resizeStarTimer.Start();
            _shootingStarTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _shootingStarTimer.Tick += (s, e) =>
            {
                if (isZen)
                {
                    double rand = _random.NextDouble();
                    if (rand < 0.01)
                    {
                        int count = 3 + _random.Next(0, 5);
                        for (int i = 0; i < count; i++)
                        {
                            Task.Delay(i * 200).ContinueWith(_ =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    if (isZen)
                                        LaunchShootingStar();
                                });
                            });
                        }
                    }
                    else if (rand < 0.1)
                        LaunchShootingStar();
                }
            };
            _shootingStarTimer.Start();
        }

        private void Zen(bool includeInfoText)
        {
            // TODO wrap zen in a big try/catch and just disable it rather than crashing app

            RemoveZen(true);
            isZen = true;
            // via https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-animate-the-position-or-color-of-a-gradient-stop?view=netframeworkdesktop-4.8

            ImageDisplay.Visibility = Visibility.Collapsed;
            VideoHost.Height = 0;
            VideoHost.Width = 0;

            magicLayersCreated = 0;
            GradientMagic(); // Zen in context menu, also ability to unload image, possibly disable this
            GenerateStars();
            if (includeInfoText)
            {
                if (!MyGrid.Children.Contains(NoImageMessage))
                {
                    MyGrid.Children.Add(NoImageMessage);
                    Canvas.SetZIndex(NoImageMessage, 99999);  // lazy
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

            foreach (var ml in magicLayers)
            {
                ml.Free(this, MyGrid);
            }

            SetBackground();

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

            magicLayers.Clear();

            ClearStars();

            // Remove the canvas from the parent container
            if (MyGrid.Children.Contains(StarsCanvas))
            {
                MyGrid.Children.Remove(StarsCanvas);
            }

            // Set StarsCanvas to null to allow garbage collection
            StarsCanvas = null;

            try { _shootingStarTimer?.Stop(); } catch { }
            _shootingStarTimer = null;

            if (currentlyDisplayedImagePath != null)
            {
                ImageDisplay.Visibility = Visibility.Visible;
                VideoHost.Height = double.NaN;
                VideoHost.Width = double.NaN;
            }

            isZen = false; // TODO check performance and that nothing is missed
        }

        private void Zen_Click(object sender, RoutedEventArgs e)
        {
            isWelcome = false;
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
            staticStarSession++;
            var session = staticStarSession;

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
            int starCount = 10 + (int)(width * height / 2500);

            for (int i = 0; i < starCount; i++)
            {
                var repeatCount = (int)(Math.Pow(_random.NextInt64(1, 100), 2) + _random.NextInt64(0, 5));
                CreateStar(width, height, repeatCount, session);
            }

            if (!MyGrid.Children.Contains(StarsCanvas))
                MyGrid.Children.Add(StarsCanvas);

            Canvas.SetZIndex(StarsCanvas, 99998);  // lazy
        }

        // repeatCount is a parameter because if we generate it within and don't reuse it, we will gradually skew toward stars with many more repeats on-screen
        private void CreateStar(double canvasWidth, double canvasHeight, int repeatCount, int starSession)
        {
            if (!isZen)
                return;

            // Create a star (small circle)
            Ellipse star = new Ellipse
            {
                Width = StarSize,
                Height = StarSize,
                Fill = Brushes.White,
                Opacity = 0 // Start fully transparent
            };

            // Randomize initial position
            double startX = _random.NextDouble() * canvasWidth;
            double startY = _random.NextDouble() * canvasHeight;

            Canvas.SetLeft(star, startX);
            Canvas.SetTop(star, startY);

            // Add the star to the canvas
            StarsCanvas?.Children.Add(star);

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
                BeginTime = TimeSpan.FromSeconds(2 * periodDelay + withDelay / 2.0) // Start fading out after fading in
            };

            Storyboard storyboard = new Storyboard
            {
                RepeatBehavior = new RepeatBehavior(count: repeatCount),
                BeginTime = TimeSpan.FromSeconds(startingDelay)
            };

            Storyboard.SetTarget(fadeIn, star);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(Ellipse.OpacityProperty));

            Storyboard.SetTarget(fadeOut, star);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(Ellipse.OpacityProperty));

            storyboard.Children.Add(fadeIn);
            storyboard.Children.Add(fadeOut);

            star.Tag = storyboard;

            try
            {
                star.RenderTransformOrigin = new Point(0.5, 0.5);
                var scale = new ScaleTransform(1.0, 1.0);
                star.RenderTransform = scale;

                var twinkleFrames = new DoubleAnimationUsingKeyFrames
                {
                    Duration = new Duration(TimeSpan.FromSeconds(withDelay)),
                    BeginTime = TimeSpan.FromSeconds(periodDelay)
                };

                // Start at normal scale
                twinkleFrames.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));

                // Add a few random twinkle keyframes
                int twinkleCount = 2 + (int)(_random.NextDouble() * 3);
                for (int k = 0; k < twinkleCount; k++)
                {
                    double frac = 0.1 + _random.NextDouble() * 0.8; // between 10% and 90%
                    double tSeconds = frac * withDelay;
                    double scaleValue = 0.7 + _random.NextDouble() * 0.7;
                    twinkleFrames.KeyFrames.Add(new SplineDoubleKeyFrame(scaleValue, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(tSeconds)), new KeySpline(0.25, 0.1, 0.25, 1)));
                }

                // End back at normal scale
                twinkleFrames.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(withDelay))));

                var twinkleFramesY = twinkleFrames.Clone();

                Storyboard.SetTarget(twinkleFrames, star);
                Storyboard.SetTargetProperty(twinkleFrames, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
                Storyboard.SetTarget(twinkleFramesY, star);
                Storyboard.SetTargetProperty(twinkleFramesY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

                storyboard.Children.Add(twinkleFrames);
                storyboard.Children.Add(twinkleFramesY);
            }
            catch
            {
            }

            storyboard.Completed += (s, e) =>
            {
                // looks like we get here after clearing/freeing star still, similar to rect issue.
                // temporary band-aid to avoid exceptions

                // must clean/free this one
                storyboard.Stop();
                storyboard.Remove(); // Remove storyboard from the clock system
                star.BeginAnimation(UIElement.OpacityProperty, null); // Detach animations

                if (starSession == staticStarSession && isZen)  // if we got here from the completion of a star that is not part of this zen session then just stop.
                {
                    StarsCanvas?.Children.Remove(star);
                    CreateStar(canvasWidth, canvasHeight, repeatCount, starSession);
                }
            };

            // Start the animation
            storyboard.Begin(this);
        }

        private void LaunchShootingStar()
        {
            if (!isZen) return;

            double width = MyGrid.ActualWidth;
            double height = MyGrid.ActualHeight;
            if (width <= 0 || height <= 0) return;

            // Choose start off-screen on the left/top area and end off-screen on the right/bottom area
            double startX = -(140 + 40); // start well off left so it always enters the visible area
            double startY = -(_random.NextDouble() * (height * 0.2)); // may start slightly above

            double endX = width + (140 + 40); // end well off right
            double endY = height + (_random.NextDouble() * (height * 0.2)); // may end slightly below

            double durationSeconds = 1.0 + _random.NextDouble() * 0.8;

            var headSize = 6 + _random.Next(0, 4);
            var head = new Ellipse
            {
                Width = headSize,
                Height = headSize,
                Fill = Brushes.White,
                Opacity = 0
            };

            var tail = new Rectangle
            {
                Width = 140,
                Height = 3,
                RadiusX = 1,
                RadiusY = 1,
                Opacity = 0,
                RenderTransformOrigin = new Point(0, 0.5)
            };

            // Tail gradient from white to transparent
            var gbrush = new LinearGradientBrush();
            gbrush.StartPoint = new Point(0, 0.5);
            gbrush.EndPoint = new Point(1, 0.5);
            gbrush.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), 0.0));
            gbrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF), 0.3));
            gbrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 1.0));
            tail.Fill = gbrush;

            try
            {
                head.Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 2 };
                tail.Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 1.5 };
            }
            catch { }

            // Place both on a container canvas so we can rotate the tail to match trajectory
            var container = new Canvas { IsHitTestVisible = false };
            container.Children.Add(tail);
            container.Children.Add(head);

            // initial placement: position container such that (0,0) is the head center
            Canvas.SetLeft(container, startX);
            Canvas.SetTop(container, startY);
            // Place head so its center is at (0,0) inside container
            Canvas.SetLeft(head, -head.Width / 2);
            Canvas.SetTop(head, -head.Height / 2);
            // Place tail so its right end meets the head center at (0,0)
            Canvas.SetLeft(tail, -tail.Width);
            Canvas.SetTop(tail, -tail.Height / 2);

            // compute angle of trajectory
            double dx = endX - startX;
            double dy = endY - startY;
            double angle = Math.Atan2(dy, dx) * (180.0 / Math.PI);
            // Combine scale and rotation so we can animate tail shortening via ScaleX.
            // Set RenderTransformOrigin so rotation and scaling pivot at the tail's right end (attached to head).
            tail.RenderTransformOrigin = new Point(1.0, 0.5);
            var scale = new ScaleTransform(1.0, 1.0);
            var rotate = new RotateTransform(angle);
            var tg = new TransformGroup();
            tg.Children.Add(scale);
            tg.Children.Add(rotate);
            tail.RenderTransform = tg;

            // add to visual tree above star canvas
            container.Opacity = 1;
            // Prefer adding to the StarsCanvas (a Canvas) so Canvas.Left/Top animations work.
            if (StarsCanvas != null)
            {
                if (!StarsCanvas.Children.Contains(container))
                    StarsCanvas.Children.Add(container);
                Canvas.SetZIndex(container, 100000);
            }
            else
            {
                if (!MyGrid.Children.Contains(container))
                    MyGrid.Children.Add(container);
                Canvas.SetZIndex(container, 100000);
            }

            // Animations: move container from start to end; fade head/tail in/out; tail shortens via ScaleX
            var moveX = new DoubleAnimation
            {
                From = startX,
                To = endX,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            var moveY = new DoubleAnimation
            {
                From = startY,
                To = endY,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            var headFade = new DoubleAnimationUsingKeyFrames();
            headFade.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            headFade.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.03))));
            headFade.KeyFrames.Add(new SplineDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(durationSeconds - 0.03))));

            var tailFade = new DoubleAnimationUsingKeyFrames();
            tailFade.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            tailFade.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.03))));
            tailFade.KeyFrames.Add(new SplineDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(durationSeconds - 0.03))));

            // Animate tail shortening via ScaleX on its RenderTransform (first child of TransformGroup)
            var tailShorten = new DoubleAnimation
            {
                From = 1.0,
                To = 0.08,
                Duration = TimeSpan.FromSeconds(durationSeconds * 0.9),
                EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut }
            };

            // Apply animations
            moveX.Completed += (s, e) =>
            {
                try
                {
                    MyGrid.Children.Remove(container);
                }
                catch { }
            };

            // Use composition: animate Canvas.Left/Top via attached properties
            container.BeginAnimation(Canvas.LeftProperty, moveX);
            container.BeginAnimation(Canvas.TopProperty, moveY);

            head.BeginAnimation(UIElement.OpacityProperty, headFade);
            tail.BeginAnimation(UIElement.OpacityProperty, tailFade);
            try
            {
                // scale is first child in TransformGroup
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, tailShorten);
            }
            catch { }
        }

        

        private Duration DetermineLifespan(int layerIndex, int fadeInDurationSeconds)
        {
            const int lifespanQuadraticBase = 5;
            const int minLifespanSecondsBeyondFadeIn = 5;
            var originalLifespan = new Duration(TimeSpan.FromSeconds(fadeInDurationSeconds + minLifespanSecondsBeyondFadeIn + Math.Pow(_random.NextDouble() * lifespanQuadraticBase, 2)));
            var prevLayer = magicLayers.Where(ml => ml.layerIndex == layerIndex - 1).FirstOrDefault();
            if (prevLayer == null)
            {
                return originalLifespan;
            }

            var now = DateTime.Now;
            var prevLayerAge = now - prevLayer.birth;
            if (prevLayerAge > prevLayer.lifespan)  // in this case the layer is just waiting to be replaced by another
                return originalLifespan;
            var remaining = prevLayer.lifespan - prevLayerAge;
            return remaining + originalLifespan;
        }

        private double DetermineGradientAngle(int layerIndex)  // TODO merely assuming we are talking degrees and not radians. can also be improved: avoid other layers and avoid 180 degrees away.
        {
            var prevLayer = magicLayers.Where(ml => ml.layerIndex == layerIndex - 1).FirstOrDefault();
            if (prevLayer == null)
            {
                return _random.NextDouble() * 360;
            }

            var prevAngle = prevLayer.gradientAngle;
            const double berth = 20;
            var newAngle = prevAngle + berth + _random.NextDouble() * (360 - berth + prevAngle); // e.g. berth 20 prev 90 allows range 110 through 430 (430 is 70).
            return newAngle % 360;
        }

        private void GradientMagic()
        {
            NameScope.SetNameScope(this, new NameScope());

            this.Background = new SolidColorBrush(new System.Windows.Media.Color() { ScA = 1 });
            magicLayersCreated = 0;

            for (int i = 0; i < CONCURRENT_ZEN_LAYERS; i++)
            {
                var ml = CreateMagicLayer();
                ml.rectStoryboard?.Begin(this);
            }
        }


        // TODO explore: timings? opacities? incorrect fills? things being removed from memory or not removed when needed?
        private MagicLayer CreateMagicLayer()
        {
            double baseOpacity = 0.7;  // 0.69 was good, up from .49 and .34. Gets more interesting after initial mud phase.
            var mlLayerIndex = magicLayersCreated++;
            bool isFirstLayer = mlLayerIndex == 0;
            bool isFirstRound = mlLayerIndex < CONCURRENT_ZEN_LAYERS;
            int fadeInDurationSeconds = isFirstRound ? 0 : 5;
            var gradientStopStoryboard = new Storyboard();

            bool isEmptyLayer = !isFirstRound && _random.NextDouble() < 0;  // occasional empty layer, can create neat effects, especially if there are several
            if (isEmptyLayer)
            {
                baseOpacity = 0;
            }
            
            var mlBirth = DateTime.Now;
            // Checks lower layer's lifespan to ensure this one lasts beyond it.
            // This lifespan includes fade-in time. after this lifespan expires, this layer will transition to full opacity, retire the previous dead layer, and will be retired as soon as the next layer is ready to replace it.
            var mlLifeSpan = DetermineLifespan(mlLayerIndex, fadeInDurationSeconds); 
            var mlGradientAngle = DetermineGradientAngle(mlLayerIndex);  // Gets angle not too near to previous angle.
            var mlGscs = CreateGradientStopContexts(mlLayerIndex, gradientStopStoryboard);  // includes using the MainWindow's RegisterName to register each GradientStop.

            LinearGradientBrush gradientBrush = new LinearGradientBrush(new GradientStopCollection(mlGscs.Select(gsc => gsc.stop)), mlGradientAngle);

            Rectangle mlRect = new Rectangle()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Fill = gradientBrush,
                Opacity = isFirstLayer ? 1 : isFirstRound ? baseOpacity : 0
            };

            MyGrid.Children.Add(mlRect);
            var rectStoryboard = new Storyboard();
            var magicLayer = new MagicLayer()
            {
                gradientStopStoryboard = gradientStopStoryboard,
                layerIndex = mlLayerIndex,
                birth = mlBirth,
                lifespan = mlLifeSpan,
                gradientAngle = mlGradientAngle,
                gscs = mlGscs,
                rect = mlRect,
                rectStoryboard = rectStoryboard
            };

            // consider defining random seed for first-time use
            this.RegisterName("MyRect" + mlLayerIndex, mlRect);
            
            if (!isFirstRound)
            {
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = baseOpacity,
                    Duration = TimeSpan.FromSeconds(fadeInDurationSeconds),
                    //EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }  // can consider omitting or using others
                };

                Storyboard.SetTargetName(fadeIn, "MyRect" + mlLayerIndex);
                Storyboard.SetTargetProperty(fadeIn, new PropertyPath(Rectangle.OpacityProperty));

                rectStoryboard.Children.Add(fadeIn);
            }

            // TODO about excluding below. if you just have a bunch of same-opacity layers not fluctuating in opacity then it looks like mud.
            ////3.remains at this opacity for its lifespan (bonus: also fluctuate layer opacity during this time, some.).
            ///
            double finalFluctuationOpacity = -1;
            if (!isFirstLayer)
            {
                var fluctuationAnimation = new DoubleAnimationUsingKeyFrames
                {
                    Duration = TimeSpan.FromSeconds(magicLayer.lifespan.TimeSpan.TotalSeconds - fadeInDurationSeconds),
                    BeginTime = TimeSpan.FromSeconds(fadeInDurationSeconds),
                };
                const double MIN_FLUX_SECONDS = 4;
                const double MAX_FLUX_SECONDS = 15;  // intentionally disobeyed in while-loop for final fluctuation, to align well with other animations
                const double MIN_FLUX_OPACITY = 0.02;
                const double MAX_FLUX_OPACITY = 0.3;
                double timeToFluctuateSeconds = magicLayer.lifespan.TimeSpan.TotalSeconds - fadeInDurationSeconds;
                int fluxCount = 0;

                while (finalFluctuationOpacity == -1) // checking this instead of time in case of epsilon issue
                {
                    var thisFluctuationOpacity = MIN_FLUX_OPACITY + _random.NextDouble() * (MAX_FLUX_OPACITY - MIN_FLUX_OPACITY);
                    var thisFluctuationSeconds = MIN_FLUX_SECONDS + _random.NextDouble() * (MAX_FLUX_SECONDS - MIN_FLUX_SECONDS);
                    // if this will be so long that the next flux would be shorter than min length... (we are okay going over defined max in this edge case)
                    if (thisFluctuationSeconds + MIN_FLUX_SECONDS >= timeToFluctuateSeconds)
                    {
                        thisFluctuationSeconds = timeToFluctuateSeconds;  // use all remaining time
                        finalFluctuationOpacity = thisFluctuationOpacity;
                    }
                    timeToFluctuateSeconds -= thisFluctuationSeconds;

                    fluctuationAnimation.KeyFrames.Add(new SplineDoubleKeyFrame(
                        thisFluctuationOpacity,
                        KeyTime.FromTimeSpan(TimeSpan.FromSeconds(thisFluctuationSeconds)),
                        new KeySpline(0.25, 0.1, 0.25, 1) // Smooth easing. Can revisit.
                    ));
                    fluxCount++;
                }

                //Debug.Print($"Made a batch of {fluxCount} fluctuations totaling {magicLayer.lifespan.TimeSpan.TotalSeconds - fadeInDurationSeconds} seconds");
                Storyboard.SetTarget(fluctuationAnimation, magicLayer.rect);
                Storyboard.SetTargetProperty(fluctuationAnimation, new PropertyPath(Rectangle.OpacityProperty));
                rectStoryboard.Children.Add(fluctuationAnimation);
            }
            
            //4.After lifespan, transition opacity to 1.0.
            var fadeToFull = new DoubleAnimation
            {
                From = isFirstLayer ? 1 : finalFluctuationOpacity,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(7),  // making this a constant value is convenient so there is not a possibility of the next layer trying to delete this one before reaching full opacity. Otherwise include in lifespan calculations to not risk occasional flashes.
                BeginTime = magicLayer.lifespan.TimeSpan,
                //EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }  // can consider omitting or using others
            };
            Storyboard.SetTarget(fadeToFull, magicLayer.rect);
            Storyboard.SetTargetProperty(fadeToFull, new PropertyPath(Rectangle.OpacityProperty));
            rectStoryboard.Children.Add(fadeToFull);

            //5.Once at 1.0, delete and free the layer that was previously at 1.0 (it's the layer with one-lower index). Remain at 1.0 until another layer does the same.
            magicLayer.completedHandler = (s, e) =>
            {
                if (magicLayer.endOfLine)
                {
                    // TODO clean this up
                    return;
                }

                // Dequeue the old layer and free its resources.
                if (!isFirstLayer)
                {
                    if (!magicLayers.TryDequeue(out var expiredLayer))
                        throw new Exception("bad");
                    if (expiredLayer.layerIndex != mlLayerIndex - 1)
                        throw new Exception("also bad");
                    //Debug.Print($"Freed layer with opacity {expiredLayer.rect?.Opacity} while new ded layer had opacity {mlRect.Opacity}");
                    expiredLayer.Free(this, MyGrid);
                }

                if (magicLayers.Any(ml => ml.layerIndex < mlLayerIndex))
                    throw new Exception("also also bad");

                if (isZen)
                {
                    var ml = CreateMagicLayer(); // This is infinite recursion (though limited in constant space); may want to check that this is cleared as expected when exiting/resetting zen.
                    ml.rectStoryboard?.Begin(this);
                }
            };
            rectStoryboard.Completed += magicLayer.completedHandler;

            magicLayers.Enqueue(magicLayer);
            gradientStopStoryboard.Begin(this);
            //Debug.WriteLine("created magic layer with index " + magicLayer.layerIndex + " lifespan " + magicLayer.lifespan);
            return magicLayer;
        }

        internal class GradientStopContext
        {
            internal GradientStop? stop;
            internal string? name;
            internal AnimationTimeline? offsetAnimation;
            internal AnimationTimeline? colorAnimation;
        }

        private Queue<MagicLayer> magicLayers = new Queue<MagicLayer>();

        internal class MagicLayer
        {
            internal Rectangle? rect;  // has opacity and fill
            internal DateTime birth;
            internal Duration lifespan;
            internal Storyboard? gradientStopStoryboard;
            internal Storyboard? rectStoryboard;
            internal int layerIndex;
            internal List<GradientStopContext> gscs = new List<GradientStopContext>();
            internal double gradientAngle;
            internal bool endOfLine = false;
            internal EventHandler? completedHandler;

            internal void Free(MainWindow mainWindow, Grid parentOfRect) // pass in MainWindow ("this") and MyGrid
            {
                endOfLine = true;

                if (rect != null)
                {
                    rect.BeginAnimation(UIElement.OpacityProperty, null);
                    rect.Fill = null;
                    parentOfRect.Children.Remove(rect);
                }

                foreach (var gsc in gscs)
                {
                    mainWindow.UnregisterName(gsc.name);

                    if (gsc.stop != null)
                    {
                        gsc.stop.BeginAnimation(GradientStop.OffsetProperty, null);
                        gsc.stop.BeginAnimation(GradientStop.ColorProperty, null);
                    }
                }

                if (gradientStopStoryboard != null)
                {
                    foreach (var anim in gradientStopStoryboard.Children)
                        Storyboard.SetTarget(anim, null);

                    gradientStopStoryboard.Children.Clear();
                    gradientStopStoryboard.Stop();
                    gradientStopStoryboard.Remove();
                    gradientStopStoryboard = null;
                }

                if (rectStoryboard != null && completedHandler != null)
                {
                    rectStoryboard.Completed -= completedHandler;
                    completedHandler = null;
                }

                if (rectStoryboard != null)
                {
                    foreach (var anim in rectStoryboard.Children)
                        Storyboard.SetTarget(anim, null);

                    rectStoryboard.Children.Clear();
                    rectStoryboard.Stop();
                    rectStoryboard.Remove();
                    rectStoryboard = null;
                }

                gscs.Clear();

                mainWindow.UnregisterName("MyRect" + layerIndex);

                rect = null;
            }
        }

        private List<GradientStopContext> CreateGradientStopContexts(int layer, Storyboard storyboard)
        {
            GradientStopContext gsc0 = new GradientStopContext();
            GradientStopContext gsc1 = new GradientStopContext();
            GradientStopContext gsc2 = new GradientStopContext();
            GradientStopContext gsc3 = new GradientStopContext();

            // Create gradient stops for the brush.
            var tweak = (float)(_random.NextDouble() * 0.3 - 0.1);
            gsc0.stop = new GradientStop(System.Windows.Media.Color.FromScRgb(1F, 0.5F - tweak, 0F, 0.5F + tweak), 0.0);
            gsc1.stop = new GradientStop(System.Windows.Media.Color.FromScRgb(1F, 0F, 0F, 0.5F + tweak), 0.3);
            gsc2.stop = new GradientStop(System.Windows.Media.Color.FromScRgb(1F, 0.6F + tweak, 0F, 0.8F - tweak), 0.6);
            gsc3.stop = new GradientStop(System.Windows.Media.Color.FromScRgb(1F, 0F, 0.4F + tweak, 0.63F + tweak), 1.0);

            gsc0.name = "GradientStop0Layer" + layer;
            gsc1.name = "GradientStop1Layer" + layer;
            gsc2.name = "GradientStop2Layer" + layer;
            gsc3.name = "GradientStop3Layer" + layer;

            // Register a name for each gradient stop with the page so that they can be animated by a storyboard.
            this.RegisterName(gsc0.name, gsc0.stop);
            this.RegisterName(gsc1.name, gsc1.stop);
            this.RegisterName(gsc2.name, gsc2.stop);
            this.RegisterName(gsc3.name, gsc3.stop);

            // We've intentionally skipped index 0 to not animate that gradient stop.
            var oa1 = CreateOffsetAnimation(gsc1.name, TimeSpan.FromSeconds(15 + _random.NextDouble() * 15), 0.03, 0.30);
            var oa2 = CreateOffsetAnimation(gsc2.name, TimeSpan.FromSeconds(15 + _random.NextDouble() * 15), 0.73, 0.37);
            var oa3 = CreateOffsetAnimation(gsc3.name, TimeSpan.FromSeconds(15 + _random.NextDouble() * 15), 0.97, 0.8);
            storyboard.Children.Add(oa1);
            storyboard.Children.Add(oa2);
            storyboard.Children.Add(oa3);
            gsc1.offsetAnimation = oa1;
            gsc2.offsetAnimation = oa2;
            gsc3.offsetAnimation = oa3;

            Action<GradientStopContext> createOpacityOrColorAnim = gsc => 
            {
                if (false && layer > 0 && _random.NextDouble() > 0.5)  // disabled for debugging TODO. May be cause of flashing when layer is freed.
                {
                    // opacity anim
                    if (gsc.name == null) throw new Exception();
                    var opacityOA = CreateOpacityOrColorAnimation(gsc.name, TimeSpan.FromSeconds(10 + _random.NextDouble() * 20), System.Windows.Media.Color.FromScRgb(_random.NextDouble()>0.5? -1.0F : 1.0F, 0F, 0F, 0F));
                    gsc.colorAnimation = opacityOA;
                    storyboard.Children.Add(opacityOA);
                }
                else
                {
                    // color anim
                    var tweak1 = (float)(0.1 + _random.NextDouble() * 0.55);
                    var tweak2 = (float)(0.1 + _random.NextDouble() * 0.55);
                    var tweak3 = (float)(0.1 + _random.NextDouble() * 0.55);
                    if (gsc.name == null) throw new Exception();
                    var colorOA = CreateOpacityOrColorAnimation(gsc.name, TimeSpan.FromSeconds(10 + _random.NextDouble() * 20), System.Windows.Media.Color.FromScRgb(0F, tweak1, tweak2, tweak3));
                    gsc.colorAnimation = colorOA;
                    storyboard.Children.Add(colorOA);
                }
            };
            createOpacityOrColorAnim(gsc0);
            createOpacityOrColorAnim(gsc1);
            createOpacityOrColorAnim(gsc2);
            createOpacityOrColorAnim(gsc3);

            return new List<GradientStopContext>() { gsc0, gsc1, gsc2, gsc3 };  // can be simplified; do this earlier in method and use foreachs
        }

        private DoubleAnimation CreateOffsetAnimation(string target, Duration duration, double from, double to)
        {
            DoubleAnimation animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = duration,
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            Storyboard.SetTargetName(animation, target);
            Storyboard.SetTargetProperty(animation, new PropertyPath(GradientStop.OffsetProperty));
            return animation;
        }

        private ColorAnimation CreateOpacityOrColorAnimation(string targetName, Duration duration, System.Windows.Media.Color colorBy)
        {
            ColorAnimation animation = new ColorAnimation
            {
                By = colorBy,
                Duration = duration,
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTargetName(animation, targetName);
            Storyboard.SetTargetProperty(animation, new PropertyPath(GradientStop.ColorProperty));
            return animation;
        }
    }
}
